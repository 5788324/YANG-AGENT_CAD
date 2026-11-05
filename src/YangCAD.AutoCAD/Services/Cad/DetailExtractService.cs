using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Independent, non-destructive QG-style rectangular detail extraction.
    /// Only geometry that can be clipped exactly is trimmed. Cross-boundary annotations,
    /// blocks and hatches are SKIPPED rather than replaced by incorrect approximations.
    /// </summary>
    public static class DetailExtractService
    {
        private const double Tolerance = 1e-7;
        private const double TwoPi = Math.PI * 2;
        public sealed class Request
        {
            public Point3d FirstCorner { get; set; }
            public Point3d OppositeCorner { get; set; }
            public Point3d Destination { get; set; }
            public double Factor { get; set; } = 1;
            public bool KeepDimensionsAndLeaders { get; set; } = true;
            public bool DrawFrame { get; set; } = true;
        }
        public sealed class Result
        {
            public bool success { get; set; }
            public bool dry_run { get; set; }
            public int source_candidates { get; set; }
            public int copied_entities { get; set; }
            public int clipped_sources { get; set; }
            public int skipped_sources { get; set; }
            public int frame_entities { get; set; }
            public string[] warnings { get; set; } = Array.Empty<string>();
            public string[] created_handles { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "single_transaction_expected";
        }
        private sealed class Rect
        {
            public double L, B, R, T;
            public bool Contains(Point3d p) => p.X >= L - Tolerance && p.X <= R + Tolerance && p.Y >= B - Tolerance && p.Y <= T + Tolerance;
            public bool Contains(Extents3d e) => Contains(e.MinPoint) && Contains(e.MaxPoint);
            public bool Intersects(Extents3d e) => e.MaxPoint.X >= L - Tolerance && e.MinPoint.X <= R + Tolerance && e.MaxPoint.Y >= B - Tolerance && e.MinPoint.Y <= T + Tolerance;
        }

        public static Result Extract(CadExecutionContext ctx, Request request, bool dryRun)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!Finite(request.Factor) || request.Factor <= 0 || request.Factor > 1000)
                throw new ArgumentOutOfRangeException(nameof(request.Factor), "放大倍数必须在 0 到 1000 之间。");
            if (!Finite(request.FirstCorner.X) || !Finite(request.FirstCorner.Y) ||
                !Finite(request.OppositeCorner.X) || !Finite(request.OppositeCorner.Y) ||
                !Finite(request.Destination.X) || !Finite(request.Destination.Y) || !Finite(request.Destination.Z))
                throw new ArgumentException("裁切区域与插入点必须为有限坐标。");
            var box = new Rect
            {
                L = Math.Min(request.FirstCorner.X, request.OppositeCorner.X),
                R = Math.Max(request.FirstCorner.X, request.OppositeCorner.X),
                B = Math.Min(request.FirstCorner.Y, request.OppositeCorner.Y),
                T = Math.Max(request.FirstCorner.Y, request.OppositeCorner.Y)
            };
            if (box.R - box.L <= Tolerance || box.T - box.B <= Tolerance)
                throw new ArgumentException("裁切框宽度和高度必须大于 0。");
            var tr = ctx.Transaction;
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForRead);
            if (!space.IsLayout) throw new InvalidOperationException("只支持在模型空间或布局空间提取大样。");
            var result = new Result { success = true, dry_run = dryRun };
            var warnings = new List<string>();
            var handles = new List<string>();
            var parts = new List<Entity>();
            var distance = request.Destination - new Point3d(box.L, box.B, request.FirstCorner.Z);
            var scale = Matrix3d.Scaling(request.Factor, new Point3d(box.L, box.B, request.FirstCorner.Z));
            var move = Matrix3d.Displacement(distance);
            var total = 0;
            try
            {
                // Snapshot IDs before adding new objects, avoiding mutation of the live iterator.
                var ids = space.Cast<ObjectId>().ToArray();
                if (ids.Length > 100000) throw new InvalidOperationException("当前空间超过 10 万对象；请在较小的图纸区域操作。");
                foreach (var id in ids)
                {
                    var source = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (source == null || source.IsErased) continue;
                    Extents3d extents;
                    try { extents = source.GeometricExtents; }
                    catch { continue; }
                    if (!box.Intersects(extents)) continue;
                    total++;
                    if (!request.KeepDimensionsAndLeaders &&
                        (source is Dimension || source is Leader || source is MLeader))
                    { result.skipped_sources++; continue; }
                    // Do not carry an external reference, raster or PDF dependency into a
                    // supposedly self-contained extracted detail.
                    if (source is Autodesk.AutoCAD.DatabaseServices.RasterImage ||
                        source is Autodesk.AutoCAD.DatabaseServices.UnderlayReference)
                    { result.skipped_sources++; if (warnings.Count < 12) warnings.Add("外部图片/底图引用未复制：" + source.Handle); continue; }
                    if (source is BlockReference blockCheck)
                    {
                        var definition = tr.GetObject(blockCheck.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (definition == null || definition.IsFromExternalReference || definition.IsFromOverlayReference || blockCheck.IsDynamicBlock)
                        { result.skipped_sources++; if (warnings.Count < 12) warnings.Add("外参或动态块未复制：" + source.Handle); continue; }
                    }

                    var inside = box.Contains(extents);
                    var newParts = new List<Entity>();
                    if (inside)
                    {
                        // Shallow clone of an INSERT may omit AttributeReference objects. Defer
                        // attributed blocks to a later deep-clone implementation, never lose tags.
                        if (source is BlockReference blockRef && blockRef.AttributeCollection.Count > 0)
                        { result.skipped_sources++; if (warnings.Count < 12) warnings.Add("含属性图块需要深克隆，当前安全跳过：" + source.Handle); continue; }
                        // Associative hatch boundary reactors must not continue referencing
                        // the original detail after an independent copy is transformed.
                        if (source is Hatch hatch && hatch.Associative)
                        { result.skipped_sources++; if (warnings.Count < 12) warnings.Add("关联填充需独立边界重建，当前安全跳过：" + source.Handle); continue; }
                        // Do not silently scale annotative dimensions into incorrect measurements.
                        if (source is Dimension && Math.Abs(request.Factor - 1) > Tolerance && !HasDimlfac(source))
                        { result.skipped_sources++; if (warnings.Count < 12) warnings.Add("标注不支持 Dimlfac 读取，未放大：" + source.Handle); continue; }
                        newParts.Add((Entity)source.Clone());
                    }
                    else if (source is Line line)
                    {
                        var trimmed = TrimLine(line.StartPoint, line.EndPoint, box, line);
                        if (trimmed != null) newParts.Add(trimmed);
                    }
                    else if (source is Arc arc && IsWorldXY(arc.Normal))
                        newParts.AddRange(TrimArc(arc.Center, arc.Radius, arc.StartAngle, arc.EndAngle, box, arc));
                    else if (source is Circle circle && IsWorldXY(circle.Normal))
                        newParts.AddRange(TrimArc(circle.Center, circle.Radius, 0, TwoPi, box, circle));
                    else if (source is Polyline poly && IsWorldXY(poly.Normal) && !poly.HasWidth)
                    {
                        var exploded = new DBObjectCollection();
                        poly.Explode(exploded);
                        try
                        {
                            foreach (DBObject dbObject in exploded)
                            {
                                if (dbObject is Line seg)
                                {
                                    var trimmed = TrimLine(seg.StartPoint, seg.EndPoint, box, poly);
                                    if (trimmed != null) newParts.Add(trimmed);
                                }
                                else if (dbObject is Arc segArc)
                                    newParts.AddRange(TrimArc(segArc.Center, segArc.Radius, segArc.StartAngle, segArc.EndAngle, box, poly));
                                else
                                {
                                    // Refuse an incomplete extraction if a polyline segment cannot be represented.
                                    foreach (var part in newParts) part.Dispose();
                                    newParts.Clear();
                                    break;
                                }
                            }
                        }
                        finally { foreach (DBObject obj in exploded) obj.Dispose(); }
                    }
                    else
                    {
                        result.skipped_sources++;
                        if (warnings.Count < 12)
                            warnings.Add("边界相交但不能无损裁切，已跳过 " + source.GetRXClass().DxfName + " / " + source.Handle);
                        continue;
                    }
                    if (newParts.Count == 0)
                    { result.skipped_sources++; continue; }
                    if (!inside) result.clipped_sources++;
                    foreach (var part in newParts)
                    {
                        try
                        {
                            part.TransformBy(scale);
                            part.TransformBy(move);
                            if (part is Dimension dim && Math.Abs(request.Factor - 1) > Tolerance)
                            {
                                if (!FixDimlfac(dim, request.Factor))
                                    throw new InvalidOperationException("无法保持大样尺寸标注读数，已取消本次提取。");
                            }
                            parts.Add(part);
                        }
                        catch { part.Dispose(); throw; }
                    }
                }
                result.source_candidates = total;
                result.copied_entities = parts.Count;
                if (parts.Count == 0)
                    throw new InvalidOperationException("裁切范围内没有可完整或安全裁切的内容；未生成空白大样框。");
                if (request.DrawFrame)
                {
                    var frame = new Polyline(4) { Closed = true };
                    frame.SetDatabaseDefaults();
                    frame.AddVertexAt(0, new Point2d(box.L, box.B), 0, 0, 0);
                    frame.AddVertexAt(1, new Point2d(box.R, box.B), 0, 0, 0);
                    frame.AddVertexAt(2, new Point2d(box.R, box.T), 0, 0, 0);
                    frame.AddVertexAt(3, new Point2d(box.L, box.T), 0, 0, 0);
                    frame.TransformBy(scale);
                    frame.TransformBy(move);
                    parts.Add(frame);
                    result.frame_entities = 1;
                }
                if (parts.Count == 0) throw new InvalidOperationException("框选范围内没有可提取的对象。");
                if (!dryRun)
                {
                    space.UpgradeOpen();
                    foreach (var part in parts)
                    {
                        space.AppendEntity(part);
                        tr.AddNewlyCreatedDBObject(part, true);
                        handles.Add(part.Handle.ToString());
                    }
                }
                result.created_handles = dryRun ? Array.Empty<string>() : handles.ToArray();
                result.warnings = warnings.ToArray();
                return result;
            }
            finally
            {
                if (dryRun)
                    foreach (var part in parts) part.Dispose();
                // Real execution uses the owning transaction, which disposes newly-created objects.
                // If any exception occurs before appending, dispose unowned parts.
                else if (handles.Count != parts.Count)
                    foreach (var part in parts.Where(p => p.ObjectId.IsNull)) part.Dispose();
            }
        }

        private static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
        private static bool IsWorldXY(Vector3d normal) => normal.GetNormal().DotProduct(Vector3d.ZAxis) > 0.999999;
        private static Line? TrimLine(Point3d a, Point3d b, Rect rect, Entity source)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var t0 = 0.0; var t1 = 1.0;
            bool Step(double p, double q)
            {
                if (Math.Abs(p) < Tolerance) return q >= -Tolerance;
                var r = q / p;
                if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                else { if (r < t0) return false; if (r < t1) t1 = r; }
                return true;
            }
            if (!Step(-dx, a.X - rect.L) || !Step(dx, rect.R - a.X) ||
                !Step(-dy, a.Y - rect.B) || !Step(dy, rect.T - a.Y) || t1 - t0 <= Tolerance)
                return null;
            var start = a + (b - a) * t0;
            var end = a + (b - a) * t1;
            var item = new Line(start, end);
            item.SetPropertiesFrom(source);
            return item;
        }
        private static IEnumerable<Arc> TrimArc(Point3d center, double radius, double start, double end, Rect rect, Entity source)
        {
            if (radius <= Tolerance) yield break;
            var span = end - start;
            while (span <= 0) span += TwoPi;
            if (span > TwoPi + Tolerance) span = TwoPi;
            var cuts = new List<double> { 0, span };
            void AddCut(double angle)
            {
                var delta = Positive(angle - start);
                if (delta > Tolerance && delta < span - Tolerance) cuts.Add(delta);
            }
            foreach (var x in new[] { rect.L, rect.R })
            {
                var u = (x - center.X) / radius;
                if (Math.Abs(u) > 1) continue;
                var angle = Math.Acos(Math.Max(-1, Math.Min(1, u)));
                AddCut(angle); AddCut(-angle);
            }
            foreach (var y in new[] { rect.B, rect.T })
            {
                var v = (y - center.Y) / radius;
                if (Math.Abs(v) > 1) continue;
                var angle = Math.Asin(Math.Max(-1, Math.Min(1, v)));
                AddCut(angle); AddCut(Math.PI - angle);
            }
            cuts.Sort();
            for (var i = 0; i + 1 < cuts.Count; i++)
            {
                double from = cuts[i], to = cuts[i + 1];
                if (to - from < Tolerance) continue;
                var middle = start + (from + to) / 2;
                if (!rect.Contains(new Point3d(center.X + radius * Math.Cos(middle), center.Y + radius * Math.Sin(middle), center.Z)))
                    continue;
                var part = new Arc(center, Vector3d.ZAxis, radius, start + from, start + to);
                part.SetPropertiesFrom(source);
                yield return part;
            }
        }
        private static double Positive(double a) { a %= TwoPi; return a < 0 ? a + TwoPi : a; }
        private static bool HasDimlfac(Entity source) => source.GetType().GetProperty("Dimlfac")?.CanRead == true;
        private static bool FixDimlfac(Dimension dimension, double factor)
        {
            var property = dimension.GetType().GetProperty("Dimlfac");
            if (property == null || !property.CanRead || !property.CanWrite) return false;
            var prior = Convert.ToDouble(property.GetValue(dimension));
            if (!Finite(prior)) return false;
            property.SetValue(dimension, prior / factor);
            return true;
        }
    }
}
