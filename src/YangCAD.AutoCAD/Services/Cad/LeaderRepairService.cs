using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Turns one unambiguous chain of exploded linework / an old LEADER into a native MLEADER.
    /// A standalone TEXT/MTEXT may be incorporated as the MLeader's own MText content.
    /// The service is shared by YGLEADERFIX and cad.leader.rebuild; it does not guess text matches.
    /// </summary>
    public static class LeaderRepairService
    {
        private const double Tolerance = 1e-5;

        public sealed class Request
        {
            public string[] SourceHandles { get; set; } = Array.Empty<string>();
            public string TextHandle { get; set; } = "";
            public Point3d? ArrowPoint { get; set; }
            public bool KeepOriginals { get; set; } = false;
        }

        public sealed class Result
        {
            public bool success { get; set; }
            public bool dry_run { get; set; }
            public string[] source_handles { get; set; } = Array.Empty<string>();
            public string text_handle { get; set; } = "";
            public string new_mleader_handle { get; set; } = "";
            public string[] removed_handles { get; set; } = Array.Empty<string>();
            public int vertex_count { get; set; }
            public bool text_associated { get; set; }
            public string warning { get; set; } = "";
            public string undo_semantics { get; set; } = "single_transaction_expected_confirm_in_autocad";
        }

        public static Result Rebuild(CadExecutionContext ctx, Request request, bool dryRun)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var handles = (request.SourceHandles ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (handles.Length == 0) throw new ArgumentException("请先选择引线或连续的引线线段。");
            if (handles.Length > 64) throw new ArgumentException("单次最多处理 64 个线段；请逐条重建，避免不同引线误拼接。");

            var tr = ctx.Transaction;
            var db = ctx.Database;
            var source = handles.Select(h => OpenEntity(db, tr, h)).ToArray();
            if (source.Any(e => e.OwnerId != source[0].OwnerId))
                throw new InvalidOperationException("引线线段必须位于同一个模型空间或布局空间。");
            var ownerRecord = (BlockTableRecord)tr.GetObject(source[0].OwnerId, OpenMode.ForRead);
            if (!ownerRecord.IsLayout)
                throw new InvalidOperationException("不能直接修改块定义/Xref 内部图元；请在模型或布局空间选择普通实体。");
            if (source.OfType<MLeader>().Any() && source.Length != 1)
                throw new InvalidOperationException("已有 MLeader 只能单独关联文字。");
            if (source.OfType<Leader>().Any() && source.Length != 1)
                throw new InvalidOperationException("旧式 Leader 只能单独转换，不能与炸开的线段混选。");
            if (source.OfType<Leader>().Any(leader => !leader.Annotation.IsNull))
                throw new InvalidOperationException("旧 Leader 已有关联注释。为避免留下孤立文字，本版本不覆盖其现有注释。");
            if (source.Any(e => e.IsErased)) throw new InvalidOperationException("选择集中有已删除实体。");

            var text = string.IsNullOrWhiteSpace(request.TextHandle)
                ? null : OpenEntity(db, tr, request.TextHandle);
            if (text != null && !(text is DBText) && !(text is MText))
                throw new ArgumentException("关联文字必须是 TEXT 或 MTEXT。多个独立 TEXT 请先合并成 MTEXT。");
            if (text != null && text.OwnerId != source[0].OwnerId)
                throw new InvalidOperationException("引线与文字必须位于同一个空间。");
            if (text is DBText dbText && string.IsNullOrWhiteSpace(dbText.TextString) ||
                text is MText mText && string.IsNullOrWhiteSpace(mText.Contents))
                throw new InvalidOperationException("关联文字内容为空，请选取有文字内容的 TEXT/MTEXT。");
            if (text != null && source.Any(s => s.ObjectId == text.ObjectId))
                throw new ArgumentException("文字和引线必须是不同实体。");

            MLeader existing = source.Length == 1 ? source[0] as MLeader : null;
            if (existing != null && existing.ContentType != ContentType.NoneContent)
                throw new InvalidOperationException("该 MLeader 已有内容，避免覆盖。请编辑原引线文字。");
            if (existing != null && text == null)
                throw new InvalidOperationException("这是已有的 MLeader，未选择文字，不需要重建。");
            if (existing == null && source.Any(e => !(e is Line) && !(e is Polyline) && !(e is Leader)))
                throw new InvalidOperationException("仅支持直线、直线型开放多段线和旧式 Leader；箭头三角形、块、曲线请单独处理。");

            Point3d[] chain = existing == null ? ExtractChain(source) : Array.Empty<Point3d>();
            if (existing == null)
            {
                var textLocation = text == null ? (Point3d?)null : TextPosition(text);
                chain = OrientArrowToTail(chain, textLocation, request.ArrowPoint);
                ValidateDetailLineChain(chain);
            }

            var result = new Result
            {
                success = true,
                dry_run = dryRun,
                source_handles = handles,
                text_handle = text?.Handle.ToString() ?? "",
                vertex_count = existing == null ? chain.Length : 0,
                text_associated = text != null,
                warning = text == null
                    ? "无文字 MLeader 可在 AutoCAD 中编辑，但当前 RVT Exchange v1 不保证把它重建为 Revit 原生引线。"
                    : "创建的 MLeader 继承当前多重引线样式；箭头/文字连接位置需在实机检查。"
            };
            if (dryRun) return result;

            // No source deletion until the complete new MLeader and optional text have been built.
            // If any step fails, the enclosing single transaction aborts; never silently keep half a result.
            MLeader target;
            if (existing != null)
            {
                existing.UpgradeOpen();
                target = existing;
            }
            else
            {
                var owner = (BlockTableRecord)tr.GetObject(source[0].OwnerId, OpenMode.ForWrite);
                target = new MLeader();
                target.SetDatabaseDefaults();
                target.MLeaderStyle = db.MLeaderstyle;
                target.LayerId = source[0].LayerId;
                target.Color = source[0].Color;
                target.LinetypeId = source[0].LinetypeId;
                owner.AppendEntity(target);
                tr.AddNewlyCreatedDBObject(target, true);
                target.ContentType = text == null ? ContentType.NoneContent : ContentType.MTextContent;
            }

            if (text != null)
            {
                using (var content = ConvertText(text))
                {
                    target.ContentType = ContentType.MTextContent;
                    target.MText = content;
                }
            }
            if (existing == null)
            {
                int group = target.AddLeader();
                int line = target.AddLeaderLine(group);
                target.AddFirstVertex(line, chain[0]);
                for (int i = 1; i < chain.Length; ++i)
                    target.AddLastVertex(line, chain[i]);
            }

            result.new_mleader_handle = target.Handle.ToString();
            var deleted = new List<string>();
            if (!request.KeepOriginals)
            {
                foreach (var item in source.Where(s => s.ObjectId != target.ObjectId))
                {
                    var originalHandle = item.Handle.ToString();
                    item.UpgradeOpen();
                    item.Erase();
                    deleted.Add(originalHandle);
                }
                if (text != null)
                {
                    var textHandle = text.Handle.ToString();
                    text.UpgradeOpen();
                    text.Erase();
                    deleted.Add(textHandle);
                }
            }
            result.removed_handles = deleted.ToArray();
            return result;
        }

        private static Entity OpenEntity(Database db, Transaction tr, string hexHandle)
        {
            if (!long.TryParse(hexHandle.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value) || value <= 0)
                throw new ArgumentException("无效的 DWG Handle：" + hexHandle);
            var id = db.GetObjectId(false, new Handle(value), 0);
            if (id.IsNull) throw new InvalidOperationException("找不到实体 Handle：" + hexHandle);
            return tr.GetObject(id, OpenMode.ForRead, false) as Entity
                ?? throw new InvalidOperationException("Handle 不是图形实体：" + hexHandle);
        }

        private static Point3d TextPosition(Entity text)
        {
            if (text is MText mt) return mt.Location;
            var t = (DBText)text;
            return t.Justify == AttachmentPoint.BaseLeft ? t.Position : t.AlignmentPoint;
        }

        private static MText ConvertText(Entity source)
        {
            var result = new MText();
            result.SetDatabaseDefaults();
            if (source is MText original)
            {
                result.Contents = original.Contents;
                result.Location = original.Location;
                result.TextHeight = original.TextHeight;
                result.TextStyleId = original.TextStyleId;
                result.Rotation = original.Rotation;
                result.Width = original.Width;
                result.Attachment = original.Attachment;
            }
            else
            {
                var originalText = (DBText)source;
                // DBText is literal text; MText interprets \, { and } as formatting commands.
                // Escaping prevents labels such as "A\B" and "{C}" from silently changing.
                result.Contents = EscapeDbTextForMText(originalText.TextString);
                result.Location = TextPosition(originalText);
                result.TextHeight = originalText.Height;
                result.TextStyleId = originalText.TextStyleId;
                result.Rotation = originalText.Rotation;
            }
            return result;
        }

        private static Point3d[] ExtractChain(Entity[] source)
        {
            if (source.Length == 1 && source[0] is Leader legacy)
            {
                if (legacy.NumVertices < 2 || legacy.NumVertices > 64)
                    throw new InvalidOperationException("旧式 Leader 顶点数量不支持。");
                return Enumerable.Range(0, legacy.NumVertices).Select(legacy.VertexAt).ToArray();
            }
            var pieces = new List<Point3d[]>();
            foreach (var entity in source)
            {
                if (entity is Line ln) pieces.Add(new[] { ln.StartPoint, ln.EndPoint });
                else if (entity is Polyline poly)
                {
                    if (poly.Closed || poly.NumberOfVertices < 2 || poly.NumberOfVertices > 64)
                        throw new InvalidOperationException("只支持非闭合、至少两个顶点的多段线。");
                    for (int i = 0; i < poly.NumberOfVertices - 1; i++)
                        if (Math.Abs(poly.GetBulgeAt(i)) > Tolerance)
                            throw new InvalidOperationException("含弧段 Bulge 的 Polyline 不能按直线引线重建。");
                    pieces.Add(Enumerable.Range(0, poly.NumberOfVertices).Select(poly.GetPoint3dAt).ToArray());
                }
            }
            if (pieces.Count != source.Length) throw new InvalidOperationException("包含不支持的线段。");
            if (pieces.Any(p => p.Length < 2 || p.Zip(p.Skip(1), (a,b) => a.DistanceTo(b)).Any(d => d < Tolerance)))
                throw new InvalidOperationException("引线包含零长度片段。");
            if (pieces.Count == 1) return pieces[0];

            // Strict topological chain; reject branched and disconnected selections.
            var ends = pieces.SelectMany((p, idx) => new[] { (Point: p[0], Piece: idx), (Point: p[p.Length - 1], Piece: idx) }).ToArray();
            var tips = ends.Where(e => ends.Count(other => e.Point.DistanceTo(other.Point) < Tolerance) == 1).ToArray();
            if (tips.Length != 2) throw new InvalidOperationException("线段无法构成一条单一路径；可能存在断点、交叉或分叉。");
            var used = new HashSet<int>();
            var path = new List<Point3d> { tips[0].Point };
            while (used.Count < pieces.Count)
            {
                var end = path.Last();
                var candidates = pieces.Select((p, i) => (P: p, Index: i))
                    .Where(e => !used.Contains(e.Index) &&
                        (e.P[0].DistanceTo(end) < Tolerance || e.P[e.P.Length - 1].DistanceTo(end) < Tolerance)).ToArray();
                if (candidates.Length != 1) throw new InvalidOperationException("连接处不是唯一后续线段，已安全终止。");
                var next = candidates[0];
                var forward = next.P[0].DistanceTo(end) < Tolerance;
                var nodes = forward ? next.P : next.P.Reverse().ToArray();
                path.AddRange(nodes.Skip(1));
                used.Add(next.Index);
            }
            if (path.Count > 65) throw new InvalidOperationException("引线顶点太多，请简化。");
            return path.ToArray();
        }

        private static string EscapeDbTextForMText(string raw)
        {
            return (raw ?? "").Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");
        }

        private static void ValidateDetailLineChain(Point3d[] points)
        {
            // V1 is a 2D detail-line repair tool. Do not silently flatten a 3D path.
            var z = points[0].Z;
            foreach (var point in points)
                if (double.IsNaN(point.X) || double.IsNaN(point.Y) || double.IsNaN(point.Z) ||
                    double.IsInfinity(point.X) || double.IsInfinity(point.Y) || double.IsInfinity(point.Z) ||
                    Math.Abs(point.Z - z) > Tolerance)
                    throw new InvalidOperationException("引线包含无效坐标或非同一平面的三维线段，请改用二维引线。");

            for (int i = 0; i < points.Length - 1; i++)
            {
                if (points[i].DistanceTo(points[i + 1]) < Tolerance)
                    throw new InvalidOperationException("引线存在零长度线段。");
                for (int j = i + 2; j < points.Length - 1; j++)
                    if (SegmentsIntersect2d(points[i], points[i + 1], points[j], points[j + 1]))
                        throw new InvalidOperationException("引线存在自交、回头重叠或闭合，不可安全恢复为单条引线。");
            }
        }

        private static bool SegmentsIntersect2d(Point3d a, Point3d b, Point3d c, Point3d d)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y;
            double sx = d.X - c.X, sy = d.Y - c.Y;
            double qx = c.X - a.X, qy = c.Y - a.Y;
            double cross = rx * sy - ry * sx;
            double qCrossR = qx * ry - qy * rx;
            if (Math.Abs(cross) < Tolerance)
            {
                if (Math.Abs(qCrossR) >= Tolerance) return false;
                // Collinear non-neighbour segments: any overlap (including repeated endpoints) is unsafe.
                return Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <=
                           Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + Tolerance &&
                       Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <=
                           Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + Tolerance;
            }
            double t = (qx * sy - qy * sx) / cross;
            double u = qCrossR / cross;
            return t >= -Tolerance && t <= 1 + Tolerance && u >= -Tolerance && u <= 1 + Tolerance;
        }

        private static Point3d[] OrientArrowToTail(Point3d[] points, Point3d? text, Point3d? arrow)
        {
            if (points.Length < 2) throw new InvalidOperationException("引线至少需要两个有效顶点。");
            if (arrow.HasValue)
                return points[0].DistanceTo(arrow.Value) <= points[points.Length - 1].DistanceTo(arrow.Value)
                    ? points : points.Reverse().ToArray();
            if (text.HasValue)
            {
                var dStart = points[0].DistanceTo(text.Value);
                var dEnd = points[points.Length - 1].DistanceTo(text.Value);
                if (Math.Abs(dStart - dEnd) < Tolerance)
                    throw new InvalidOperationException("文字位于引线两端的等距位置；请明确拾取箭头端点。");
                return dStart > dEnd ? points : points.Reverse().ToArray();
            }
            throw new InvalidOperationException("无文字引线必须指定箭头端点，以防把箭头生成在错误的一侧。");
        }
    }
}
