using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class PolylineCleanupService
    {
        public static object Clean(
            Database db,
            Transaction tr,
            string handle,
            double tolerance,
            bool removeDuplicates,
            bool removeCollinear,
            bool dryRun)
        {
            if (!removeDuplicates && !removeCollinear)
                throw new ArgumentException("remove_duplicates / remove_collinear 至少启用一项。");

            var p = PolylineInspectionService.PolylineFromHandle(db, tr, handle, OpenMode.ForWrite);
            var before = PolylineInspectionService.InspectPolyline(p, tolerance);

            if (p.HasBulges)
                throw new InvalidOperationException("cad.polyline.clean 当前拒绝包含任何非零 bulge 的 Polyline；请使用专用几何 Tool。");
            if (PolylineInspectionService.HasNonZeroSegmentWidths(p))
                throw new InvalidOperationException("cad.polyline.clean 当前拒绝带任何非零 segment width 的 Polyline；避免改变宽度语义。");

            var removedDuplicate = new List<int>();
            var removedCollinear = new List<int>();
            var minVertices = p.Closed ? 3 : 2;

            if (removeDuplicates)
            {
                for (var i = p.NumberOfVertices - 1; i >= 1; i--)
                {
                    if (p.NumberOfVertices <= minVertices) break;
                    if (!PolylineInspectionService.Near(p.GetPoint2dAt(i - 1), p.GetPoint2dAt(i), tolerance)) continue;
                    removedDuplicate.Add(i);
                    p.RemoveVertexAt(i);
                }
                if (p.Closed && p.NumberOfVertices > minVertices &&
                    PolylineInspectionService.Near(p.GetPoint2dAt(p.NumberOfVertices - 1), p.GetPoint2dAt(0), tolerance))
                {
                    var index = p.NumberOfVertices - 1;
                    removedDuplicate.Add(index);
                    p.RemoveVertexAt(index);
                }
            }

            if (removeCollinear && p.NumberOfVertices > minVertices)
            {
                var n = p.NumberOfVertices;
                var start = p.Closed ? n - 1 : n - 2;
                var stop = p.Closed ? 0 : 1;
                for (var i = start; i >= stop && p.NumberOfVertices > minVertices; i--)
                {
                    if (i >= p.NumberOfVertices) continue;
                    var currentN = p.NumberOfVertices;
                    var prev = (i - 1 + currentN) % currentN;
                    var next = (i + 1) % currentN;
                    if (prev == i || next == i || prev == next) continue;
                    var a = p.GetPoint2dAt(prev);
                    var b = p.GetPoint2dAt(i);
                    var c = p.GetPoint2dAt(next);
                    if (PolylineInspectionService.Near(a, b, tolerance) || PolylineInspectionService.Near(b, c, tolerance)) continue;
                    if (!PolylineInspectionService.CollinearAndBetween(a, b, c, tolerance)) continue;
                    removedCollinear.Add(i);
                    p.RemoveVertexAt(i);
                }
            }

            if (p.NumberOfVertices < minVertices)
                throw new InvalidOperationException("清理后顶点数量不足，操作已拒绝。");

            var after = PolylineInspectionService.InspectPolyline(p, tolerance);
            return new
            {
                dry_run = dryRun,
                changed = removedDuplicate.Count + removedCollinear.Count > 0,
                removed_duplicate_indices = removedDuplicate.OrderBy(i => i).ToArray(),
                removed_collinear_indices = removedCollinear.OrderBy(i => i).ToArray(),
                before,
                after
            };
        }
    }
}
