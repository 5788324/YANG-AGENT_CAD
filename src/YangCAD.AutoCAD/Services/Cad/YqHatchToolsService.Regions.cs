using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static partial class YqHatchToolsService
    {
        // ============================================================
        // 环提取、统一 CCW 定向、局部坐标对齐与 Region 布尔重构
        // ============================================================
        private static bool TryExtractLoopHierarchy(
            Hatch hatch,
            Point2d localOrigin,
            out List<LoopRecord> records,
            out List<(int OuterIndex, List<int> HoleIndices)> islands)
        {
            records = new List<LoopRecord>();
            islands = new List<(int OuterIndex, List<int> HoleIndices)>();
            var shift = -localOrigin.GetAsVector();

            for (var i = 0; i < hatch.NumberOfLoops; i++)
            {
                var loop = hatch.GetLoopAt(i);
                var poly = ConvertLoopToPolyline(hatch, loop, shift);
                if (poly == null) continue;

                var reg = CreateRegionFromCurve(poly);
                if (reg == null || reg.Area < AreaTol)
                {
                    try { reg?.Dispose(); } catch { }
                    poly.Dispose();
                    continue;
                }

                records.Add(new LoopRecord(poly)
                {
                    Region = reg,
                    Area = reg.Area,
                    Extents = poly.GeometricExtents
                });
            }

            if (records.Count == 0) return false;

            records.Sort((a, b) => b.Area.CompareTo(a.Area));

            for (var i = 0; i < records.Count; i++)
            {
                var containers = new List<int>();
                for (var j = 0; j < i; j++)
                {
                    if (records[j].Area <= records[i].Area * 1.0001) continue;
                    if (!ExtentsContains2d(records[j].Extents, records[i].Extents, 1e-2)) continue;
                    if (IsRegionContainedIn(records[i].Region!, records[j].Region!, records[i].Area))
                    {
                        containers.Add(j);
                    }
                }
                records[i].Depth = containers.Count;
                if (containers.Count > 0)
                {
                    records[i].ParentIndex = containers[containers.Count - 1];
                }
            }

            var islandMap = new Dictionary<int, List<int>>();
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Depth % 2 == 0)
                {
                    islandMap[i] = new List<int>();
                }
                else
                {
                    var p = records[i].ParentIndex;
                    if (p >= 0 && islandMap.TryGetValue(p, out var holes))
                    {
                        holes.Add(i);
                    }
                }
            }

            foreach (var kv in islandMap)
            {
                islands.Add((kv.Key, kv.Value));
            }
            return islands.Count > 0;
        }

        private static bool TryBuildHatchCompositeRegion(Hatch hatch, Point2d localOrigin, out Region? composite,
            bool requireAllLoops = false)
        {
            composite = null;
            if (!TryExtractLoopHierarchy(hatch, localOrigin, out var records, out var islands))
                return false;

            using (DisposeList(records))
            {
                if (requireAllLoops && records.Count != hatch.NumberOfLoops)
                    return false;

                Region? total = null;
                try
                {
                    foreach (var (outerIdx, holeIndices) in islands)
                    {
                        using var shell = (Region)records[outerIdx].Region!.Clone();
                        foreach (var hi in holeIndices)
                        {
                            using var hole = (Region)records[hi].Region!.Clone();
                            shell.BooleanOperation(BooleanOperationType.BoolSubtract, hole);
                        }

                        if (shell.Area < AreaTol) continue;

                        if (total == null)
                        {
                            total = (Region)shell.Clone();
                        }
                        else
                        {
                            total.BooleanOperation(BooleanOperationType.BoolUnite, shell);
                        }
                    }

                    composite = total;
                    return composite != null && composite.Area >= AreaTol;
                }
                catch
                {
                    try { total?.Dispose(); } catch { }
                    return false;
                }
            }
        }

        private static bool TryPopulateHatchFromRegion(
            Transaction tr,
            BlockTableRecord space,
            Hatch template,
            Region region,
            Point2d localOrigin,
            bool groupIntoSingleHatch,
            Hatch? updateInPlace,
            out List<Hatch> resultingHatches,
            bool requireCompleteResult = false, bool attachToSpace = true)
        {
            resultingHatches = new List<Hatch>();
            var loopPolys = ExplodeRegionToClosedPolylines(region, template.Normal, template.Elevation);
            if (loopPolys.Count == 0) return false;

            var records = new List<LoopRecord>();
            var shiftBack = localOrigin.GetAsVector();

            try
            {
                foreach (var poly in loopPolys)
                {
                    var reg = CreateRegionFromCurve(poly);
                    if (reg == null || reg.Area < AreaTol)
                    {
                        try { reg?.Dispose(); } catch { }
                        poly.Dispose();
                        continue;
                    }
                    records.Add(new LoopRecord(poly)
                    {
                        Region = reg,
                        Area = reg.Area,
                        Extents = poly.GeometricExtents
                    });
                }

                if (records.Count == 0 || (requireCompleteResult && records.Count != loopPolys.Count)) return false;

                records.Sort((a, b) => b.Area.CompareTo(a.Area));
                for (var i = 0; i < records.Count; i++)
                {
                    var containers = new List<int>();
                    for (var j = 0; j < i; j++)
                    {
                        if (records[j].Area <= records[i].Area * 1.0001) continue;
                        if (!ExtentsContains2d(records[j].Extents, records[i].Extents, 1e-2)) continue;
                        if (IsRegionContainedIn(records[i].Region!, records[j].Region!, records[i].Area))
                        {
                            containers.Add(j);
                        }
                    }
                    records[i].Depth = containers.Count;
                    if (containers.Count > 0) records[i].ParentIndex = containers[containers.Count - 1];
                }

                var islands = new Dictionary<int, List<int>>();
                for (var i = 0; i < records.Count; i++)
                {
                    if (records[i].Depth % 2 == 0)
                    {
                        islands[i] = new List<int>();
                    }
                    else
                    {
                        var p = records[i].ParentIndex;
                        if (p >= 0 && islands.TryGetValue(p, out var holes)) holes.Add(i);
                    }
                }

                if (islands.Count == 0) return false;
                if (attachToSpace) FilterSliverIslands(records, islands);
                if (islands.Count == 0) return false;

                if (groupIntoSingleHatch)
                {
                    var target = updateInPlace ?? CreateEmptyClone(template);
                    if (updateInPlace != null)
                    {
                        target.Associative = false;
                        try { target.RemoveAssociatedObjectIds(); } catch { }
                        while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);
                    }

                    try
                    {
                        foreach (var kv in islands)
                        {
                            if (!AppendPolylineAsLoop(target, records[kv.Key].Polyline, HatchLoopTypes.External, shiftBack) && requireCompleteResult)
                                throw new InvalidOperationException("无法重建填充外边界。");
                            foreach (var hi in kv.Value)
                            {
                                if (!AppendPolylineAsLoop(target, records[hi].Polyline, HatchLoopTypes.Default, shiftBack) && requireCompleteResult)
                                    throw new InvalidOperationException("无法重建填充内边界。");
                            }
                        }

                        try { target.EvaluateHatch(false); } catch when (!requireCompleteResult) { }

                        if (updateInPlace == null && attachToSpace)
                        {
                            space.AppendEntity(target);
                            tr.AddNewlyCreatedDBObject(target, true);
                        }
                    }
                    catch
                    {
                        if (updateInPlace == null && target.ObjectId.IsNull) target.Dispose();
                        throw;
                    }
                    resultingHatches.Add(target);
                    return true;
                }

                var idx = 0;
                foreach (var kv in islands)
                {
                    var useExisting = updateInPlace != null && idx == 0;
                    var target = useExisting ? updateInPlace! : CreateEmptyClone(template);
                    if (useExisting)
                    {
                        target.Associative = false;
                        try { target.RemoveAssociatedObjectIds(); } catch { }
                        while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);
                    }

                    if (!AppendPolylineAsLoop(target, records[kv.Key].Polyline, HatchLoopTypes.External, shiftBack) && requireCompleteResult)
                        throw new InvalidOperationException("无法重建填充外边界。");
                    foreach (var hi in kv.Value)
                    {
                        if (!AppendPolylineAsLoop(target, records[hi].Polyline, HatchLoopTypes.Default, shiftBack) && requireCompleteResult)
                            throw new InvalidOperationException("无法重建填充内边界。");
                    }

                    try { target.EvaluateHatch(false); } catch when (!requireCompleteResult) { }

                    if (!useExisting)
                    {
                        space.AppendEntity(target);
                        tr.AddNewlyCreatedDBObject(target, true);
                    }
                    resultingHatches.Add(target);
                    idx++;
                }

                return resultingHatches.Count > 0;
            }
            finally
            {
                foreach (var r in records) r.Dispose();
            }
        }

        private static bool IsSliverGeometry(double area, double perimeter, double maxArea, double maxThickness)
        {
            if (perimeter <= 1e-8) return true;
            var thickness = 2.0 * area / perimeter;
            return thickness < 0.25 && thickness < maxThickness * 0.01 && area < maxArea * 0.005;
        }

        private static void FilterSliverIslands(List<LoopRecord> records, Dictionary<int, List<int>> islands)
        {
            if (islands.Count <= 1) return;
            var metrics = new List<(int Key, double Area, double Perim)>(islands.Count);
            var maxArea = 0.0;
            var maxThick = 0.0;
            foreach (var key in islands.Keys)
            {
                var area = records[key].Area;
                var perim = ComputePolylinePerimeter2d(records[key].Polyline);
                var thick = perim > 1e-8 ? 2.0 * area / perim : 0.0;
                metrics.Add((key, area, perim));
                if (area > maxArea) maxArea = area;
                if (thick > maxThick) maxThick = thick;
            }

            foreach (var m in metrics)
            {
                if (IsSliverGeometry(m.Area, m.Perim, maxArea, maxThick))
                {
                    islands.Remove(m.Key);
                }
            }
        }

        private static void FilterSliverPolys2d(List<List<Point2d>> polys)
        {
            if (polys.Count <= 1) return;
            var metrics = new List<(double Area, double Perim)>(polys.Count);
            var maxArea = 0.0;
            var maxThick = 0.0;
            for (var i = 0; i < polys.Count; i++)
            {
                var area = Math.Abs(ComputeSignedArea2d(polys[i]));
                var perim = ComputePolygonPerimeter2d(polys[i]);
                var thick = perim > 1e-8 ? 2.0 * area / perim : 0.0;
                metrics.Add((area, perim));
                if (area > maxArea) maxArea = area;
                if (thick > maxThick) maxThick = thick;
            }

            for (var i = polys.Count - 1; i >= 0; i--)
            {
                if (IsSliverGeometry(metrics[i].Area, metrics[i].Perim, maxArea, maxThick))
                {
                    polys.RemoveAt(i);
                }
            }
        }

        private static double ComputePolylinePerimeter2d(Polyline poly)
        {
            try
            {
                var len = poly.Length;
                if (len > 1e-8 && !double.IsNaN(len) && !double.IsInfinity(len)) return len;
            }
            catch { }

            return ComputePolygonPerimeter2d(ExtractVerticesList(poly));
        }

        private static List<Point2d> ExtractVerticesList(Polyline poly)
        {
            var n = poly.NumberOfVertices;
            var pts = new List<Point2d>(n);
            for (var i = 0; i < n; i++) pts.Add(poly.GetPoint2dAt(i));
            return pts;
        }

        private static double ComputePolygonPerimeter2d(IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 2) return 0.0;
            var sum = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                sum += pts[i].GetDistanceTo(pts[(i + 1) % pts.Count]);
            }
            return sum;
        }

        private static bool IsRegionContainedIn(Region inner, Region outer, double innerArea)
        {
            using var cloneInner = (Region)inner.Clone();
            using var cloneOuter = (Region)outer.Clone();
            try
            {
                cloneInner.BooleanOperation(BooleanOperationType.BoolIntersect, cloneOuter);
                return cloneInner.Area >= innerArea * 0.5;
            }
            catch
            {
                return false;
            }
        }

        private static Hatch CreateEmptyClone(Hatch src)
        {
            var dst = (Hatch)src.Clone();
            dst.Associative = false;
            try { dst.RemoveAssociatedObjectIds(); } catch { }
            while (dst.NumberOfLoops > 0)
            {
                dst.RemoveLoopAt(dst.NumberOfLoops - 1);
            }
            dst.Origin = src.Origin;
            return dst;
        }

        private static bool AppendPolylineAsLoop(Hatch hatch, Polyline poly, HatchLoopTypes baseType, Vector2d offset)
        {
            var (pts, bulges) = ExtractVertices2d(poly, offset);
            if (pts.Count < 2) return false;
            if (pts.Count == 2 && Math.Abs(bulges[0]) < 1e-5 && Math.Abs(bulges[1]) < 1e-5) return false;
            hatch.AppendLoop(baseType | HatchLoopTypes.Polyline, pts, bulges);
            return true;
        }

        private static (Point2dCollection Pts, DoubleCollection Bulges) ExtractVertices2d(Polyline poly, Vector2d offset)
        {
            var pts = new Point2dCollection();
            var bulges = new DoubleCollection();
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                pts.Add(poly.GetPoint2dAt(i) + offset);
                bulges.Add(poly.GetBulgeAt(i));
            }
            return (pts, bulges);
        }

        private static Point2d ComputeHatchLocalOrigin2d(Hatch hatch)
        {
            try
            {
                var ext = hatch.GeometricExtents;
                var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, hatch.Normal));
                var pMin = ext.MinPoint.TransformBy(w2ocs);
                var pMax = ext.MaxPoint.TransformBy(w2ocs);
                return new Point2d(0.5 * (pMin.X + pMax.X), 0.5 * (pMin.Y + pMax.Y));
            }
            catch
            {
                return Point2d.Origin;
            }
        }

    }
}
