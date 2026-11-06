using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Editable, native WCS XY double-lines. Each boundary stores the exact center
    /// path, width and a group ID in XData, so edits always regenerate symmetrically
    /// about the invariant centerline instead of applying cumulative OFFSET deltas.
    /// </summary>
    public static class DualLineService
    {
        private const string RegApp = "YANGCAD_DUAL_V1";
        private const double Eps = 1e-8;
        private const int MaxVertices = 96;

        public sealed class Vertex
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Bulge { get; set; }
        }

        public sealed class Request
        {
            public IReadOnlyList<Point2d> Points { get; set; } = Array.Empty<Point2d>();
            public double Width { get; set; }  // drawing units, not millimetres
            public string Mode { get; set; } = "Pipe";
            public string Alignment { get; set; } = "Center";
            public string Corner { get; set; } = "Miter";
            public double CenterExtension { get; set; }
            public double Elevation { get; set; }
        }

        public sealed class Result
        {
            public bool success { get; set; }
            public string group_id { get; set; } = "";
            public double width { get; set; }
            public double old_width { get; set; }
            public bool center_unchanged { get; set; } = true;
            public string[] created_handles { get; set; } = Array.Empty<string>();
            public string[] replaced_handles { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "single_transaction_expected_windows_not_tested";
        }

        private sealed class Record
        {
            public string Group = "";
            public string Role = "";
            public string Mode = "";
            public double Width;
            public double CenterExtension;
            public double Elevation;
            public readonly List<Vertex> Center = new List<Vertex>();
        }

        public static double MillimetersPerDrawingUnit(Database db)
        {
            // Explicitly model unitless drawings as mm, matching common China CAD practice.
            // Other unrecognized units are rejected rather than silently drawing 1000 feet.
            switch (Convert.ToInt32(db.Insunits))
            {
                case 0: case 4: return 1;
                case 1: return 25.4;
                case 2: return 304.8;
                case 5: return 10;
                case 6: return 1000;
                case 7: return 1000000;
                default: throw new NotSupportedException(
                    "当前 INSUNITS 不是受支持的长度单位，请先设置单位（0/1/2/4/5/6/7）。");
            }
        }

        private static void CheckWidth(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value <= Eps || value > 1e9)
                throw new ArgumentOutOfRangeException(nameof(value), "双线宽度必须为有限正数。");
        }

        private static Polyline MakePolyline(IReadOnlyList<Vertex> vertices, double elevation = 0)
        {
            if (vertices.Count < 2 || vertices.Count > MaxVertices * 4)
                throw new ArgumentException("中心路径顶点数量无效。");
            var poly = new Polyline(vertices.Count) { Elevation = elevation };
            for (var i = 0; i < vertices.Count; i++)
                poly.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y),
                                 vertices[i].Bulge, 0, 0);
            return poly;
        }

        private static List<Vertex> Snapshot(Polyline poly)
        {
            var items = new List<Vertex>(poly.NumberOfVertices);
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                var p = poly.GetPoint2dAt(i);
                items.Add(new Vertex { X = p.X, Y = p.Y, Bulge = poly.GetBulgeAt(i) });
            }
            return items;
        }

        private static void ValidatePath(IReadOnlyList<Point2d> points, double width)
        {
            CheckWidth(width);
            if (points == null || points.Count < 2 || points.Count > MaxVertices)
                throw new ArgumentException("路径须包含 2～96 个顶点。");
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (double.IsNaN(p.X) || double.IsInfinity(p.X) ||
                    double.IsNaN(p.Y) || double.IsInfinity(p.Y))
                    throw new ArgumentException("路径坐标必须是有限数。");
                if (i > 0 && p.GetDistanceTo(points[i - 1]) <= Eps)
                    throw new ArgumentException("路径有重合相邻点。");
            }
            for (var i = 0; i + 1 < points.Count; i++)
                for (var j = i + 2; j + 1 < points.Count; j++)
                    if (Touch(points[i], points[i + 1], points[j], points[j + 1]))
                        throw new ArgumentException("路径自交或重复接触，已拒绝绘制双线。");
            // Reject 180-degree reversals and unresolved self-crossing; native OFFSET
            // may otherwise manufacture unexpected loops.
            for (var i = 1; i + 1 < points.Count; i++)
            {
                var a = (points[i] - points[i - 1]).GetNormal();
                var b = (points[i + 1] - points[i]).GetNormal();
                if (a.DotProduct(b) < -0.9999)
                    throw new ArgumentException("路径不能原路折返。");
            }
        }

        private static double Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;

        private static bool Touch(Point2d a, Point2d b, Point2d c, Point2d d)
        {
            var ab = b - a;
            var cd = d - c;
            var ac = c - a;
            var ad = d - a;
            var ca = a - c;
            var cb = b - c;
            var x1 = Cross(ab, ac); var x2 = Cross(ab, ad);
            var x3 = Cross(cd, ca); var x4 = Cross(cd, cb);
            if (((x1 > Eps && x2 < -Eps) || (x1 < -Eps && x2 > Eps)) &&
                ((x3 > Eps && x4 < -Eps) || (x3 < -Eps && x4 > Eps)))
                return true;
            return OnSegment(a, b, c) || OnSegment(a, b, d) ||
                   OnSegment(c, d, a) || OnSegment(c, d, b);
        }

        private static bool OnSegment(Point2d a, Point2d b, Point2d p)
        {
            var ab = b - a;
            if (Math.Abs(Cross(ab, p - a)) > Eps * Math.Max(1, ab.Length))
                return false;
            return p.X >= Math.Min(a.X, b.X) - Eps &&
                   p.X <= Math.Max(a.X, b.X) + Eps &&
                   p.Y >= Math.Min(a.Y, b.Y) - Eps &&
                   p.Y <= Math.Max(a.Y, b.Y) + Eps;
        }


        private static List<Vertex> MakeCenter(IReadOnlyList<Point2d> points,
                                               string corner, double width)
        {
            var result = new List<Vertex>();
            if (corner == "Miter" || points.Count < 3)
            {
                foreach (var p in points)
                    result.Add(new Vertex { X = p.X, Y = p.Y });
                return result;
            }

            var cuts = new double[points.Count];
            var turns = new double[points.Count];
            var radians = new double[points.Count];
            for (var i = 1; i + 1 < points.Count; i++)
            {
                var incoming = (points[i] - points[i - 1]).GetNormal();
                var outgoing = (points[i + 1] - points[i]).GetNormal();
                var angle = Math.Acos(Math.Max(-1, Math.Min(1, incoming.DotProduct(outgoing))));
                var cross = Cross(incoming, outgoing);
                if (angle <= 1e-6 || Math.Abs(cross) <= Eps) continue;
                var shortEdge = Math.Min(points[i].GetDistanceTo(points[i - 1]),
                                         points[i].GetDistanceTo(points[i + 1]));
                if (corner == "Bevel")
                    cuts[i] = Math.Min(width * 0.25, shortEdge * 0.30);
                else if (corner == "Round")
                {
                    var requestedRadius = width * 1.5;
                    cuts[i] = requestedRadius * Math.Tan(angle * 0.5);
                    if (cuts[i] > shortEdge * 0.35)
                        throw new InvalidOperationException(
                            "转角附近线段过短，无法放置满足管径的圆弧弯头。可延长线段或改用斜接。");
                    radians[i] = cuts[i] / Math.Tan(angle * 0.5);
                    if (radians[i] <= width * 0.5 + Eps)
                        throw new InvalidOperationException("内侧弯头半径不足。");
                    turns[i] = Math.Sign(cross) * angle;
                }
                else throw new ArgumentException("未知转角模式。");
            }
            for (var i = 0; i + 1 < points.Count; i++)
                if (cuts[i] + cuts[i + 1] >= points[i].GetDistanceTo(points[i + 1]) - Eps)
                    throw new InvalidOperationException("两个相邻转角过近，无法生成稳定的双线。");

            result.Add(new Vertex { X = points[0].X, Y = points[0].Y });
            for (var i = 1; i + 1 < points.Count; i++)
            {
                if (cuts[i] < Eps)
                {
                    result.Add(new Vertex { X = points[i].X, Y = points[i].Y });
                    continue;
                }
                var u = (points[i] - points[i - 1]).GetNormal();
                var v = (points[i + 1] - points[i]).GetNormal();
                var before = points[i] - u * cuts[i];
                var after = points[i] + v * cuts[i];
                result.Add(new Vertex { X = before.X, Y = before.Y,
                    Bulge = corner == "Round" ? Math.Tan(turns[i] * 0.25) : 0 });
                result.Add(new Vertex { X = after.X, Y = after.Y });
            }
            result.Add(new Vertex { X = points[points.Count - 1].X,
                                    Y = points[points.Count - 1].Y });
            return result;
        }

        private static Polyline GetOffset(Polyline center, double distance)
        {
            var raw = center.GetOffsetCurves(distance);
            Polyline? retained = null;
            try
            {
                if (raw.Count != 1 || !(raw[0] is Polyline))
                    throw new NotSupportedException(
                        "本次偏移生成多个或不受支持的曲线，已取消，避免错误的墙/管边界。");
                retained = (Polyline)raw[0];
                return retained;
            }
            finally
            {
                foreach (DBObject o in raw)
                    if (!ReferenceEquals(retained, o)) o.Dispose();
            }
        }

        private static List<Vertex> ExtendCenter(IReadOnlyList<Vertex> original,
                                                  double extension)
        {
            var mid = original.Select(x => new Vertex {
                X = x.X, Y = x.Y, Bulge = x.Bulge }).ToList();
            if (extension > Eps && mid.Count >= 2)
            {
                var first = new Point2d(mid[0].X, mid[0].Y);
                var second = new Point2d(mid[1].X, mid[1].Y);
                var last = new Point2d(mid[mid.Count - 1].X, mid[mid.Count - 1].Y);
                var beforeLast = new Point2d(mid[mid.Count - 2].X, mid[mid.Count - 2].Y);
                var v1 = (second - first).GetNormal();
                var v2 = (last - beforeLast).GetNormal();
                mid[0].X -= v1.X * extension;
                mid[0].Y -= v1.Y * extension;
                mid[mid.Count - 1].X += v2.X * extension;
                mid[mid.Count - 1].Y += v2.Y * extension;
            }
            return mid;
        }

        private static void CheckRadius(IReadOnlyList<Vertex> path, double width)
        {
            // Both offsets must remain on the same side of any fillet's center.
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var bulge = path[i].Bulge;
                if (Math.Abs(bulge) < Eps) continue;
                var dx = path[i + 1].X - path[i].X;
                var dy = path[i + 1].Y - path[i].Y;
                var chord = Math.Sqrt(dx * dx + dy * dy);
                var radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
                if (radius <= width * 0.5 + Eps)
                    throw new InvalidOperationException("修改后宽度超过圆弧弯头内半径限制。");
            }
        }

        private static (Polyline Left, Polyline Right) BuildSides(
            IReadOnlyList<Vertex> path, double width, double elevation)
        {
            CheckRadius(path, width);
            using (var center = MakePolyline(path, elevation))
            {
                Polyline? positive = null;
                Polyline? negative = null;
                try
                {
                    positive = GetOffset(center, width * .5);
                    negative = GetOffset(center, -width * .5);
                    var start = center.GetPoint2dAt(0);
                    var tangent = center.GetPoint2dAt(1) - start;
                    var side = positive.GetPoint2dAt(0) - start;
                    if (Math.Abs(Cross(tangent, side)) <= Eps)
                        throw new InvalidOperationException("无法判断左右双线方向，已取消。");
                    var positiveIsLeft = Cross(tangent, side) > 0;
                    var left = positiveIsLeft ? positive : negative;
                    var right = positiveIsLeft ? negative : positive;
                    positive = null; negative = null; // transferred ownership
                    return (left, right);
                }
                finally
                {
                    positive?.Dispose(); negative?.Dispose();
                }
            }
        }

        private static void EnsureRegApp(CadExecutionContext ctx)
        {
            var tr = ctx.Transaction;
            var table = (RegAppTable)tr.GetObject(ctx.Database.RegAppTableId, OpenMode.ForRead);
            if (table.Has(RegApp)) return;
            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = RegApp };
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static void Tag(Entity entity, Record record)
        {
            var vals = new List<TypedValue> {
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "V1"),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, record.Group),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, record.Role),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, record.Mode),
                new TypedValue((int)DxfCode.ExtendedDataReal, record.Width),
                new TypedValue((int)DxfCode.ExtendedDataReal, record.CenterExtension),
                new TypedValue((int)DxfCode.ExtendedDataReal, record.Elevation),
                new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)record.Center.Count)
            };
            foreach (var v in record.Center)
            {
                vals.Add(new TypedValue((int)DxfCode.ExtendedDataReal, v.X));
                vals.Add(new TypedValue((int)DxfCode.ExtendedDataReal, v.Y));
                vals.Add(new TypedValue((int)DxfCode.ExtendedDataReal, v.Bulge));
            }
            using (var buffer = new ResultBuffer(vals.ToArray())) entity.XData = buffer;
        }

        private static Record? Read(Entity entity)
        {
            using (var xdata = entity.GetXDataForApplication(RegApp))
            {
                if (xdata == null) return null;
                var values = xdata.AsArray();
                if (values.Length < 9 ||
                    Convert.ToString(values[1].Value) != "V1")
                    throw new InvalidOperationException("双线数据版本无法识别。");
                var r = new Record {
                    Group = Convert.ToString(values[2].Value) ?? "",
                    Role = Convert.ToString(values[3].Value) ?? "",
                    Mode = Convert.ToString(values[4].Value) ?? "",
                    Width = Convert.ToDouble(values[5].Value),
                    CenterExtension = Convert.ToDouble(values[6].Value),
                    Elevation = Convert.ToDouble(values[7].Value)
                };
                var count = Convert.ToInt32(values[8].Value);
                if (r.Group.Length == 0 || count < 2 || count > MaxVertices * 4 ||
                    values.Length != count * 3 + 9)
                    throw new InvalidOperationException("双线成组信息不完整。");
                CheckWidth(r.Width);
                if (double.IsNaN(r.Elevation) || double.IsInfinity(r.Elevation))
                    throw new InvalidOperationException("双线标高数据损坏。");
                if (double.IsNaN(r.CenterExtension) || double.IsInfinity(r.CenterExtension) ||
                    r.CenterExtension < 0)
                    throw new InvalidOperationException("中心线延长值损坏。");
                for (var i = 0; i < count; i++)
                    r.Center.Add(new Vertex {
                        X = Convert.ToDouble(values[9 + i * 3].Value),
                        Y = Convert.ToDouble(values[10 + i * 3].Value),
                        Bulge = Convert.ToDouble(values[11 + i * 3].Value)
                    });
                return r;
            }
        }

        private static ObjectId Append(CadExecutionContext ctx, Entity entity,
                                       BlockTableRecord space, Record record)
        {
            entity.SetDatabaseDefaults();
            if (entity is Polyline drawable) drawable.Elevation = record.Elevation;
            space.AppendEntity(entity);
            ctx.Transaction.AddNewlyCreatedDBObject(entity, true);
            Tag(entity, record);
            return entity.ObjectId;
        }

        public static Result Draw(CadExecutionContext ctx, Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ValidatePath(request.Points, request.Width);
            if (request.Mode != "Pipe" && request.Mode != "Double")
                throw new ArgumentException("绘制模式仅允许 Pipe 或 Double。");
            if (request.Alignment != "Center" && request.Alignment != "Left" &&
                request.Alignment != "Right")
                throw new ArgumentException("未知对齐方式。");
            if (double.IsNaN(request.CenterExtension) || request.CenterExtension < 0)
                throw new ArgumentException("中心线延长值不能为负。");
            if (double.IsNaN(request.Elevation) || double.IsInfinity(request.Elevation))
                throw new ArgumentException("双线标高必须有限。");
            var raw = MakeCenter(request.Points, request.Corner, request.Width);
            List<Vertex> centerPath = raw;
            if (request.Alignment != "Center")
            {
                using (var alignment = MakePolyline(raw, request.Elevation))
                using (var plus = GetOffset(alignment, request.Width * 0.5))
                using (var minus = GetOffset(alignment, -request.Width * 0.5))
                {
                    var origin = alignment.GetPoint2dAt(0);
                    var tangent = alignment.GetPoint2dAt(1) - origin;
                    var positiveSign = Cross(tangent, plus.GetPoint2dAt(0) - origin);
                    if (Math.Abs(positiveSign) <= Eps)
                        throw new InvalidOperationException("无法判断路径对齐方向。");
                    var shouldGoLeft = request.Alignment == "Right";
                    var choosePlus = (positiveSign > 0) == shouldGoLeft;
                    centerPath = Snapshot(choosePlus ? plus : minus);
                }
            }

            var tr = ctx.Transaction;
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForWrite);
            if (!space.IsLayout) throw new InvalidOperationException("仅支持当前模型或布局空间。");
            EnsureRegApp(ctx);
            var sides = BuildSides(centerPath, request.Width, request.Elevation);
            var group = Guid.NewGuid().ToString("N");
            var record = new Record { Group = group, Mode = request.Mode, Width = request.Width,
                                      CenterExtension = request.CenterExtension, Elevation = request.Elevation };
            record.Center.AddRange(centerPath);
            var handles = new List<string>();
            try
            {
                record.Role = "L";
                var idLeft = Append(ctx, sides.Left, space, record);
                handles.Add(idLeft.Handle.ToString());
                record.Role = "R";
                var idRight = Append(ctx, sides.Right, space, record);
                handles.Add(idRight.Handle.ToString());
                if (request.Mode == "Pipe")
                {
                    var mid = ExtendCenter(centerPath, request.CenterExtension);
                    var axis = MakePolyline(mid);
                    record.Role = "C";
                    var idCenter = Append(ctx, axis, space, record);
                    handles.Add(idCenter.Handle.ToString());
                }
                return new Result { success = true, group_id = group,
                                    width = request.Width, created_handles = handles.ToArray() };
            }
            catch { throw; } // transaction aborts, caller handles all appended entities
        }

        private static bool SamePolyline(Polyline actual, Polyline expected, double tol)
        {
            if (Math.Abs(actual.Elevation - expected.Elevation) > tol) return false;
            if (actual.NumberOfVertices != expected.NumberOfVertices ||
                actual.Closed != expected.Closed) return false;
            for (var i = 0; i < actual.NumberOfVertices; i++)
                if (actual.GetPoint2dAt(i).GetDistanceTo(expected.GetPoint2dAt(i)) > tol ||
                    Math.Abs(actual.GetBulgeAt(i) - expected.GetBulgeAt(i)) > 1e-5)
                    return false;
            return true;
        }

        public static double? StoredWidth(CadExecutionContext ctx, ObjectId selected)
        {
            if (selected.IsNull) return null;
            var e = ctx.Transaction.GetObject(selected, OpenMode.ForRead, false) as Entity;
            return e == null ? null : Read(e)?.Width;
        }

        public static Result ChangeWidth(CadExecutionContext ctx, ObjectId selected, double width)
        {
            CheckWidth(width);
            var tr = ctx.Transaction;
            var picked = tr.GetObject(selected, OpenMode.ForRead, false) as Entity
                ?? throw new ArgumentException("请选择有效双线边界。");
            var metadata = Read(picked) ??
                throw new ArgumentException("该对象不是 YangCAD 成组双线；旧 LINE 请采用双线配对模式。");
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForRead);
            if (picked.OwnerId != ctx.Database.CurrentSpaceId)
                throw new InvalidOperationException("只能修改当前空间的双线。");
            var found = new Dictionary<string, (ObjectId Id, Polyline Poly)>();
            foreach (ObjectId id in space)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                if (entity == null || entity.IsErased) continue;
                var info = Read(entity);
                if (info == null || info.Group != metadata.Group) continue;
                if (found.ContainsKey(info.Role))
                    throw new InvalidOperationException("发现复制后重复的双线组，无法判断配对，请拆分后重建。");
                if (!(entity is Polyline p))
                    throw new InvalidOperationException("双线图元类型被修改，已取消。");
                found.Add(info.Role, (id, p));
            }
            if (!found.ContainsKey("L") || !found.ContainsKey("R") ||
                found.Count != (metadata.Mode == "Pipe" ? 3 : 2))
                throw new InvalidOperationException("左右边线或中心线缺失，不能安全修改宽度。");
            foreach (var item in found.Values)
            {
                var layer = (LayerTableRecord)tr.GetObject(item.Poly.LayerId, OpenMode.ForRead);
                if (layer.IsLocked || layer.IsFrozen || layer.IsOff)
                    throw new InvalidOperationException("双线存在锁定/冻结/关闭图层，已取消。");
            }

            var oldSides = BuildSides(metadata.Center, metadata.Width, metadata.Elevation);
            try
            {
                var tol = Math.Max(1e-5, metadata.Width * 1e-6);
                if (!SamePolyline(found["L"].Poly, oldSides.Left, tol) ||
                    !SamePolyline(found["R"].Poly, oldSides.Right, tol))
                    throw new InvalidOperationException("双线已被手动编辑，与记录的中轴不匹配；请重新配对。");
            }
            finally { oldSides.Left.Dispose(); oldSides.Right.Dispose(); }

            if (found.ContainsKey("C"))
            {
                using (var expectedCenter = MakePolyline(ExtendCenter(
                           metadata.Center, metadata.CenterExtension), metadata.Elevation))
                {
                    if (!SamePolyline(found["C"].Poly, expectedCenter, Math.Max(1e-5, width * 1e-6)))
                        throw new InvalidOperationException(
                            "管道中心线已手动改变；先重新建立成组关系，禁止偏移到错误的中心。");
                }
            }
            var newSides = BuildSides(metadata.Center, width, metadata.Elevation);
            var handles = new List<string>();
            var replaced = new List<string>();
            space.UpgradeOpen();
            EnsureRegApp(ctx);
            foreach (var kv in new[] {
                (Role:"L", Entity:newSides.Left),
                (Role:"R", Entity:newSides.Right) })
            {
                var original = found[kv.Role].Poly;
                kv.Entity.SetPropertiesFrom(original);
                var r = new Record { Group = metadata.Group, Mode = metadata.Mode,
                                     Role = kv.Role, Width = width,
                                     CenterExtension = metadata.CenterExtension, Elevation = metadata.Elevation };
                r.Center.AddRange(metadata.Center);
                space.AppendEntity(kv.Entity);
                tr.AddNewlyCreatedDBObject(kv.Entity, true);
                Tag(kv.Entity, r);
                handles.Add(kv.Entity.Handle.ToString());
                replaced.Add(original.Handle.ToString());
                original.UpgradeOpen();
                original.Erase();
            }
            if (found.ContainsKey("C"))
            {
                var c = found["C"].Poly;
                c.UpgradeOpen();
                var r = new Record { Group = metadata.Group, Mode = metadata.Mode,
                                     Role = "C", Width = width,
                                     CenterExtension = metadata.CenterExtension, Elevation = metadata.Elevation };
                r.Center.AddRange(metadata.Center);
                Tag(c, r); // no change to centerline geometry/Handle
            }
            return new Result { success = true, width = width, old_width = metadata.Width,
                                group_id = metadata.Group, center_unchanged = true,
                                created_handles = handles.ToArray(), replaced_handles = replaced.ToArray() };
        }

        public static double MeasureLegacyLinePair(CadExecutionContext ctx,
            ObjectId firstId, ObjectId secondId)
        {
            var tr = ctx.Transaction;
            if (firstId.IsNull || secondId.IsNull || firstId == secondId)
                throw new ArgumentException("需要两条不同的 LINE。");
            var first = tr.GetObject(firstId, OpenMode.ForRead, false) as Line;
            var second = tr.GetObject(secondId, OpenMode.ForRead, false) as Line;
            if (first == null || second == null ||
                first.OwnerId != ctx.Database.CurrentSpaceId ||
                second.OwnerId != ctx.Database.CurrentSpaceId)
                throw new ArgumentException("必须选当前空间内的两条普通 LINE。");
            var direction = first.EndPoint - first.StartPoint;
            var length = direction.Length;
            var otherLen = second.StartPoint.DistanceTo(second.EndPoint);
            if (length <= Eps || Math.Abs(otherLen - length) > 1e-5)
                throw new ArgumentException("两线长度不等或零长度。");
            if (Math.Abs(first.StartPoint.Z - first.EndPoint.Z) > Eps ||
                Math.Abs(second.StartPoint.Z - second.EndPoint.Z) > Eps ||
                Math.Abs(first.StartPoint.Z - second.StartPoint.Z) > Eps)
                throw new ArgumentException("两线必须在同一 WCS XY 平面。");
            var unit = direction / length;
            var otherDir = (second.EndPoint - second.StartPoint) / otherLen;
            var reversed = otherDir.DotProduct(unit) < 0;
            if (Math.Abs(Math.Abs(otherDir.DotProduct(unit)) - 1) > 1e-5)
                throw new ArgumentException("两线必须严格平行。");
            var counterpart = reversed ? second.EndPoint : second.StartPoint;
            var delta = counterpart - first.StartPoint;
            var projection = delta.DotProduct(unit);
            if (Math.Abs(projection) > 1e-5)
                throw new ArgumentException("两条 LINE 的端点未对齐，不能确定共同中轴。");
            var normal = Vector3d.ZAxis.CrossProduct(unit);
            if (normal.Length <= Eps)
                throw new ArgumentException("两线不是 XY 平面双线。");
            var width = Math.Abs(delta.DotProduct(normal.GetNormal()));
            if (width <= Eps) throw new ArgumentException("两条线重合，没有可测宽度。");
            return width;
        }

        /// <summary>
        /// Legacy XY LINE pair: keep both center endpoints EXACT, change each side by
        /// +/- half the width delta; never offset just one boundary.
        /// </summary>
        public static Result ChangeLegacyLinePair(CadExecutionContext ctx,
            ObjectId firstId, ObjectId secondId, double newWidth)
        {
            CheckWidth(newWidth);
            if (firstId == secondId || firstId.IsNull || secondId.IsNull)
                throw new ArgumentException("请选择两条不同的普通 LINE。");
            var tr = ctx.Transaction;
            var a = tr.GetObject(firstId, OpenMode.ForRead, false) as Line;
            var b = tr.GetObject(secondId, OpenMode.ForRead, false) as Line;
            if (a == null || b == null)
                throw new ArgumentException("旧图元配对首版仅支持两条 LINE。");
            if (a.OwnerId != ctx.Database.CurrentSpaceId || b.OwnerId != ctx.Database.CurrentSpaceId)
                throw new InvalidOperationException("两条线必须处于同一当前空间。");
            foreach (var ent in new[] { a, b })
            {
                var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                if (layer.IsLocked || layer.IsOff || layer.IsFrozen)
                    throw new InvalidOperationException("配对线图层被锁定、关闭或冻结。");
                if (Math.Abs(ent.StartPoint.Z - ent.EndPoint.Z) > Eps)
                    throw new NotSupportedException("旧线配对暂不支持三维坡度线。");
            }
            if (Math.Abs(a.StartPoint.Z - b.StartPoint.Z) > Eps)
                throw new NotSupportedException("两条配对线必须同一 Z 平面。");
            var len1 = a.StartPoint.DistanceTo(a.EndPoint);
            var len2 = b.StartPoint.DistanceTo(b.EndPoint);
            if (len1 <= Eps || len2 <= Eps)
                throw new ArgumentException("不能配对零长度 LINE。");
            var u = (a.EndPoint - a.StartPoint) / len1;
            var v = (b.EndPoint - b.StartPoint) / len2;
            var reversed = u.DotProduct(v) < 0;
            var bStart = reversed ? b.EndPoint : b.StartPoint;
            var bEnd = reversed ? b.StartPoint : b.EndPoint;
            if (Math.Abs(len1 - len2) > 1e-5 ||
                Math.Abs(u.DotProduct((bEnd - bStart) / len2) - 1) > 1e-5)
                throw new InvalidOperationException("两条 LINE 方向或长度不匹配，拒绝自动配对。");
            var offset = bStart - a.StartPoint;
            var axial = offset.DotProduct(u);
            var normal = Vector3d.ZAxis.CrossProduct(u);
            if (normal.Length <= Eps)
                throw new NotSupportedException("仅支持 XY 平面内的平行 LINE。");
            normal = normal.GetNormal();
            var signedDistance = offset.DotProduct(normal);
            var oldWidth = Math.Abs(signedDistance);
            if (oldWidth <= Eps || Math.Abs(axial) > 1e-5)
                throw new InvalidOperationException("两线端点不对应或重合，无法确定中轴。");
            var aNew = normal * ((signedDistance > 0 ? -1 : 1) * newWidth * .5);
            var bNew = -aNew;
            var centerStart = a.StartPoint + offset * .5;
            var centerEnd = a.EndPoint + (bEnd - a.EndPoint) * .5;
            a.UpgradeOpen(); b.UpgradeOpen();
            if (!reversed)
            {
                b.StartPoint = centerStart + bNew;
                b.EndPoint = centerEnd + bNew;
            }
            else
            {
                b.StartPoint = centerEnd + bNew;
                b.EndPoint = centerStart + bNew;
            }
            a.StartPoint = centerStart + aNew;
            a.EndPoint = centerEnd + aNew;
            return new Result { success = true, width = newWidth, old_width = oldWidth,
                center_unchanged = true,
                replaced_handles = new[] { a.Handle.ToString(), b.Handle.ToString() } };
        }
    }
}
