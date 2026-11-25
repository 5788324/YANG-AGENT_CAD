using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static partial class YqHatchToolsService
    {
        // A null result leaves the source hatch intact. An empty result is a proven empty intersection.
        internal static List<Hatch>? ClipForBoundary(Transaction tr, BlockTableRecord space, Hatch hatch,
            PolygonBoundary2d boundary)
        {
            if (hatch.Associative || Math.Abs(hatch.Normal.DotProduct(Vector3d.ZAxis)) < 0.999999 ||
                hatch.HatchStyle != HatchStyle.Normal)
                return null;

            for (var i = 0; i < hatch.NumberOfLoops; i++)
            {
                var loop = hatch.GetLoopAt(i);
                if (!loop.IsPolyline && (loop.Curves == null ||
                    loop.Curves.Cast<Curve2d>().Any(curve => !(curve is LineSegment2d) && !(curve is CircularArc2d))))
                    return null;
            }

            var localOrigin = ComputeHatchLocalOrigin2d(hatch);
            using var polyline = boundary.CreatePolyline(0);
            using var projected = ProjectWcsPolylineToHatchPlane(polyline, hatch.Normal, hatch.Elevation, localOrigin);
            if (projected == null) return null;
            using var region = CreateRegionFromCurve(projected);
            if (region == null ||
                !TryBuildHatchCompositeRegion(hatch, localOrigin, out var intersection, requireAllLoops: true) ||
                intersection == null)
                return null;
            using (intersection)
            {
                if (!TryApplyClipBoolean(intersection, BooleanOperationType.BoolIntersect, region))
                    return null;
                if (intersection.Area == 0)
                    return new List<Hatch>();
                if (intersection.Area < AreaTol)
                    return null;
                if (!TryPopulateHatchFromRegion(tr, space, hatch, intersection, localOrigin,
                    groupIntoSingleHatch: true, updateInPlace: null, out var parts,
                    requireCompleteResult: true, attachToSpace: false)) return null;

                var expected = intersection.Area;
                if (Math.Abs(parts.Sum(piece => piece.Area) - expected) <= Math.Max(AreaTol, expected * 1e-7))
                    return parts;
                foreach (var piece in parts) piece.Dispose();
                return null;
            }
        }
    }
}
