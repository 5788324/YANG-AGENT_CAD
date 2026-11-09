using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Validated WCS XY polygon. Geometry-only helper, reusable by a future YGOUTSIDE.
    /// All boundary edges must be straight; bulge/OCS rejection is performed by callers.
    /// Boundary-touch is considered inside; ambiguous non-curve objects are not clipped.
    /// </summary>
    public sealed class PolygonBoundary2d
    {
        private const double Eps = 1e-8;
        private const double TwoPi = Math.PI * 2;
        private readonly Point2d[] _vertices;
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public int Count => _vertices.Length;
        public Point2d GetVertex(int i) => _vertices[i];

        public sealed class LinePart
        {
            public Point3d Start { get; set; }
            public Point3d End { get; set; }
        }

        public sealed class ArcPart
        {
            public double StartAngle { get; set; }
            public double EndAngle { get; set; }
        }

        public PolygonBoundary2d(IReadOnlyList<Point2d> vertices)
        {
            if (vertices == null || vertices.Count < 3 || vertices.Count > 512)
                throw new ArgumentException("闭合边界须包含 3～512 个顶点。");
            _vertices = vertices.ToArray();
            foreach (var p in _vertices)
                if (!Finite(p.X) || !Finite(p.Y))
                    throw new ArgumentException("边界坐标不能包含 NaN 或 Infinity。");
            MinX = _vertices.Min(p => p.X);
            MinY = _vertices.Min(p => p.Y);
            MaxX = _vertices.Max(p => p.X);
            MaxY = _vertices.Max(p => p.Y);
            if (MaxX - MinX < Eps || MaxY - MinY < Eps)
                throw new ArgumentException("边界宽度或高度过小。");
            double signedArea = 0;
            for (int i = 0; i < Count; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) % Count];
                if (DistSquared(a, b) < Eps * Eps)
                    throw new ArgumentException("边界含重复相邻顶点或零长度边。");
                signedArea += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(signedArea) < Eps * Eps)
                throw new ArgumentException("闭合边界面积过小。");
            // Adjacent sides share their intended endpoint. Any other contact is invalid.
            for (int i = 0; i < Count; i++)
                for (int j = i + 1; j < Count; j++)
                {
                    if (j == i + 1 || (i == 0 && j == Count - 1)) continue;
                    if (SegmentsTouch(_vertices[i], _vertices[(i + 1) % Count],
                                      _vertices[j], _vertices[(j + 1) % Count]))
                        throw new ArgumentException("闭合边界自交或自接触，不能安全裁切。");
                }
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;
        private static double Cross(Point2d a, Point2d b, Point2d p) =>
            Cross(b.X - a.X, b.Y - a.Y, p.X - a.X, p.Y - a.Y);
        private static double DistSquared(Point2d a, Point2d b) =>
            (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        private static bool OnSegment(Point2d a, Point2d b, Point2d p)
        {
            var len = Math.Sqrt(DistSquared(a, b));
            return Math.Abs(Cross(a, b, p)) <= Eps * Math.Max(1, len) &&
                   p.X >= Math.Min(a.X, b.X) - Eps && p.X <= Math.Max(a.X, b.X) + Eps &&
                   p.Y >= Math.Min(a.Y, b.Y) - Eps && p.Y <= Math.Max(a.Y, b.Y) + Eps;
        }

        private static bool SegmentsTouch(Point2d a, Point2d b, Point2d c, Point2d d)
        {
            var o1 = Cross(a, b, c); var o2 = Cross(a, b, d);
            var o3 = Cross(c, d, a); var o4 = Cross(c, d, b);
            if ((o1 > Eps && o2 < -Eps || o1 < -Eps && o2 > Eps) &&
                (o3 > Eps && o4 < -Eps || o3 < -Eps && o4 > Eps)) return true;
            return OnSegment(a, b, c) || OnSegment(a, b, d) ||
                   OnSegment(c, d, a) || OnSegment(c, d, b);
        }

        public bool BoundsOverlap(double left, double bottom, double right, double top) =>
            right >= MinX - Eps && left <= MaxX + Eps &&
            top >= MinY - Eps && bottom <= MaxY + Eps;

        public bool Contains(Point2d p)
        {
            bool inside = false;
            for (int i = 0, j = Count - 1; i < Count; j = i++)
            {
                var a = _vertices[j]; var b = _vertices[i];
                if (OnSegment(a, b, p)) return true;
                if ((a.Y > p.Y) != (b.Y > p.Y))
                {
                    var x = a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                    if (x > p.X) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Conservative bounding-box containment for blocks, text, dimensions, etc.
        /// Checks all corners and rejects any boundary intersection/vertex inside the box.
        /// </summary>
        public bool ContainsBox(double left, double bottom, double right, double top)
        {
            var corners = new[] {
                new Point2d(left, bottom), new Point2d(right, bottom),
                new Point2d(right, top), new Point2d(left, top)
            };
            if (!corners.All(Contains)) return false;
            for (int i = 0; i < Count; i++)
            {
                var a = _vertices[i]; var b = _vertices[(i + 1) % Count];
                for (int k = 0; k < 4; k++)
                    if (SegmentsTouch(a, b, corners[k], corners[(k + 1) % 4]))
                        return false;
                if (a.X > left && a.X < right && a.Y > bottom && a.Y < top)
                    return false;
            }
            return true;
        }

        private static void AddCut(List<double> cuts, double value, double end)
        {
            if (Finite(value) && value > Eps && value < end - Eps) cuts.Add(value);
        }

        private static List<double> UniqueCuts(List<double> cuts)
        {
            cuts.Sort();
            var result = new List<double>();
            foreach (var t in cuts)
                if (result.Count == 0 || t - result[result.Count - 1] > Eps)
                    result.Add(t);
            return result;
        }

        /// <summary>Exact line intersections with all straight polygon sides.</summary>
        public List<LinePart> ClipLine(Point3d start, Point3d end)
        {
            var p = new Point2d(start.X, start.Y);
            var dx = end.X - start.X; var dy = end.Y - start.Y;
            var sq = dx * dx + dy * dy;
            if (sq < Eps * Eps) return new List<LinePart>();
            var cuts = new List<double> { 0, 1 };
            for (int i = 0; i < Count; i++)
            {
                var q = _vertices[i];
                var next = _vertices[(i + 1) % Count];
                var sx = next.X - q.X; var sy = next.Y - q.Y;
                var den = Cross(dx, dy, sx, sy);
                var qx = q.X - p.X; var qy = q.Y - p.Y;
                if (Math.Abs(den) <= Eps * Math.Max(1, Math.Sqrt(sq * (sx * sx + sy * sy))))
                {
                    if (Math.Abs(Cross(qx, qy, dx, dy)) <= Eps * Math.Max(1, Math.Sqrt(sq)))
                    {
                        AddCut(cuts, (qx * dx + qy * dy) / sq, 1);
                        AddCut(cuts, ((next.X - p.X) * dx + (next.Y - p.Y) * dy) / sq, 1);
                    }
                    continue;
                }
                var t = Cross(qx, qy, sx, sy) / den;
                var u = Cross(qx, qy, dx, dy) / den;
                if (t >= -Eps && t <= 1 + Eps && u >= -Eps && u <= 1 + Eps)
                    AddCut(cuts, t, 1);
            }
            var sorted = UniqueCuts(cuts);
            var output = new List<LinePart>();
            var vec = end - start;
            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                var lo = sorted[i]; var hi = sorted[i + 1];
                if (hi - lo <= Eps) continue;
                var mid = start + vec * ((lo + hi) * .5);
                if (Contains(new Point2d(mid.X, mid.Y)))
                    output.Add(new LinePart { Start = start + vec * lo, End = start + vec * hi });
            }
            return output;
        }

        private static double Positive(double angle)
        {
            angle %= TwoPi;
            return angle < 0 ? angle + TwoPi : angle;
        }

        /// <summary>Analytic circle-segment intersections; returns kept angular intervals.</summary>
        public List<ArcPart> ClipArc(Point3d center, double radius,
                                    double startAngle, double endAngle, bool fullCircle)
        {
            if (!Finite(radius) || radius <= Eps) return new List<ArcPart>();
            var span = fullCircle ? TwoPi : endAngle - startAngle;
            while (!fullCircle && span <= 0) span += TwoPi;
            if (span > TwoPi) span = TwoPi;
            if (span <= Eps) return new List<ArcPart>();
            var cuts = new List<double> { 0, span };
            for (int i = 0; i < Count; i++)
            {
                var a = _vertices[i]; var b = _vertices[(i + 1) % Count];
                var dx = b.X - a.X; var dy = b.Y - a.Y;
                var fx = a.X - center.X; var fy = a.Y - center.Y;
                var A = dx * dx + dy * dy;
                var B = 2 * (fx * dx + fy * dy);
                var C = fx * fx + fy * fy - radius * radius;
                var discriminant = B * B - 4 * A * C;
                if (discriminant < 0) continue;
                var root = Math.Sqrt(Math.Max(0, discriminant));
                foreach (var u in new[] { (-B - root) / (2 * A), (-B + root) / (2 * A) })
                {
                    if (u < -Eps || u > 1 + Eps) continue;
                    var angle = Math.Atan2(fy + u * dy, fx + u * dx);
                    AddCut(cuts, Positive(angle - startAngle), span);
                }
            }
            var sorted = UniqueCuts(cuts);
            var output = new List<ArcPart>();
            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                var lo = sorted[i]; var hi = sorted[i + 1];
                if (hi - lo <= Eps) continue;
                var mid = startAngle + (lo + hi) * .5;
                var sample = new Point2d(center.X + radius * Math.Cos(mid),
                                         center.Y + radius * Math.Sin(mid));
                if (Contains(sample))
                    output.Add(new ArcPart { StartAngle = startAngle + lo, EndAngle = startAngle + hi });
            }
            return output;
        }
    }
}
