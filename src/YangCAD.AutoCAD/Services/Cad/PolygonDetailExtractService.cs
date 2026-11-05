using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Non-destructive polygon detail extraction. Shares PolygonBoundary2d with future
    /// delete-outside functionality, while leaving the validated rectangle path unchanged.
    /// Sources are read-only; all output entities are appended in one caller transaction.
    /// </summary>
    public static class PolygonDetailExtractService
    {
        private const double Eps = 1e-8;
        public sealed class Request
        {
            public IReadOnlyList<Point2d> Vertices { get; set; } = Array.Empty<Point2d>();
            public ObjectId BoundaryId { get; set; } = ObjectId.Null;
            public double BoundaryElevation { get; set; }
            public Point3d Destination { get; set; }
            public double Factor { get; set; } = 1;
            public bool DrawFrame { get; set; } = true;
            public bool KeepDimensionsAndLeaders { get; set; } = true;
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

        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool WorldXY(Vector3d normal) =>
            normal.Length > Eps && normal.GetNormal().DotProduct(Vector3d.ZAxis) > 0.999999;

        private static bool HasDimlfac(Dimension dim)
        {
            var p = dim.GetType().GetProperty("Dimlfac");
            return p != null && p.CanRead && p.CanWrite;
        }

        private static void AdjustDimension(Dimension dim, double factor)
        {
            var p = dim.GetType().GetProperty("Dimlfac");
            if (p == null || !p.CanRead || !p.CanWrite)
                throw new InvalidOperationException("此标注无法安全调整 Dimlfac。");
            var prior = Convert.ToDouble(p.GetValue(dim));
            if (!Finite(prior)) throw new InvalidOperationException("标注 Dimlfac 非有限数。");
            p.SetValue(dim, prior / factor);
        }

        private static void Warn(List<string> warnings, string message)
        {
            if (warnings.Count < 16) warnings.Add(message);
        }

        private static void AddClippedCurve(List<Entity> output, Entity part,
                                            PolygonBoundary2d boundary, Entity source)
        {
            if (part is Line line)
            {
                foreach (var fragment in boundary.ClipLine(line.StartPoint, line.EndPoint))
                {
                    var item = new Line(fragment.Start, fragment.End);
                    item.SetPropertiesFrom(source);
                    output.Add(item);
                }
            }
            else if (part is Arc arc && WorldXY(arc.Normal))
            {
                foreach (var interval in boundary.ClipArc(
                    arc.Center, arc.Radius, arc.StartAngle, arc.EndAngle, false))
                {
                    var item = new Arc(arc.Center, Vector3d.ZAxis, arc.Radius,
                                       interval.StartAngle, interval.EndAngle);
                    item.SetPropertiesFrom(source);
                    output.Add(item);
                }
            }
            else if (part is Circle circle && WorldXY(circle.Normal))
            {
                var intervals = boundary.ClipArc(circle.Center, circle.Radius, 0, Math.PI * 2, true);
                foreach (var interval in intervals)
                {
                    Entity item;
                    // A complete interior circle remains a Circle, never a 360-degree Arc.
                    if (interval.EndAngle - interval.StartAngle >= Math.PI * 2 - Eps)
                        item = (Entity)circle.Clone();
                    else
                        item = new Arc(circle.Center, Vector3d.ZAxis, circle.Radius,
                                       interval.StartAngle, interval.EndAngle);
                    item.SetPropertiesFrom(source);
                    output.Add(item);
                }
            }
            else
            {
                throw new NotSupportedException("此跨界对象无法精确裁切。");
            }
        }

        public static Result Extract(CadExecutionContext ctx, Request request, bool dryRun)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!Finite(request.Factor) || request.Factor <= 0 || request.Factor > 1000 ||
                !Finite(request.BoundaryElevation) ||
                !Finite(request.Destination.X) || !Finite(request.Destination.Y) ||
                !Finite(request.Destination.Z))
                throw new ArgumentException("大样比例、标高或插入点无效。");
            var boundary = new PolygonBoundary2d(request.Vertices);
            var tr = ctx.Transaction;
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForRead);
            if (!space.IsLayout) throw new InvalidOperationException("只支持当前模型空间或布局空间。");
            var origin = new Point3d(boundary.MinX, boundary.MinY, request.BoundaryElevation);
            var scale = Matrix3d.Scaling(request.Factor, origin);
            var move = Matrix3d.Displacement(request.Destination - origin);
            var result = new Result { success = true, dry_run = dryRun };
            var parts = new List<Entity>();
            var handles = new List<string>();
            var warnings = new List<string>();

            try
            {
                // Snapshot before append: never iterate a collection while adding new entities.
                var ids = space.Cast<ObjectId>().ToArray();
                if (ids.Length > 100000)
                    throw new InvalidOperationException("当前空间对象超过 10 万，请先缩小工作范围。");
                foreach (var id in ids)
                {
                    if (id == request.BoundaryId) continue;
                    var source = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (source == null || source.IsErased) continue;
                    Extents3d extents;
                    try { extents = source.GeometricExtents; }
                    catch { continue; }
                    if (!Finite(extents.MinPoint.X) || !Finite(extents.MinPoint.Y) ||
                        !Finite(extents.MaxPoint.X) || !Finite(extents.MaxPoint.Y) ||
                        !boundary.BoundsOverlap(extents.MinPoint.X, extents.MinPoint.Y,
                                                extents.MaxPoint.X, extents.MaxPoint.Y))
                        continue;
                    result.source_candidates++;

                    var layer = (LayerTableRecord)tr.GetObject(source.LayerId, OpenMode.ForRead);
                    if (layer.IsLocked || layer.IsFrozen || layer.IsOff)
                    {
                        result.skipped_sources++;
                        Warn(warnings, "锁定/冻结/关闭图层已跳过：" + source.Handle);
                        continue;
                    }
                    if (!request.KeepDimensionsAndLeaders &&
                        (source is Dimension || source is Leader || source is MLeader))
                    {
                        result.skipped_sources++;
                        continue;
                    }
                    if (source is RasterImage || source is UnderlayReference)
                    {
                        result.skipped_sources++;
                        Warn(warnings, "外部图片或底图引用未复制：" + source.Handle);
                        continue;
                    }
                    if (source is BlockReference block)
                    {
                        var def = tr.GetObject(block.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (def == null || def.IsFromExternalReference || def.IsFromOverlayReference ||
                            block.IsDynamicBlock || block.AttributeCollection.Count > 0)
                        {
                            result.skipped_sources++;
                            Warn(warnings, "外参/动态块/含属性图块安全跳过：" + source.Handle);
                            continue;
                        }
                    }
                    if (source is Hatch hatch && hatch.Associative)
                    {
                        result.skipped_sources++;
                        Warn(warnings, "关联填充需独立重建边界，已跳过：" + source.Handle);
                        continue;
                    }
                    if (source is Dimension dim && Math.Abs(request.Factor - 1) > Eps && !HasDimlfac(dim))
                    {
                        result.skipped_sources++;
                        Warn(warnings, "不能安全缩放的标注已跳过：" + source.Handle);
                        continue;
                    }

                    var completelyInside = boundary.ContainsBox(
                        extents.MinPoint.X, extents.MinPoint.Y,
                        extents.MaxPoint.X, extents.MaxPoint.Y);
                    var newParts = new List<Entity>();
                    try
                    {
                        if (completelyInside)
                        {
                            newParts.Add((Entity)source.Clone());
                        }
                        else if (source is Line || source is Arc || source is Circle)
                        {
                            AddClippedCurve(newParts, source, boundary, source);
                        }
                        else if (source is Polyline poly && WorldXY(poly.Normal) && !poly.HasWidth)
                        {
                            var exploded = new DBObjectCollection();
                            try
                            {
                                poly.Explode(exploded);
                                // All segments must be representable or the whole source is skipped.
                                if (exploded.Cast<DBObject>().Any(x =>
                                    !(x is Line) && !(x is Arc a && WorldXY(a.Normal))))
                                    throw new NotSupportedException("多段线包含不受支持的曲线段。");
                                foreach (DBObject db in exploded)
                                    AddClippedCurve(newParts, (Entity)db, boundary, source);
                            }
                            finally { foreach (DBObject db in exploded) db.Dispose(); }
                        }
                        else
                        {
                            result.skipped_sources++;
                            Warn(warnings, "复杂跨界对象已跳过：" + source.GetRXClass().DxfName +
                                           " / " + source.Handle);
                            continue;
                        }
                    }
                    catch (NotSupportedException)
                    {
                        foreach (var item in newParts) item.Dispose();
                        result.skipped_sources++;
                        Warn(warnings, "不能精确裁切的对象已跳过：" + source.Handle);
                        continue;
                    }
                    catch
                    {
                        foreach (var item in newParts) item.Dispose();
                        throw;
                    }
                    if (newParts.Count == 0) { result.skipped_sources++; continue; }
                    if (parts.Count + newParts.Count > 20000)
                    {
                        foreach (var item in newParts) item.Dispose();
                        throw new InvalidOperationException("提取结果超过 2 万图元，操作已取消。");
                    }
                    try
                    {
                        foreach (var item in newParts)
                        {
                            item.TransformBy(scale);
                            item.TransformBy(move);
                            if (item is Dimension d && Math.Abs(request.Factor - 1) > Eps)
                                AdjustDimension(d, request.Factor);
                        }
                    }
                    catch
                    {
                        foreach (var item in newParts) item.Dispose();
                        throw;
                    }
                    parts.AddRange(newParts);
                    if (!completelyInside) result.clipped_sources++;
                }
                result.copied_entities = parts.Count;
                if (parts.Count == 0)
                    throw new InvalidOperationException("边界内没有可安全提取的内容，未创建空白大样。");

                if (request.DrawFrame)
                {
                    var frame = new Polyline(boundary.Count) {
                        Closed = true, Elevation = request.BoundaryElevation
                    };
                    frame.SetDatabaseDefaults();
                    for (int i = 0; i < boundary.Count; i++)
                        frame.AddVertexAt(i, boundary.GetVertex(i), 0, 0, 0);
                    frame.TransformBy(scale);
                    frame.TransformBy(move);
                    parts.Add(frame);
                    result.frame_entities = 1;
                }

                if (!dryRun)
                {
                    space.UpgradeOpen();
                    foreach (var item in parts)
                    {
                        space.AppendEntity(item);
                        tr.AddNewlyCreatedDBObject(item, true);
                        handles.Add(item.Handle.ToString());
                    }
                }
                result.created_handles = dryRun ? Array.Empty<string>() : handles.ToArray();
                result.warnings = warnings.ToArray();
                return result;
            }
            finally
            {
                // The transaction takes ownership of appended entities.
                if (dryRun)
                    foreach (var p in parts) p.Dispose();
                else
                    foreach (var p in parts.Where(p => p.ObjectId.IsNull)) p.Dispose();
            }
        }
    }
}
