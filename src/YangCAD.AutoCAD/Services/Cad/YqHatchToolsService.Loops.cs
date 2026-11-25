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
        // HatchLoop <-> Polyline / Curve2d / Region 转换（含首尾链拼合 & CCW 统一）
        // ============================================================
        private static Polyline? ConvertLoopToPolyline(Hatch hatch, HatchLoop loop, Vector2d shift = default)
        {
            if (loop == null) return null;

            if (loop.IsPolyline && loop.Polyline != null && loop.Polyline.Count >= 2)
            {
                var bvs = loop.Polyline;
                var count = bvs.Count;
                if (count > 2 && bvs[0].Vertex.GetDistanceTo(bvs[count - 1].Vertex) < PtTol)
                    count--;

                var poly = new Polyline
                {
                    Normal = hatch.Normal,
                    Elevation = hatch.Elevation,
                    Closed = true
                };
                for (var i = 0; i < count; i++)
                {
                    poly.AddVertexAt(i, bvs[i].Vertex + shift, bvs[i].Bulge, 0.0, 0.0);
                }
                return SanitizeAndOrientPolyline2d(poly, wantCCW: true) ? poly : DisposeNull(poly);
            }

            if (loop.Curves == null || loop.Curves.Count == 0) return null;

            // 单圆弧闭环（完整圆）
            if (loop.Curves.Count == 1 && loop.Curves[0] is CircularArc2d singleArc &&
                singleArc.StartPoint.GetDistanceTo(singleArc.EndPoint) < PtTol)
            {
                var p0 = singleArc.StartPoint + shift;
                var c = singleArc.Center + shift;
                var opp = new Point2d(2 * c.X - p0.X, 2 * c.Y - p0.Y);
                var b = singleArc.IsClockWise ? -1.0 : 1.0;
                var circlePoly = new Polyline
                {
                    Normal = hatch.Normal,
                    Elevation = hatch.Elevation,
                    Closed = true
                };
                circlePoly.AddVertexAt(0, p0, b, 0.0, 0.0);
                circlePoly.AddVertexAt(1, opp, b, 0.0, 0.0);
                return SanitizeAndOrientPolyline2d(circlePoly, wantCCW: true) ? circlePoly : DisposeNull(circlePoly);
            }

            // 将 Curve2dCollection 标准化为段并首尾链拼合（防止部分曲线方向颠倒导致缺顶点）
            var segs = new List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)>();
            foreach (Curve2d c2 in loop.Curves)
            {
                if (c2 is LineSegment2d ls)
                {
                    var s = ls.StartPoint + shift;
                    var e = ls.EndPoint + shift;
                    if (s.GetDistanceTo(e) > 1e-8)
                        segs.Add((s, e, 0.0, null));
                }
                else if (c2 is CircularArc2d ca)
                {
                    var iv = ca.GetInterval();
                    var mid = ca.EvaluatePoint((iv.LowerBound + iv.UpperBound) * 0.5) + shift;
                    var s = ca.StartPoint + shift;
                    var e = ca.EndPoint + shift;
                    var bulge = ComputeThreePointBulge(s, mid, e);
                    segs.Add((s, e, bulge, null));
                }
                else
                {
                    var sampled = SampleCurve2d(c2, 24).Select(p => p + shift).ToList();
                    if (sampled.Count >= 2)
                        segs.Add((sampled[0], sampled[sampled.Count - 1], 0.0, sampled));
                }
            }

            var chainedPolys = ChainSegmentsIntoClosedPolylines(segs, hatch.Normal, hatch.Elevation);
            if (chainedPolys.Count == 0)
            {
                // HPGAPTOL 容差边界若存在 > 0.05 微小缺口，用无距离上限单环桥接兜底
                chainedPolys = ChainSingleHatchLoopSegments(segs, hatch.Normal, hatch.Elevation);
            }
            if (chainedPolys.Count == 0) return null;

            // 返回最大环并清理多余临时环
            chainedPolys.Sort((a, b) => Math.Abs(GetPolylineSignedArea2d(b)).CompareTo(Math.Abs(GetPolylineSignedArea2d(a))));
            for (var i = 1; i < chainedPolys.Count; i++) chainedPolys[i].Dispose();
            return chainedPolys[0];
        }

        private static List<Polyline> ExplodeRegionToClosedPolylines(Region rootRegion, Vector3d normal, double elevation)
        {
            var result = new List<Polyline>();
            ExplodeRegionRecursive(rootRegion, normal, elevation, result);
            return result;
        }

        private static void ExplodeRegionRecursive(Region region, Vector3d normal, double elevation, List<Polyline> output)
        {
            using var objs = new DBObjectCollection();
            try
            {
                region.Explode(objs);
            }
            catch
            {
                return;
            }

            var childRegions = new List<Region>();
            var curves = new List<Curve>();

            foreach (DBObject obj in objs)
            {
                if (obj is Region child) childRegions.Add(child);
                else if (obj is Curve curve) curves.Add(curve);
                else { try { obj.Dispose(); } catch { } }
            }

            // 每个子 Region 各自独立 Explode 并串联边界，彻底防止不同岛屿/孔洞边界混串
            foreach (var child in childRegions)
            {
                using (child)
                {
                    ExplodeRegionRecursive(child, normal, elevation, output);
                }
            }

            if (curves.Count == 0) return;

            var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, normal));
            var openSegs = new List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)>();

            foreach (var c in curves)
            {
                using (c)
                {
                    if (c is Circle circle)
                    {
                        var centerOcs = circle.Center.TransformBy(w2ocs);
                        var p0 = new Point2d(centerOcs.X + circle.Radius, centerOcs.Y);
                        var p1 = new Point2d(centerOcs.X - circle.Radius, centerOcs.Y);
                        var p = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
                        p.AddVertexAt(0, p0, 1.0, 0.0, 0.0);
                        p.AddVertexAt(1, p1, 1.0, 0.0, 0.0);
                        if (SanitizeAndOrientPolyline2d(p, wantCCW: true)) output.Add(p);
                        else p.Dispose();
                        continue;
                    }

                    if (c is Line line)
                    {
                        var sp = line.StartPoint.TransformBy(w2ocs);
                        var ep = line.EndPoint.TransformBy(w2ocs);
                        var s2 = new Point2d(sp.X, sp.Y);
                        var e2 = new Point2d(ep.X, ep.Y);
                        if (s2.GetDistanceTo(e2) > 1e-8)
                            openSegs.Add((s2, e2, 0.0, null));
                        continue;
                    }

                    if (c is Arc arc)
                    {
                        var sp = arc.StartPoint.TransformBy(w2ocs);
                        var ep = arc.EndPoint.TransformBy(w2ocs);
                        var midWcs = arc.GetPointAtParameter((arc.StartParam + arc.EndParam) * 0.5);
                        var mp = midWcs.TransformBy(w2ocs);
                        var s2 = new Point2d(sp.X, sp.Y);
                        var m2 = new Point2d(mp.X, mp.Y);
                        var e2 = new Point2d(ep.X, ep.Y);
                        var b = ComputeThreePointBulge(s2, m2, e2);
                        openSegs.Add((s2, e2, b, null));
                        continue;
                    }

                    if (c.Closed)
                    {
                        var closedPoly = SampleCurve3dToPolyline(c, w2ocs, normal, elevation, closed: true);
                        if (closedPoly != null) output.Add(closedPoly);
                    }
                    else
                    {
                        var pts = SampleCurve3dPoints(c, w2ocs, 32);
                        if (pts.Count >= 2)
                            openSegs.Add((pts[0], pts[pts.Count - 1], 0.0, pts));
                    }
                }
            }

            if (openSegs.Count > 0)
            {
                output.AddRange(ChainSegmentsIntoClosedPolylines(openSegs, normal, elevation));
            }
        }

        private static List<Polyline> ChainSegmentsIntoClosedPolylines(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> openSegs,
            Vector3d normal,
            double elevation)
        {
            var result = new List<Polyline>();
            var used = new bool[openSegs.Count];

            for (var startIdx = 0; startIdx < openSegs.Count; startIdx++)
            {
                if (used[startIdx]) continue;
                used[startIdx] = true;

                var chain = new List<(Point2d Pt, double Bulge)>();
                AppendSegToChain(chain, openSegs[startIdx], reverse: false);
                var chainStart = openSegs[startIdx].Start;
                var chainEnd = openSegs[startIdx].End;

                var closed = false;
                for (var guard = 0; guard < openSegs.Count + 2; guard++)
                {
                    if (chainEnd.GetDistanceTo(chainStart) < 5e-2 && IsValidLoopChain(chain))
                    {
                        if (chainEnd.GetDistanceTo(chainStart) > 1e-5)
                            chain.Add((chainEnd, 0.0));
                        closed = true;
                        break;
                    }

                    var (bestIdx, bestRev, bestDist) = FindNearestUnusedSeg(openSegs, used, chainEnd, 5e-2);
                    if (bestIdx < 0) break;
                    used[bestIdx] = true;
                    if (bestDist > 1e-5)
                        chain.Add((chainEnd, 0.0));
                    AppendSegToChain(chain, openSegs[bestIdx], bestRev);
                    chainEnd = bestRev ? openSegs[bestIdx].Start : openSegs[bestIdx].End;
                }

                if (closed && TryBuildPolylineFromChain(chain, normal, elevation, out var poly) && poly != null)
                {
                    result.Add(poly);
                }
            }

            return result;
        }

        private static List<Polyline> ChainSingleHatchLoopSegments(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> segs,
            Vector3d normal,
            double elevation)
        {
            var result = new List<Polyline>();
            if (segs.Count == 0) return result;

            var used = new bool[segs.Count];
            used[0] = true;

            var chain = new List<(Point2d Pt, double Bulge)>();
            AppendSegToChain(chain, segs[0], reverse: false);
            var chainStart = segs[0].Start;
            var chainEnd = segs[0].End;

            for (var step = 1; step < segs.Count; step++)
            {
                var (bestIdx, bestRev, bestDist) = FindNearestUnusedSeg(segs, used, chainEnd, double.MaxValue);
                if (bestIdx < 0) break;
                used[bestIdx] = true;
                if (bestDist > 1e-5)
                    chain.Add((chainEnd, 0.0));
                AppendSegToChain(chain, segs[bestIdx], bestRev);
                chainEnd = bestRev ? segs[bestIdx].Start : segs[bestIdx].End;
            }

            if (chainEnd.GetDistanceTo(chainStart) > 1e-5)
                chain.Add((chainEnd, 0.0));

            if (TryBuildPolylineFromChain(chain, normal, elevation, out var poly) && poly != null)
            {
                result.Add(poly);
            }

            return result;
        }

        private static (int Index, bool Reverse, double Distance) FindNearestUnusedSeg(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> segs,
            bool[] used,
            Point2d fromPt,
            double maxDist)
        {
            var bestIdx = -1;
            var bestRev = false;
            var bestDist = maxDist;

            for (var j = 0; j < segs.Count; j++)
            {
                if (used[j]) continue;
                var d1 = fromPt.GetDistanceTo(segs[j].Start);
                if (d1 < bestDist)
                {
                    bestDist = d1;
                    bestIdx = j;
                    bestRev = false;
                }
                var d2 = fromPt.GetDistanceTo(segs[j].End);
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    bestIdx = j;
                    bestRev = true;
                }
            }

            return (bestIdx, bestRev, bestDist);
        }

        private static bool TryBuildPolylineFromChain(
            List<(Point2d Pt, double Bulge)> chain,
            Vector3d normal,
            double elevation,
            out Polyline? poly)
        {
            poly = null;
            if (!IsValidLoopChain(chain)) return false;

            var candidate = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            for (var vi = 0; vi < chain.Count; vi++)
            {
                candidate.AddVertexAt(vi, chain[vi].Pt, chain[vi].Bulge, 0.0, 0.0);
            }
            if (!SanitizeAndOrientPolyline2d(candidate, wantCCW: true))
            {
                candidate.Dispose();
                return false;
            }

            poly = candidate;
            return true;
        }

        private static bool IsValidLoopChain(List<(Point2d Pt, double Bulge)> chain)
        {
            if (chain.Count >= 3) return true;
            return chain.Count == 2 && (Math.Abs(chain[0].Bulge) > 1e-5 || Math.Abs(chain[1].Bulge) > 1e-5);
        }

        private static void AppendSegToChain(
            List<(Point2d Pt, double Bulge)> chain,
            (Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts) seg,
            bool reverse)
        {
            if (seg.PolyPts != null && seg.PolyPts.Count >= 2)
            {
                if (!reverse)
                {
                    for (var i = 0; i < seg.PolyPts.Count - 1; i++)
                        chain.Add((seg.PolyPts[i], 0.0));
                }
                else
                {
                    for (var i = seg.PolyPts.Count - 1; i > 0; i--)
                        chain.Add((seg.PolyPts[i], 0.0));
                }
                return;
            }

            if (!reverse)
                chain.Add((seg.Start, seg.Bulge));
            else
                chain.Add((seg.End, -seg.Bulge));
        }

        // ============================================================
        // 清理重复顶点 & 共线/毛刺退化点 & 强制统一 CCW 方向
        // ============================================================
        private static bool SanitizeAndOrientPolyline2d(Polyline poly, bool wantCCW)
        {
            if (poly == null || poly.NumberOfVertices < 2) return false;

            const double mergeTol = 1e-3;
            var verts = new List<(Point2d Pt, double Bulge)>();
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                var pt = poly.GetPoint2dAt(i);
                var b = poly.GetBulgeAt(i);
                if (verts.Count == 0 || verts[verts.Count - 1].Pt.GetDistanceTo(pt) > mergeTol)
                {
                    verts.Add((pt, b));
                }
                else if (Math.Abs(verts[verts.Count - 1].Bulge) < 1e-6 && Math.Abs(b) > 1e-6)
                {
                    verts[verts.Count - 1] = (verts[verts.Count - 1].Pt, b);
                }
            }

            if (verts.Count > 2 && verts[0].Pt.GetDistanceTo(verts[verts.Count - 1].Pt) <= mergeTol)
            {
                verts.RemoveAt(verts.Count - 1);
            }

            RemoveCollinearAndSpikeVertices2d(verts);

            if (verts.Count < 2) return false;
            if (verts.Count == 2 && Math.Abs(verts[0].Bulge) < 1e-5 && Math.Abs(verts[1].Bulge) < 1e-5)
                return false;

            var signedArea = ComputeSignedAreaWithBulge2d(verts);
            if (Math.Abs(signedArea) < AreaTol) return false;

            if ((wantCCW && signedArea < 0) || (!wantCCW && signedArea > 0))
            {
                var n = verts.Count;
                var rev = new List<(Point2d Pt, double Bulge)>(n);
                for (var k = 0; k < n; k++)
                {
                    var srcIdx = k == 0 ? 0 : n - k;
                    var edgeIdx = (n - k - 1 + n) % n;
                    rev.Add((verts[srcIdx].Pt, -verts[edgeIdx].Bulge));
                }
                verts = rev;
            }

            while (poly.NumberOfVertices > verts.Count)
                poly.RemoveVertexAt(poly.NumberOfVertices - 1);
            while (poly.NumberOfVertices < verts.Count)
                poly.AddVertexAt(poly.NumberOfVertices, Point2d.Origin, 0.0, 0.0, 0.0);

            for (var i = 0; i < verts.Count; i++)
            {
                poly.SetPointAt(i, verts[i].Pt);
                poly.SetBulgeAt(i, verts[i].Bulge);
            }
            poly.Closed = true;
            return true;
        }

        private static void RemoveCollinearAndSpikeVertices2d(List<(Point2d Pt, double Bulge)> verts)
        {
            var changed = true;
            while (changed && verts.Count >= 3)
            {
                changed = false;
                for (var i = 0; i < verts.Count; i++)
                {
                    var n = verts.Count;
                    var prev = (i - 1 + n) % n;
                    var next = (i + 1) % n;

                    // 仅在前后边均为直线时清理，避免改动圆弧顶点或 bulge
                    if (Math.Abs(verts[prev].Bulge) >= 1e-6 || Math.Abs(verts[i].Bulge) >= 1e-6)
                        continue;

                    var a = verts[prev].Pt;
                    var b = verts[i].Pt;
                    var c = verts[next].Pt;
                    var v1 = b - a;
                    var v2 = c - b;
                    var len1 = v1.Length;
                    var len2 = v2.Length;

                    if (len1 < 1e-8 || len2 < 1e-8)
                    {
                        verts.RemoveAt(i);
                        changed = true;
                        break;
                    }

                    var dot = v1.DotProduct(v2);
                    var crossAbs = Math.Abs(v1.X * v2.Y - v1.Y * v2.X);

                    var ac = c - a;
                    var lenAc = ac.Length;
                    if (dot > 0.0 && lenAc > 1e-8)
                    {
                        var perp = Math.Abs((b.X - a.X) * ac.Y - (b.Y - a.Y) * ac.X) / lenAc;
                        if (perp < 1e-5)
                        {
                            verts.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }

                    if (dot < 0.0 && (a.GetDistanceTo(c) < 1e-5 || (crossAbs / len1 < 1e-5 && crossAbs / len2 < 1e-5)))
                    {
                        verts.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
        }

        private static double GetPolylineSignedArea2d(Polyline poly)
        {
            var list = new List<(Point2d Pt, double Bulge)>(poly.NumberOfVertices);
            for (var i = 0; i < poly.NumberOfVertices; i++)
                list.Add((poly.GetPoint2dAt(i), poly.GetBulgeAt(i)));
            return ComputeSignedAreaWithBulge2d(list);
        }

        private static double ComputeSignedAreaWithBulge2d(IReadOnlyList<(Point2d Pt, double Bulge)> verts)
        {
            double sum = 0.0;
            var n = verts.Count;
            for (var i = 0; i < n; i++)
            {
                var p1 = verts[i].Pt;
                var p2 = verts[(i + 1) % n].Pt;
                sum += 0.5 * (p1.X * p2.Y - p2.X * p1.Y);

                var b = verts[i].Bulge;
                if (Math.Abs(b) > 1e-8)
                {
                    var chord = p1.GetDistanceTo(p2);
                    if (chord > 1e-8)
                    {
                        var absB = Math.Abs(b);
                        var theta = 4.0 * Math.Atan(absB);
                        var r = chord * (1.0 + absB * absB) / (4.0 * absB);
                        var segArea = 0.5 * r * r * (theta - Math.Sin(theta));
                        sum += b > 0 ? segArea : -segArea;
                    }
                }
            }
            return sum;
        }

        private static Region? CreateRegionFromCurve(Curve curve)
        {
            using var segs = new DBObjectCollection { curve };
            try
            {
                using var regs = Region.CreateFromCurves(segs);
                if (regs.Count == 0) return null;

                Region? primary = regs[0] as Region;
                for (var i = 1; i < regs.Count; i++)
                {
                    if (regs[i] is Region extra)
                    {
                        if (primary != null)
                        {
                            try { primary.BooleanOperation(BooleanOperationType.BoolUnite, extra); } catch { }
                        }
                        extra.Dispose();
                    }
                }
                return primary;
            }
            catch
            {
                return null;
            }
        }

        // ============================================================
        // 几何小助手（扁平化 / 凸性 / 点内判 / 三点凸度 / 平面投影）
        // ============================================================
        private static List<Point2d>? GetStraightVertices(Polyline poly)
        {
            var vertices = new List<Point2d>(poly.NumberOfVertices);
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                if (Math.Abs(poly.GetBulgeAt(i)) > 1e-9) return null;
                AddUniquePt(vertices, poly.GetPoint2dAt(i));
            }
            if (vertices.Count > 2 && vertices[0].GetDistanceTo(vertices[vertices.Count - 1]) < PtTol)
                vertices.RemoveAt(vertices.Count - 1);
            return vertices;
        }

        private static double ComputeSignedArea2d(IReadOnlyList<Point2d> pts)
        {
            double s = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                s += 0.5 * (a.X * b.Y - b.X * a.Y);
            }
            return s;
        }

        private static bool IsConvexPolygonCcw(IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 3) return false;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                var c = pts[(i + 2) % pts.Count];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross < -1e-6) return false;
            }
            return true;
        }

        private static Point2d FindPointInsideOrOnPolygon(IReadOnlyList<Point2d> pts)
        {
            double cx = 0, cy = 0;
            foreach (var p in pts) { cx += p.X; cy += p.Y; }
            var cen = new Point2d(cx / pts.Count, cy / pts.Count);
            if (IsPointInPolygon2d(cen, pts)) return cen;
            return new Point2d((pts[0].X + pts[1].X) * 0.5, (pts[0].Y + pts[1].Y) * 0.5);
        }

        private static bool IsPointInPolygon2d(Point2d pt, IReadOnlyList<Point2d> poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var xi = poly[i].X; var yi = poly[i].Y;
                var xj = poly[j].X; var yj = poly[j].Y;
                if (((yi > pt.Y) != (yj > pt.Y)) &&
                    (pt.X < (xj - xi) * (pt.Y - yi) / ((yj - yi) + 1e-18) + xi))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static double ComputeThreePointBulge(Point2d start, Point2d mid, Point2d end)
        {
            var ax = start.X; var ay = start.Y;
            var bx = mid.X; var by = mid.Y;
            var cx = end.X; var cy = end.Y;

            var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-10) return 0.0;

            var aSq = ax * ax + ay * ay;
            var bSq = bx * bx + by * by;
            var cSq = cx * cx + cy * cy;

            var ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / d;
            var uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / d;
            var center = new Point2d(ux, uy);

            var v1 = start - center;
            var vm = mid - center;
            var v2 = end - center;

            var a1 = Math.Atan2(v1.Y, v1.X);
            var am = Math.Atan2(vm.Y, vm.X);
            var a2 = Math.Atan2(v2.Y, v2.X);

            var ccwTotal = PositiveMod(a2 - a1, 2.0 * Math.PI);
            var ccwMid = PositiveMod(am - a1, 2.0 * Math.PI);

            if (ccwMid <= ccwTotal + 1e-6)
                return Math.Tan(ccwTotal / 4.0);

            var cwTotal = 2.0 * Math.PI - ccwTotal;
            return -Math.Tan(cwTotal / 4.0);
        }

        private static double PositiveMod(double val, double mod)
        {
            var r = val % mod;
            return r < 0 ? r + mod : r;
        }

        private static List<Point2d> SampleCurve2d(Curve2d c2, int count)
        {
            var pts = new List<Point2d>(count + 1);
            var iv = c2.GetInterval();
            var t0 = iv.LowerBound;
            var t1 = iv.UpperBound;
            for (var i = 0; i <= count; i++)
            {
                var t = t0 + (t1 - t0) * i / count;
                pts.Add(c2.EvaluatePoint(t));
            }
            return pts;
        }

        private static List<Point2d> SampleCurve3dPoints(Curve curve, Matrix3d w2ocs, int count)
        {
            var pts = new List<Point2d>(count + 1);
            var t0 = curve.StartParam;
            var t1 = curve.EndParam;
            for (var i = 0; i <= count; i++)
            {
                var t = t0 + (t1 - t0) * i / count;
                var p = curve.GetPointAtParameter(t).TransformBy(w2ocs);
                var p2 = new Point2d(p.X, p.Y);
                if (pts.Count == 0 || pts[pts.Count - 1].GetDistanceTo(p2) > PtTol || i == count)
                    pts.Add(p2);
            }
            return pts;
        }

        private static Polyline? SampleCurve3dToPolyline(Curve curve, Matrix3d w2ocs, Vector3d normal, double elevation, bool closed)
        {
            var pts = SampleCurve3dPoints(curve, w2ocs, 48);
            if (closed && pts.Count > 2 && pts[0].GetDistanceTo(pts[pts.Count - 1]) < PtTol)
                pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return null;

            var poly = new Polyline { Normal = normal, Elevation = elevation, Closed = closed };
            for (var i = 0; i < pts.Count; i++)
                poly.AddVertexAt(i, pts[i], 0.0, 0.0, 0.0);
            if (closed && !SanitizeAndOrientPolyline2d(poly, wantCCW: true)) return DisposeNull(poly);
            return poly;
        }

        private static Polyline? ProjectWcsPolylineToHatchPlane(
            Polyline polyWcs,
            Vector3d normal,
            double elevation,
            Point2d localOrigin)
        {
            var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, normal));
            var outPoly = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            var n = polyWcs.NumberOfVertices;
            if (n < 2) return null;

            var sign = polyWcs.Normal.DotProduct(normal) >= 0 ? 1.0 : -1.0;

            for (var i = 0; i < n; i++)
            {
                var ptWcs = polyWcs.GetPoint3dAt(i);
                var ptOcs = ptWcs.TransformBy(w2ocs);
                var bulge = polyWcs.GetBulgeAt(i) * sign;
                outPoly.AddVertexAt(i, new Point2d(ptOcs.X - localOrigin.X, ptOcs.Y - localOrigin.Y), bulge, 0.0, 0.0);
            }

            if (!SanitizeAndOrientPolyline2d(outPoly, wantCCW: true))
                return DisposeNull(outPoly);
            return outPoly;
        }

        private static Polyline BuildOcsRectPoly(Vector3d normal, double elevation, Point2d p0, Point2d p1, Point2d p2, Point2d p3)
        {
            var p = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            p.AddVertexAt(0, p0, 0.0, 0.0, 0.0);
            p.AddVertexAt(1, p1, 0.0, 0.0, 0.0);
            p.AddVertexAt(2, p2, 0.0, 0.0, 0.0);
            p.AddVertexAt(3, p3, 0.0, 0.0, 0.0);
            SanitizeAndOrientPolyline2d(p, wantCCW: true);
            return p;
        }

        private static Region? BuildSlitRegionFromPolyline(Vector3d normal, double elevation, IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 2) return null;
            const double halfW = 0.001;
            Region? slit = null;
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var d = b - a;
                if (d.Length < 1e-6) continue;
                var u = d.GetNormal();
                var n = new Vector2d(-u.Y, u.X) * halfW;
                var aExt = a - (i == 0 ? u * 1000.0 : u * halfW);
                var bExt = b + (i == pts.Count - 2 ? u * 1000.0 : u * halfW);
                using var strip = BuildOcsRectPoly(normal, elevation, aExt - n, bExt - n, bExt + n, aExt + n);
                var piece = CreateRegionFromCurve(strip);
                if (piece == null) continue;
                if (slit == null)
                {
                    slit = piece;
                }
                else
                {
                    try { slit.BooleanOperation(BooleanOperationType.BoolUnite, piece); } catch { }
                    piece.Dispose();
                }
            }
            return slit;
        }

    }
}
