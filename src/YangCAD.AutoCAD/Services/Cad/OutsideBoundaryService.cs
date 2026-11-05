using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// YGOUTSIDE V1: safely erase fully-outside entities and replace supported
    /// crossing curves with their exact inside fragments. CurrentSpace only.
    /// Preview/execute share the same classifier; all writes use caller transaction.
    /// </summary>
    public static class OutsideBoundaryService
    {
        private const double Eps = 1e-7;
        private const int MaxObjects = 100000;
        private const int MaxChanges = 10000;
        private const int MaxNewParts = 20000;

        public sealed class Request
        {
            public IReadOnlyList<Point2d> Vertices { get; set; } = Array.Empty<Point2d>();
            public ObjectId BoundaryId { get; set; } = ObjectId.Null;
        }

        public sealed class Result
        {
            public bool success { get; set; }
            public bool preview { get; set; }
            public int scanned { get; set; }
            public int erased { get; set; }
            public int trimmed { get; set; }
            public int new_fragments { get; set; }
            public int skipped { get; set; }
            public string fingerprint { get; set; } = "";
            public string[] warnings { get; set; } = Array.Empty<string>();
            public string[] changed_handles { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "native_undo_must_be_tested";
        }

        private enum Kind { Erase, Replace }

        private sealed class Change
        {
            public ObjectId Id;
            public Kind Action;
            public List<Entity> Parts = new List<Entity>();
        }

        private sealed class Plan : IDisposable
        {
            public Result Summary = new Result();
            public readonly List<Change> Changes = new List<Change>();

            public void Dispose()
            {
                foreach (var change in Changes)
                    foreach (var item in change.Parts)
                        if (item.ObjectId.IsNull) item.Dispose();
            }
        }

        private static bool WorldXY(Vector3d normal) =>
            normal.Length > Eps && normal.GetNormal().DotProduct(Vector3d.ZAxis) > 0.999999;
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static string N(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        private static void Warn(List<string> warnings, string text)
        {
            if (warnings.Count < 16) warnings.Add(text);
        }

        private static bool OnSegment(Point2d a, Point2d b, Point2d p)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var area = dx * (p.Y - a.Y) - dy * (p.X - a.X);
            return Math.Abs(area) <= Eps * Math.Max(1, Math.Sqrt(dx * dx + dy * dy)) &&
                   p.X >= Math.Min(a.X, b.X) - Eps && p.X <= Math.Max(a.X, b.X) + Eps &&
                   p.Y >= Math.Min(a.Y, b.Y) - Eps && p.Y <= Math.Max(a.Y, b.Y) + Eps;
        }

        private static bool EdgesTouch(Point2d a, Point2d b, Point2d c, Point2d d)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var sx = d.X - c.X; var sy = d.Y - c.Y;
            var p = dx * (c.Y - a.Y) - dy * (c.X - a.X);
            var q = dx * (d.Y - a.Y) - dy * (d.X - a.X);
            var r = sx * (a.Y - c.Y) - sy * (a.X - c.X);
            var s = sx * (b.Y - c.Y) - sy * (b.X - c.X);
            if (((p > Eps && q < -Eps) || (p < -Eps && q > Eps)) &&
                ((r > Eps && s < -Eps) || (r < -Eps && s > Eps))) return true;
            return OnSegment(a, b, c) || OnSegment(a, b, d) ||
                   OnSegment(c, d, a) || OnSegment(c, d, b);
        }

        /// <summary>
        /// AABB definitely disjoint from a polygon (including its boundary).
        /// Negative => possibly intersects, so a complex object must be skipped.
        /// </summary>
        private static bool DefinitelyOutside(PolygonBoundary2d boundary, Extents3d box)
        {
            var left = box.MinPoint.X; var bottom = box.MinPoint.Y;
            var right = box.MaxPoint.X; var top = box.MaxPoint.Y;
            if (!boundary.BoundsOverlap(left, bottom, right, top)) return true;
            var corners = new[] {
                new Point2d(left, bottom), new Point2d(right, bottom),
                new Point2d(right, top), new Point2d(left, top)
            };
            if (corners.Any(boundary.Contains)) return false;
            for (var i = 0; i < boundary.Count; i++)
            {
                var a = boundary.GetVertex(i);
                var b = boundary.GetVertex((i + 1) % boundary.Count);
                if (a.X >= left - Eps && a.X <= right + Eps &&
                    a.Y >= bottom - Eps && a.Y <= top + Eps)
                    return false;
                for (var k = 0; k < 4; k++)
                    if (EdgesTouch(a, b, corners[k], corners[(k + 1) % 4]))
                        return false;
            }
            return true;
        }

        private static bool SamePoint(Point3d a, Point3d b) =>
            a.DistanceTo(b) <= Eps;

        private static List<Entity>? Clip(Entity source, PolygonBoundary2d boundary)
        {
            var output = new List<Entity>();
            var transferred = false;
            try
            {
                if (source is Line line)
                {
                    foreach (var segment in boundary.ClipLine(line.StartPoint, line.EndPoint))
                    {
                        var item = new Line(segment.Start, segment.End);
                        item.SetPropertiesFrom(source);
                        output.Add(item);
                    }
                }
                else if (source is Arc arc && WorldXY(arc.Normal))
                {
                    foreach (var a in boundary.ClipArc(arc.Center, arc.Radius,
                                    arc.StartAngle, arc.EndAngle, false))
                    {
                        var item = new Arc(arc.Center, Vector3d.ZAxis, arc.Radius,
                                           a.StartAngle, a.EndAngle);
                        item.SetPropertiesFrom(source);
                        output.Add(item);
                    }
                }
                else if (source is Circle circle && WorldXY(circle.Normal))
                {
                    foreach (var a in boundary.ClipArc(circle.Center, circle.Radius,
                                    0, Math.PI * 2, true))
                    {
                        Entity item;
                        if (a.EndAngle - a.StartAngle >= Math.PI * 2 - Eps)
                            item = (Entity)circle.Clone();
                        else
                            item = new Arc(circle.Center, Vector3d.ZAxis, circle.Radius,
                                           a.StartAngle, a.EndAngle);
                        item.SetPropertiesFrom(source);
                        output.Add(item);
                    }
                }
                else if (source is Polyline poly && WorldXY(poly.Normal) && !poly.HasWidth)
                {
                    var exploded = new DBObjectCollection();
                    try
                    {
                        poly.Explode(exploded);
                        if (exploded.Cast<DBObject>().Any(obj =>
                            !(obj is Line) && !(obj is Arc a && WorldXY(a.Normal))))
                            return null;
                        foreach (DBObject obj in exploded)
                        {
                            var fragments = Clip((Entity)obj, boundary);
                            if (fragments == null) return null;
                            output.AddRange(fragments);
                        }
                    }
                    finally { foreach (DBObject obj in exploded) obj.Dispose(); }
                }
                else
                    return null;
                transferred = true;
                return output;
            }
            finally
            {
                if (!transferred)
                    foreach (var item in output) item.Dispose();
            }
        }

        private static bool IsSameCurve(Entity source, IReadOnlyList<Entity> parts)
        {
            if (parts.Count != 1) return false;
            if (source is Line line && parts[0] is Line kept)
                return SamePoint(line.StartPoint, kept.StartPoint) &&
                       SamePoint(line.EndPoint, kept.EndPoint);
            if (source is Arc arc && parts[0] is Arc keptArc)
                return Math.Abs(arc.StartAngle - keptArc.StartAngle) < Eps &&
                       Math.Abs(arc.EndAngle - keptArc.EndAngle) < Eps;
            return source is Circle && parts[0] is Circle;
        }

        private static bool Protected(Entity source, Transaction tr)
        {
            if (source is Viewport || source is RasterImage ||
                source is UnderlayReference || source is Hatch hatch && hatch.Associative)
                return true;
            if (source is BlockReference block)
            {
                var def = tr.GetObject(block.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (def == null || def.IsFromExternalReference || def.IsFromOverlayReference ||
                    block.IsDynamicBlock || block.AttributeCollection.Count > 0)
                    return true;
            }
            return false;
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static Plan MakePlan(CadExecutionContext ctx, Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var boundary = new PolygonBoundary2d(request.Vertices);
            var tr = ctx.Transaction;
            var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForRead);
            if (!space.IsLayout)
                throw new InvalidOperationException("只支持当前模型空间或布局空间。");
            if (!request.BoundaryId.IsNull)
            {
                var bound = tr.GetObject(request.BoundaryId, OpenMode.ForRead, false) as Polyline;
                if (bound == null || bound.OwnerId != ctx.Database.CurrentSpaceId ||
                    !bound.Closed || bound.NumberOfVertices != boundary.Count ||
                    bound.HasWidth || !WorldXY(bound.Normal))
                    throw new InvalidOperationException("边界对象已变化，请重新选择。");
                for (int i = 0; i < boundary.Count; i++)
                {
                    if (Math.Abs(bound.GetBulgeAt(i)) > Eps ||
                        bound.GetPoint2dAt(i).GetDistanceTo(boundary.GetVertex(i)) > Eps)
                        throw new InvalidOperationException("预览边界已变化，请重新选择。");
                }
            }

            var plan = new Plan();
            var warnings = new List<string>();
            var digest = new StringBuilder();
            try
            {
                digest.Append(ctx.Database.CurrentSpaceId.ToString()).Append('|');
                for (var j = 0; j < boundary.Count; j++)
                    digest.Append(N(boundary.GetVertex(j).X)).Append(',')
                          .Append(N(boundary.GetVertex(j).Y)).Append(';');
                var ids = space.Cast<ObjectId>().ToArray();
                if (ids.Length > MaxObjects)
                    throw new InvalidOperationException("当前空间超过 10 万个图元，请分区处理。");
                foreach (var id in ids)
                {
                    if (id == request.BoundaryId) continue; // the boundary is never erased
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null || entity.IsErased) continue;
                    plan.Summary.scanned++;
                    var layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
                    if (layer.IsLocked || layer.IsFrozen || layer.IsOff || Protected(entity, tr))
                    {
                        plan.Summary.skipped++;
                        Warn(warnings, "受保护对象已跳过：" + entity.Handle);
                        continue;
                    }
                    Extents3d box;
                    try { box = entity.GeometricExtents; }
                    catch
                    {
                        plan.Summary.skipped++;
                        Warn(warnings, "无法读取几何范围：" + entity.Handle);
                        continue;
                    }
                    if (!Finite(box.MinPoint.X) || !Finite(box.MinPoint.Y) ||
                        !Finite(box.MaxPoint.X) || !Finite(box.MaxPoint.Y))
                    {
                        plan.Summary.skipped++;
                        continue;
                    }

                    // Completely internal objects never change, even blocks or annotations.
                    if (boundary.ContainsBox(box.MinPoint.X, box.MinPoint.Y,
                                              box.MaxPoint.X, box.MaxPoint.Y))
                        continue;

                    Change? change = null;
                    if (DefinitelyOutside(boundary, box))
                        change = new Change { Id = id, Action = Kind.Erase };
                    else
                    {
                        var parts = Clip(entity, boundary);
                        if (parts == null)
                        {
                            plan.Summary.skipped++;
                            Warn(warnings, "跨界复杂对象不能可靠裁切：" + entity.Handle);
                            continue;
                        }
                        if (IsSameCurve(entity, parts))
                        {
                            foreach (var p in parts) p.Dispose();
                            continue;
                        }
                        change = new Change {
                            Id = id, Action = parts.Count == 0 ? Kind.Erase : Kind.Replace,
                            Parts = parts
                        };
                    }
                    // The plan owns every transient fragment until appended.
                    plan.Changes.Add(change);
                    if (change.Action == Kind.Erase) plan.Summary.erased++;
                    else
                    {
                        plan.Summary.trimmed++;
                        plan.Summary.new_fragments += change.Parts.Count;
                    }
                    if (plan.Changes.Count > MaxChanges ||
                        plan.Summary.new_fragments > MaxNewParts)
                        throw new InvalidOperationException("预期影响对象过多，请缩小区域再操作。");
                    digest.Append(entity.Handle.ToString()).Append(':')
                          .Append(change.Action).Append(':')
                          .Append(N(box.MinPoint.X)).Append(',')
                          .Append(N(box.MinPoint.Y)).Append(',')
                          .Append(N(box.MaxPoint.X)).Append(',')
                          .Append(N(box.MaxPoint.Y)).Append(':')
                          .Append(change.Parts.Count);
                    foreach (var part in change.Parts)
                    {
                        if (part is Line seg)
                        {
                            digest.Append(":L:").Append(N(seg.StartPoint.X)).Append(',')
                                  .Append(N(seg.StartPoint.Y)).Append(',')
                                  .Append(N(seg.StartPoint.Z)).Append(',')
                                  .Append(N(seg.EndPoint.X)).Append(',')
                                  .Append(N(seg.EndPoint.Y)).Append(',')
                                  .Append(N(seg.EndPoint.Z));
                        }
                        else if (part is Arc arcPart)
                        {
                            digest.Append(":A:").Append(N(arcPart.Center.X)).Append(',')
                                  .Append(N(arcPart.Center.Y)).Append(',')
                                  .Append(N(arcPart.Radius)).Append(',')
                                  .Append(N(arcPart.StartAngle)).Append(',')
                                  .Append(N(arcPart.EndAngle));
                        }
                        else if (part is Circle circlePart)
                            digest.Append(":C:").Append(N(circlePart.Center.X)).Append(',')
                                  .Append(N(circlePart.Center.Y)).Append(',')
                                  .Append(N(circlePart.Radius));
                    }
                    digest.Append(';');
                }
                plan.Summary.warnings = warnings.ToArray();
                plan.Summary.fingerprint = Hash(digest.ToString());
                plan.Summary.success = true;
                return plan;
            }
            catch
            {
                plan.Dispose();
                throw;
            }
        }

        public static Result Preview(CadExecutionContext ctx, Request request)
        {
            using (var plan = MakePlan(ctx, request))
            {
                plan.Summary.preview = true;
                return plan.Summary;
            }
        }

        public static Result Execute(CadExecutionContext ctx, Request request, string previewFingerprint)
        {
            if (string.IsNullOrWhiteSpace(previewFingerprint))
                throw new InvalidOperationException("必须先执行只读预览。");
            using (var plan = MakePlan(ctx, request))
            {
                if (!string.Equals(plan.Summary.fingerprint, previewFingerprint,
                                   StringComparison.Ordinal))
                    throw new InvalidOperationException("预览后图元或边界发生变化，请重新预览。");
                if (plan.Changes.Count == 0)
                    throw new InvalidOperationException("没有需要删除或修剪的图元。");
                var tr = ctx.Transaction;
                var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForWrite);
                var modifiedHandles = new List<string>();
                foreach (var change in plan.Changes)
                {
                    var entity = (Entity)tr.GetObject(change.Id, OpenMode.ForWrite, false);
                    modifiedHandles.Add(entity.Handle.ToString());
                    foreach (var part in change.Parts)
                    {
                        space.AppendEntity(part);
                        tr.AddNewlyCreatedDBObject(part, true);
                    }
                    entity.Erase();
                }
                plan.Summary.changed_handles = modifiedHandles.ToArray();
                plan.Summary.preview = false;
                return plan.Summary;
            }
        }
    }
}
