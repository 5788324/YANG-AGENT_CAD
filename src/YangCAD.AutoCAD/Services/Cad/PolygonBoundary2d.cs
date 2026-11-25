using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Validated WCS XY polygon with straight and circular-arc sides.
    /// Boundary-touch is considered inside; ambiguous non-curve objects are not clipped.
    /// </summary>
    public sealed class PolygonBoundary2d
    {
        private const double Eps = 1e-8;
        private const double TwoPi = Math.PI * 2;
        private readonly Point2d[] _vertices;
        private readonly double[] _bulges;
        private readonly BoundaryArc?[] _arcs;
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public int Count => _vertices.Length;
        public Point2d GetVertex(int i) => _vertices[i];
        public double GetBulge(int i) => _bulges[i];

        internal Polyline CreatePolyline(double elevation)
        {
            var polyline = new Polyline(Count) { Closed = true, Elevation = elevation };
            for (var i = 0; i < Count; i++)
                polyline.AddVertexAt(i, _vertices[i], _bulges[i], 0, 0);
            return polyline;
        }

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

        internal static Point3d[] RectangleCornersInWorld(Point3d first, Point3d opposite, Matrix3d ucs)
        {
            RequireHorizontalUcs(ucs);
            var left = Math.Min(first.X, opposite.X);
            var right = Math.Max(first.X, opposite.X);
            var bottom = Math.Min(first.Y, opposite.Y);
            var top = Math.Max(first.Y, opposite.Y);
            if (right - left <= 1e-8 || top - bottom <= 1e-8)
                throw new ArgumentException("矩形边界宽度和高度必须大于 0。");

            return new[]
            {
                new Point3d(left, bottom, first.Z).TransformBy(ucs),
                new Point3d(right, bottom, first.Z).TransformBy(ucs),
                new Point3d(right, top, first.Z).TransformBy(ucs),
                new Point3d(left, top, first.Z).TransformBy(ucs)
            };
        }

        internal static void RequireHorizontalUcs(Matrix3d ucs)
        {
            var normal = Vector3d.ZAxis.TransformBy(ucs);
            if (Math.Abs(normal.GetNormal().DotProduct(Vector3d.ZAxis)) < 0.999999)
                throw new NotSupportedException("当前 UCS 与世界 XY 平面不平行，请切换到水平 UCS。");
        }

        public PolygonBoundary2d(IReadOnlyList<Point2d> vertices, IReadOnlyList<double>? bulges = null)
        {
            if (vertices == null || vertices.Count < 2 || vertices.Count > 512 ||
                (bulges != null && bulges.Count != vertices.Count))
                throw new ArgumentException("闭合边界须包含 2～512 个顶点，直边边界至少三个。");
            _vertices = vertices.ToArray();
            _bulges = bulges?.ToArray() ?? new double[Count];
            _arcs = new BoundaryArc?[Count];
            var bounds = new List<Point2d>(_vertices);
            double signedArea = 0;
            var origin = _vertices[0];
            for (var i = 0; i < Count; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) % Count];
                if (!Finite(a.X) || !Finite(a.Y) || !Finite(_bulges[i]))
                    throw new ArgumentException("边界含无效坐标或圆弧参数。");
                if (DistSquared(a, b) < Eps * Eps)
                    throw new ArgumentException("边界含重复相邻顶点或零长度边。");
                signedArea += Cross(a.X - origin.X, a.Y - origin.Y, b.X - origin.X, b.Y - origin.Y) * .5;
                if (Math.Abs(_bulges[i]) <= Eps)
                {
                    _bulges[i] = 0;
                    continue;
                }
                var arc = new BoundaryArc(a, b, _bulges[i]);
                _arcs[i] = arc;
                signedArea += .5 * arc.Radius * arc.Radius * (arc.Sweep - Math.Sin(arc.Sweep));
                for (var quadrant = 0; quadrant < 4; quadrant++)
                {
                    var angle = quadrant * Math.PI / 2;
                    if (arc.IncludesAngle(angle)) bounds.Add(arc.AtAngle(angle));
                }
            }
            MinX = bounds.Min(p => p.X);
            MinY = bounds.Min(p => p.Y);
            MaxX = bounds.Max(p => p.X);
            MaxY = bounds.Max(p => p.Y);
            if (Math.Abs(signedArea) < Eps * Eps || MaxX - MinX < Eps || MaxY - MinY < Eps)
                throw new ArgumentException("闭合边界面积过小。");

            for (var i = 0; i < Count; i++)
                for (var j = i + 1; j < Count; j++)
                {
                    var a = _vertices[i]; var b = _vertices[(i + 1) % Count];
                    var c = _vertices[j]; var d = _vertices[(j + 1) % Count];
                    var adjacent = j == i + 1 || (i == 0 && j == Count - 1);
                    var first = _arcs[i]; var second = _arcs[j];
                    var intersections = first != null && second != null
                        ? first.Intersections(second)
                        : first != null ? first.Intersections(c, d)
                        : second != null ? second.Intersections(a, b)
                        : Enumerable.Empty<Point2d>();
                    var crosses = intersections.Any(p => !adjacent ||
                        !(DistSquared(p, b) <= Eps * Eps && j == i + 1 ||
                          DistSquared(p, a) <= Eps * Eps && i == 0 && j == Count - 1));
                    if (!crosses && first == null && second == null)
                        crosses = adjacent
                            ? (j == i + 1 && Count > 2 && (OnSegment(a, b, d) || OnSegment(c, d, a))) ||
                              (i == 0 && j == Count - 1 && Count > 2 && (OnSegment(a, b, c) || OnSegment(c, d, b)))
                            : SegmentsTouch(a, b, c, d);
                    if (crosses) throw new ArgumentException("闭合边界自交或自接触，不能安全裁切。");
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
                if (_arcs[j] is BoundaryArc arc)
                {
                    if (arc.Contains(p)) return true;
                    if (arc.CrossesRay(p)) inside = !inside;
                    continue;
                }
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
                var a = _vertices[i];
                for (int k = 0; k < 4; k++)
                    if (SideTouches(i, corners[k], corners[(k + 1) % 4]))
                        return false;
                if (a.X > left && a.X < right && a.Y > bottom && a.Y < top)
                    return false;
            }
            return true;
        }

        internal bool SideTouches(int side, Point2d a, Point2d b)
        {
            if (_arcs[side] is BoundaryArc arc)
                return arc.Contains(a) || arc.Contains(b) || arc.Intersections(a, b).Any();
            return SegmentsTouch(_vertices[side], _vertices[(side + 1) % Count], a, b);
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

        /// <summary>Exact line intersections with all polygon sides.</summary>
        public List<LinePart> ClipLine(Point3d start, Point3d end)
        {
            var p = new Point2d(start.X, start.Y);
            var dx = end.X - start.X; var dy = end.Y - start.Y;
            var sq = dx * dx + dy * dy;
            if (sq < Eps * Eps) return new List<LinePart>();
            var cuts = new List<double> { 0, 1 };
            for (int i = 0; i < Count; i++)
            {
                if (_arcs[i] is BoundaryArc arc)
                {
                    foreach (var intersection in arc.Intersections(p, new Point2d(end.X, end.Y)))
                        AddCut(cuts, ((intersection.X - p.X) * dx + (intersection.Y - p.Y) * dy) / sq, 1);
                    continue;
                }
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
                if (!Contains(new Point2d(mid.X, mid.Y))) continue;
                var from = start + vec * lo;
                var to = start + vec * hi;
                if (output.Count > 0 && output[output.Count - 1].End.DistanceTo(from) <= Eps)
                    output[output.Count - 1].End = to;
                else
                    output.Add(new LinePart { Start = from, End = to });
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
            var sourceArc = new BoundaryArc(new Point2d(center.X, center.Y), radius, startAngle, span);
            for (int i = 0; i < Count; i++)
            {
                if (_arcs[i] is BoundaryArc arc)
                {
                    foreach (var intersection in sourceArc.Intersections(arc))
                        AddCut(cuts, Positive(Math.Atan2(intersection.Y - center.Y, intersection.X - center.X) - startAngle), span);
                    continue;
                }
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
                if (!Contains(sample)) continue;
                if (output.Count > 0 && Math.Abs(output[output.Count - 1].EndAngle - startAngle - lo) <= Eps)
                    output[output.Count - 1].EndAngle = startAngle + hi;
                else
                    output.Add(new ArcPart { StartAngle = startAngle + lo, EndAngle = startAngle + hi });
            }
            return output;
        }
        private sealed class BoundaryArc
        {
            private readonly Point2d _center;
            private readonly double _start;
            private readonly double _startY;
            private readonly double _endY;
            public double Radius { get; }
            public double Sweep { get; }

            internal BoundaryArc(Point2d center, double radius, double start, double sweep)
            {
                _center = center; Radius = radius; _start = start; Sweep = sweep;
                _startY = center.Y + radius * Math.Sin(start);
                _endY = center.Y + radius * Math.Sin(start + sweep);
            }

            internal BoundaryArc(Point2d a, Point2d b, double bulge)
            {
                var offset = (1 - bulge * bulge) / (4 * bulge);
                _center = new Point2d((a.X + b.X) / 2 - (b.Y - a.Y) * offset,
                                      (a.Y + b.Y) / 2 + (b.X - a.X) * offset);
                Radius = Math.Sqrt(DistSquared(a, _center));
                _start = Math.Atan2(a.Y - _center.Y, a.X - _center.X);
                Sweep = 4 * Math.Atan(bulge);
                _startY = a.Y;
                _endY = b.Y;
            }

            internal Point2d AtAngle(double angle) =>
                new Point2d(_center.X + Radius * Math.Cos(angle), _center.Y + Radius * Math.Sin(angle));

            private double DistanceAlong(double angle) => Positive(Math.Sign(Sweep) * (angle - _start));

            internal bool IncludesAngle(double angle)
            {
                var distance = DistanceAlong(angle);
                return distance <= Math.Abs(Sweep) + Eps || distance >= TwoPi - Eps;
            }

            internal bool Contains(Point2d p) =>
                Math.Abs(Math.Sqrt(DistSquared(_center, p)) - Radius) <= Eps &&
                IncludesAngle(Math.Atan2(p.Y - _center.Y, p.X - _center.X));

            internal bool CrossesRay(Point2d p)
            {
                var y = p.Y - _center.Y;
                if (Math.Abs(y) >= Radius) return false;
                var knots = new List<double> { 0, Math.Abs(Sweep) };
                AddCut(knots, DistanceAlong(Math.PI / 2), Math.Abs(Sweep));
                AddCut(knots, DistanceAlong(3 * Math.PI / 2), Math.Abs(Sweep));
                knots.Sort();
                var parity = false;
                for (var i = 1; i < knots.Count; i++)
                {
                    var a = _start + Math.Sign(Sweep) * knots[i - 1];
                    var b = _start + Math.Sign(Sweep) * knots[i];
                    var loY = i == 1 ? _startY : _center.Y + Radius * Math.Sin(a);
                    var hiY = i == knots.Count - 1 ? _endY : _center.Y + Radius * Math.Sin(b);
                    if ((loY > p.Y) == (hiY > p.Y)) continue;
                    var x = _center.X + Math.Sign(Math.Cos((a + b) / 2)) *
                        Math.Sqrt(Math.Max(0, (Radius - y) * (Radius + y)));
                    if (x > p.X) parity = !parity;
                }
                return parity;
            }

            internal IEnumerable<Point2d> Intersections(Point2d a, Point2d b)
            {
                var dx = b.X - a.X; var dy = b.Y - a.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= Eps) yield break;
                dx /= length; dy /= length;
                var x = _center.X - a.X; var y = _center.Y - a.Y;
                var foot = x * dx + y * dy;
                var offset = Math.Abs(Cross(dx, dy, x, y));
                if (offset > Radius + Eps) yield break;
                var delta = Math.Sqrt(Math.Max(0, (Radius - offset) * (Radius + offset)));
                foreach (var t in new[] { foot - delta, foot + delta })
                {
                    if (t < -Eps || t > length + Eps) continue;
                    var point = new Point2d(a.X + t * dx, a.Y + t * dy);
                    if (IncludesAngle(Math.Atan2(point.Y - _center.Y, point.X - _center.X))) yield return point;
                }
            }

            internal IEnumerable<Point2d> Intersections(BoundaryArc other)
            {
                var dx = other._center.X - _center.X; var dy = other._center.Y - _center.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= Eps)
                {
                    if (Math.Abs(Radius - other.Radius) > Eps) yield break;
                    foreach (var point in new[] {
                        AtAngle(_start), AtAngle(_start + Sweep), AtAngle(_start + Sweep / 2),
                        other.AtAngle(other._start), other.AtAngle(other._start + other.Sweep),
                        other.AtAngle(other._start + other.Sweep / 2)
                    })
                        if (Contains(point) && other.Contains(point)) yield return point;
                    yield break;
                }
                if (distance > Radius + other.Radius + Eps ||
                    distance < Math.Abs(Radius - other.Radius) - Eps) yield break;
                dx /= distance; dy /= distance;
                var along = (distance + (Radius - other.Radius) * (Radius + other.Radius) / distance) / 2;
                var height = Math.Sqrt(Math.Max(0, (Radius - along) * (Radius + along)));
                foreach (var h in new[] { -height, height })
                {
                    var point = new Point2d(_center.X + along * dx - h * dy, _center.Y + along * dy + h * dx);
                    if (IncludesAngle(Math.Atan2(point.Y - _center.Y, point.X - _center.X)) &&
                        other.IncludesAngle(Math.Atan2(point.Y - other._center.Y, point.X - other._center.X)))
                        yield return point;
                }
            }
        }
    }
}
