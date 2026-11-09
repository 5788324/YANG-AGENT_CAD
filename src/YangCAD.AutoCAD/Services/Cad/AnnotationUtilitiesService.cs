using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>Independent annotation helpers. No source dimensions, lines or text are rewritten.</summary>
    public static class AnnotationUtilitiesService
    {
        private const double Eps = 1e-9;
        private const int MaxScan = 200000;
        private static bool Finite(double v) => !double.IsInfinity(v) && !double.IsNaN(v);
        private static string Number(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);

        public sealed class DimensionFilterResult
        {
            public ObjectId[] Ids { get; set; } = Array.Empty<ObjectId>();
            public int scanned { get; set; }
            public int found { get; set; }
            public int overridden_measurements { get; set; }
            public int with_measurement_token { get; set; }
            public int suppressed { get; set; }
        }

        /// <summary>
        /// DimensionText == "" is AutoCAD's automatic measurement text.
        /// Literal "<>" only is not a meaningful override; prefixes/suffixes are.
        /// Hidden "." and explicit blanks are user-supplied and remain selected.
        /// </summary>
        public static bool HasUserTextOverride(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (string.Equals(text.Trim(), "<>", StringComparison.Ordinal)) return false;
            return true;
        }

        public static DimensionFilterResult FindEditedDimensionText(CadExecutionContext context)
        {
            var tr = context.Transaction;
            var space = (BlockTableRecord)tr.GetObject(context.Database.CurrentSpaceId, OpenMode.ForRead);
            if (!space.IsLayout)
                throw new InvalidOperationException("只能筛选当前模型空间或布局中的尺寸标注。");
            var result = new DimensionFilterResult();
            var list = new List<ObjectId>();
            foreach (ObjectId id in space)
            {
                if (result.scanned++ >= MaxScan)
                    throw new InvalidOperationException("当前空间对象超过 20 万，请拆分图纸后筛选。");
                var dim = tr.GetObject(id, OpenMode.ForRead, false) as Dimension;
                if (dim == null || dim.IsErased) continue;
                string text;
                try { text = dim.DimensionText ?? ""; }
                catch { continue; }
                if (!HasUserTextOverride(text)) continue;
                list.Add(id);
                if (text.Contains("<>")) result.with_measurement_token++;
                else if (string.IsNullOrWhiteSpace(text) || text.Trim() == ".")
                    result.suppressed++;
                else result.overridden_measurements++;
            }
            result.Ids = list.ToArray();
            result.found = list.Count;
            return result;
        }

        public static double MillimetersPerUnit(Database db)
        {
            switch (Convert.ToInt32(db.Insunits))
            {
                case 0: case 4: return 1;
                case 1: return 25.4;
                case 2: return 304.8;
                case 5: return 10;
                case 6: return 1000;
                case 7: return 1000000;
                default:
                    throw new NotSupportedException(
                        "当前 INSUNITS 不支持毫米换算。请改为 0/1/2/4/5/6/7，再使用文字工具。");
            }
        }

        private static void ValidateCurrentLayer(CadExecutionContext context)
        {
            var layer = (LayerTableRecord)context.Transaction.GetObject(
                context.Database.Clayer, OpenMode.ForRead);
            if (layer.IsLocked || layer.IsFrozen || layer.IsOff)
                throw new InvalidOperationException("当前图层已锁定、冻结或关闭；没有生成新标注。");
        }

        private static string AddEntity(CadExecutionContext context,
                                        BlockTableRecord space, Entity entity)
        {
            entity.SetDatabaseDefaults();
            space.AppendEntity(entity);
            context.Transaction.AddNewlyCreatedDBObject(entity, true);
            return entity.Handle.ToString();
        }

        private static Point3d Point(Point2d p, double z) => new Point3d(p.X, p.Y, z);
        private static Vector2d Left(Vector2d v) => new Vector2d(-v.Y, v.X);

        public sealed class SlopeRequest
        {
            public Point3d First { get; set; }
            public Point3d Second { get; set; }
            /// <summary>Section: rise=Y, run=|X|. Plan3D: rise=Z, run=XY length.</summary>
            public string Basis { get; set; } = "Section";
            public string Format { get; set; } = "Percent";
            public double TextHeight { get; set; }
            public double Offset { get; set; }
        }

        public sealed class SlopeResult
        {
            public double run { get; set; }
            public double rise { get; set; }
            public double percent { get; set; }
            public double degrees { get; set; }
            public string label { get; set; } = "";
            public string[] created_handles { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "not_native_tested";
        }

        /// <summary>
        /// The reference LINE is never edited. Draw independent marker line,
        /// two arrow arms and one native MText in the same caller transaction.
        /// The arrow points toward the LOWER end.
        /// </summary>
        public static SlopeResult MarkSlope(CadExecutionContext ctx, SlopeRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (!new[] { req.First.X, req.First.Y, req.First.Z, req.Second.X,
                         req.Second.Y, req.Second.Z, req.TextHeight, req.Offset }.All(Finite))
                throw new ArgumentException("坐标、高度或偏移存在非有限数。");
            if (req.TextHeight <= Eps || req.Offset < 0)
                throw new ArgumentException("文字高度须大于 0，偏移距离不能为负。");
            if (req.Basis != "Section" && req.Basis != "Plan3D")
                throw new ArgumentException("斜率基准只能是 Section 或 Plan3D。");
            if (req.Format != "Degrees" && req.Format != "Percent" && req.Format != "Ratio")
                throw new ArgumentException("斜率文字格式只能为 Degrees / Percent / Ratio。");

            var p = new Point2d(req.First.X, req.First.Y);
            var q = new Point2d(req.Second.X, req.Second.Y);
            var vector = q - p;
            var xyLength = vector.Length;
            if (xyLength <= Eps) throw new ArgumentException("两点在 XY 投影中重合，无法画坡度指示线。");
            var run = req.Basis == "Section" ? Math.Abs(req.Second.X - req.First.X) : xyLength;
            if (run <= Eps)
                throw new ArgumentException("坡度水平距离为零，无法显示百分比或 1:X。");
            var rise = req.Basis == "Section"
                ? req.Second.Y - req.First.Y : req.Second.Z - req.First.Z;
            var ratio = Math.Abs(rise / run);
            var degrees = Math.Atan(ratio) * 180.0 / Math.PI;
            var label = req.Format == "Degrees"
                ? Number(degrees) + "°"
                : req.Format == "Percent"
                    ? Number(ratio * 100) + "%"
                    : ratio <= Eps ? "1:∞" : "1:" + Number(1.0 / ratio);

            var unit = vector.GetNormal();
            var normal = Left(unit);
            var mid = p + vector * 0.5;
            var length = Math.Min(xyLength * 0.6, Math.Max(req.TextHeight * 8, xyLength * 0.2));
            var markMid = mid + normal * req.Offset;
            var a = markMid - unit * (length * 0.5);
            var b = markMid + unit * (length * 0.5);
            var z = req.Basis == "Section" ? req.First.Z : req.First.Z;

            // Arrow travels from the higher elevation/ordinate to the lower one.
            var downhillTowardSecond = rise < 0 || Math.Abs(rise) <= Eps;
            var tip = downhillTowardSecond ? b : a;
            var backward = downhillTowardSecond ? -unit : unit;
            var arrowLength = Math.Min(length * 0.2, req.TextHeight * 0.6);
            var wing1 = tip + backward * arrowLength + normal * (arrowLength * 0.45);
            var wing2 = tip + backward * arrowLength - normal * (arrowLength * 0.45);

            var direction = Math.Atan2(unit.Y, unit.X);
            if (direction > Math.PI * 0.5) direction -= Math.PI;
            if (direction < -Math.PI * 0.5) direction += Math.PI;
            var position = markMid + normal * (req.TextHeight * 0.45);
            ValidateCurrentLayer(ctx);
            var space = (BlockTableRecord)ctx.Transaction.GetObject(
                ctx.Database.CurrentSpaceId, OpenMode.ForWrite);
            if (!space.IsLayout) throw new InvalidOperationException("只支持当前模型或布局空间。");
            var handles = new List<string>();
            using (var mtext = new MText {
                Location = Point(position, z),
                Contents = label,
                TextHeight = req.TextHeight,
                Rotation = direction,
                Attachment = AttachmentPoint.BottomCenter,
                Width = 0
            })
            {
                handles.Add(AddEntity(ctx, space, new Line(Point(a, z), Point(b, z))));
                handles.Add(AddEntity(ctx, space, new Line(Point(tip, z), Point(wing1, z))));
                handles.Add(AddEntity(ctx, space, new Line(Point(tip, z), Point(wing2, z))));
                handles.Add(AddEntity(ctx, space, mtext));
            }
            return new SlopeResult {
                run = run, rise = rise, percent = ratio * 100,
                degrees = degrees, label = label, created_handles = handles.ToArray()
            };
        }

        public sealed class FrameRequest
        {
            public ObjectId TextId { get; set; } = ObjectId.Null;
            public string Shape { get; set; } = "Rectangle";
            public double Margin { get; set; } // drawing units
        }

        public sealed class FrameResult
        {
            public string shape { get; set; } = "";
            public string created_handle { get; set; } = "";
            public string source_handle { get; set; } = "";
            public double margin { get; set; }
            public string undo_semantics { get; set; } = "not_native_tested";
        }

        private static bool WorldXY(Vector3d n) =>
            n.Length > Eps && n.GetNormal().DotProduct(Vector3d.ZAxis) > 0.999999;

        private static List<Point3d> TextCorners(Entity text, out Vector2d direction)
        {
            if (text is MText mt)
            {
                if (!WorldXY(mt.Normal))
                    throw new NotSupportedException("暂不支持非 WCS XY 平面的多行文字。");
                var v = mt.Direction;
                direction = new Vector2d(v.X, v.Y).GetNormal();
                var pts = mt.GetBoundingPoints();
                if (pts == null || pts.Count < 4)
                    throw new InvalidOperationException("无法读取多行文字实际显示范围。");
                // AutoCAD GetBoundingPoints can use the MTEXT wrapping-width
                // rectangle rather than visible glyph width. Correct the right edge
                // with ActualWidth so a short label in a wide MText box gets a tight frame.
                var corners = pts.Cast<Point3d>().ToList();
                var actualWidth = mt.ActualWidth;
                if (Finite(actualWidth) && actualWidth > Eps)
                {
                    corners[1] = corners[0] + new Vector3d(
                        direction.X * actualWidth, direction.Y * actualWidth, 0);
                    corners[3] = corners[2] + new Vector3d(
                        direction.X * actualWidth, direction.Y * actualWidth, 0);
                }
                return corners;
            }
            if (text is DBText dbt)
            {
                if (!WorldXY(dbt.Normal))
                    throw new NotSupportedException("暂不支持倾斜 OCS 平面的单行文字。");
                direction = new Vector2d(Math.Cos(dbt.Rotation), Math.Sin(dbt.Rotation));
                // Temporarily unrotate a clone, read actual glyph extents, then
                // rotate corners back. Never alter the original DBText.
                using (var clone = (DBText)dbt.Clone())
                {
                    var unrotate = Matrix3d.Rotation(-dbt.Rotation, Vector3d.ZAxis, dbt.Position);
                    clone.TransformBy(unrotate);
                    var ext = clone.GeometricExtents;
                    var unrotated = new[] {
                        new Point3d(ext.MinPoint.X, ext.MinPoint.Y, ext.MinPoint.Z),
                        new Point3d(ext.MaxPoint.X, ext.MinPoint.Y, ext.MinPoint.Z),
                        new Point3d(ext.MaxPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z),
                        new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z)
                    };
                    var restore = Matrix3d.Rotation(dbt.Rotation, Vector3d.ZAxis, dbt.Position);
                    return unrotated.Select(p => p.TransformBy(restore)).ToList();
                }
            }
            throw new ArgumentException("只支持原生 TEXT 或 MTEXT。");
        }

        public static FrameResult FrameText(CadExecutionContext ctx, FrameRequest req)
        {
            if (req == null || req.TextId.IsNull)
                throw new ArgumentException("请先选中一条 TEXT 或 MTEXT。");
            if (req.Shape != "Rectangle" && req.Shape != "Circle")
                throw new ArgumentException("文字框类型只能是 Rectangle 或 Circle。");
            if (!Finite(req.Margin) || req.Margin < 0 || req.Margin > 1e8)
                throw new ArgumentOutOfRangeException(nameof(req.Margin), "文字框偏移距离不合法。");
            var tr = ctx.Transaction;
            var text = tr.GetObject(req.TextId, OpenMode.ForRead, false) as Entity;
            if (!(text is DBText) && !(text is MText))
                throw new ArgumentException("只支持原生 TEXT 或 MTEXT。");
            if (text.OwnerId != ctx.Database.CurrentSpaceId)
                throw new InvalidOperationException("请选择当前模型空间或布局内的文字。");

            var points = TextCorners(text, out var direction);
            if (direction.Length <= Eps)
                throw new InvalidOperationException("文字旋转方向无效。");
            direction = direction.GetNormal();
            var normal = Left(direction);
            var origin = points[0];
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxY = double.NegativeInfinity;
            foreach (var point in points)
            {
                var v = new Vector2d(point.X - origin.X, point.Y - origin.Y);
                var x = v.DotProduct(direction);
                var y = v.DotProduct(normal);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
            if (!new[] { minX, minY, maxX, maxY }.All(Finite) ||
                maxX - minX <= Eps || maxY - minY <= Eps)
                throw new InvalidOperationException("文字没有可用的二维范围。");

            var center = new Point2d(origin.X, origin.Y) +
                         direction * ((minX + maxX) * 0.5) +
                         normal * ((minY + maxY) * 0.5);
            var z = origin.Z;
            ValidateCurrentLayer(ctx);
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForWrite);
            if (!space.IsLayout) throw new InvalidOperationException("只支持模型或布局空间。");
            Entity frame;
            if (req.Shape == "Circle")
            {
                var r = Math.Sqrt(Math.Pow((maxX - minX) * 0.5, 2) +
                                  Math.Pow((maxY - minY) * 0.5, 2)) + req.Margin;
                frame = new Circle(Point(center, z), Vector3d.ZAxis, r);
            }
            else
            {
                var poly = new Polyline(4) { Elevation = z };
                var xs = new[] { minX - req.Margin, maxX + req.Margin };
                var ys = new[] { minY - req.Margin, maxY + req.Margin };
                var pts = new[] {
                    new Point2d(xs[0],ys[0]), new Point2d(xs[1],ys[0]),
                    new Point2d(xs[1],ys[1]), new Point2d(xs[0],ys[1])
                };
                for (var i = 0; i < pts.Length; i++)
                {
                    var wp = new Point2d(origin.X, origin.Y) +
                             direction * pts[i].X + normal * pts[i].Y;
                    poly.AddVertexAt(i, wp, 0, 0, 0);
                }
                poly.Closed = true;
                frame = poly;
            }
            var id = AddEntity(ctx, space, frame);
            return new FrameResult {
                source_handle = text.Handle.ToString(),
                created_handle = id, shape = req.Shape, margin = req.Margin
            };
        }
    }
}
