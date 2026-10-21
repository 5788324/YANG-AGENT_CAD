using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class PolylineInspectionService
    {
        public static Polyline PolylineFromHandle(Database db, Transaction tr, string handle, OpenMode mode)
            => tr.GetObject(EntityHelpers.FromHandle(db, handle), mode) as Polyline
               ?? throw new InvalidOperationException("Handle 不是 2D Lightweight Polyline。");

        public static bool Near(Point2d a, Point2d b, double tolerance)
            => a.GetDistanceTo(b) <= tolerance;

        public static bool CollinearAndBetween(Point2d a, Point2d b, Point2d c, double tolerance)
        {
            var ab = b - a;
            var ac = c - a;
            var acLength = ac.Length;
            if (acLength <= tolerance) return false;

            var cross = Math.Abs(ab.X * ac.Y - ab.Y * ac.X);
            var perpendicularDistance = cross / acLength;
            if (perpendicularDistance > tolerance) return false;

            var dot = (b.X - a.X) * (b.X - c.X) + (b.Y - a.Y) * (b.Y - c.Y);
            return dot <= tolerance * Math.Max(1.0, acLength);
        }

        public static bool HasNonZeroSegmentWidths(Polyline p)
        {
            if (p.HasWidth) return true;
            try { if (p.ConstantWidth != 0.0) return true; } catch { }
            for (var i = 0; i < p.NumberOfVertices; i++)
            {
                if (p.GetStartWidthAt(i) != 0.0 || p.GetEndWidthAt(i) != 0.0) return true;
            }
            return false;
        }

        public static object InspectPolyline(Polyline p, double tolerance)
        {
            var duplicates = new List<int>();
            var zeroSegments = new List<int>();
            var collinear = new List<int>();
            var hasBulges = p.HasBulges;
            var hasVariableWidths = HasNonZeroSegmentWidths(p);
            var n = p.NumberOfVertices;

            var segmentCount = p.Closed ? n : Math.Max(0, n - 1);
            for (var i = 0; i < segmentCount; i++)
            {
                var j = (i + 1) % n;
                var a = p.GetPoint2dAt(i);
                var b = p.GetPoint2dAt(j);
                if (Near(a, b, tolerance))
                {
                    zeroSegments.Add(i);
                    duplicates.Add(j == 0 ? i : j);
                }
            }

            if (!hasBulges && !hasVariableWidths && n >= (p.Closed ? 3 : 3))
            {
                var start = p.Closed ? 0 : 1;
                var end = p.Closed ? n : n - 1;
                for (var i = start; i < end; i++)
                {
                    var prev = (i - 1 + n) % n;
                    var next = (i + 1) % n;
                    if (Near(p.GetPoint2dAt(prev), p.GetPoint2dAt(i), tolerance) ||
                        Near(p.GetPoint2dAt(i), p.GetPoint2dAt(next), tolerance))
                        continue;
                    if (CollinearAndBetween(p.GetPoint2dAt(prev), p.GetPoint2dAt(i), p.GetPoint2dAt(next), tolerance))
                        collinear.Add(i);
                }
            }

            double? length = null;
            try { length = p.Length; } catch { }
            double? area = null;
            if (p.Closed)
            {
                try { area = p.Area; } catch { }
            }

            double? constantWidth = null;
            try { constantWidth = p.ConstantWidth; } catch { }

            return new
            {
                handle = p.ObjectId.IsNull ? null : p.Handle.ToString(),
                layer = p.Layer,
                closed = p.Closed,
                vertex_count = n,
                segment_count = segmentCount,
                length,
                area,
                elevation = p.Elevation,
                constant_width = constantWidth,
                has_bulges = hasBulges,
                has_nonzero_segment_widths = hasVariableWidths,
                clean_supported = !hasBulges && !hasVariableWidths && n >= (p.Closed ? 3 : 2),
                duplicate_vertex_indices = duplicates.Distinct().OrderBy(idx => idx).ToArray(),
                zero_length_segment_indices = zeroSegments.ToArray(),
                collinear_vertex_indices = collinear.ToArray(),
                tolerance
            };
        }

        public static object Inspect(Database db, Transaction tr, string handle, double tolerance)
        {
            var p = PolylineFromHandle(db, tr, handle, OpenMode.ForRead);
            return InspectPolyline(p, tolerance);
        }
    }
}
