using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Rebuild an existing LINE as one native, editable zig-zag polyline. The original
    /// endpoints and drawing traits are preserved. All writes share the caller's CAD
    /// transaction so the user can undo a conversion with one native U operation.
    /// </summary>
    public static class BreaklineService
    {
        private const double Tolerance = 1e-8;

        public sealed class Request
        {
            public ObjectId LineId { get; set; }
            public Point3d PickPoint { get; set; }
            // Zero means automatic sizing relative to the selected line length.
            public double SymbolWidth { get; set; }
        }

        public sealed class Result
        {
            public bool success { get; set; }
            public bool dry_run { get; set; }
            public string original_handle { get; set; } = "";
            public string new_handle { get; set; } = "";
            public string entity_type { get; set; } = "";
            public double symbol_width { get; set; }
            public string undo_semantics { get; set; } = "single_transaction_expected";
        }

        public static Result Convert(CadExecutionContext ctx, Request request, bool dryRun)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.LineId.IsNull) throw new ArgumentException("请选择一条直线。");
            if (double.IsNaN(request.SymbolWidth) || double.IsInfinity(request.SymbolWidth) || request.SymbolWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(request.SymbolWidth), "折断符号宽度不能为负数或非有限数。");

            var tr = ctx.Transaction;
            var source = tr.GetObject(request.LineId, OpenMode.ForRead, false) as Line
                ?? throw new ArgumentException("目前仅支持普通 LINE 直线。");
            if (source.IsErased || source.OwnerId != ctx.Database.CurrentSpaceId)
                throw new InvalidOperationException("请在当前模型空间或布局空间选取独立直线，暂不修改块定义或外参。");
            var layer = (LayerTableRecord)tr.GetObject(source.LayerId, OpenMode.ForRead);
            if (layer.IsLocked || layer.IsFrozen || layer.IsOff)
                throw new InvalidOperationException("直线所在图层已锁定、冻结或关闭，请先恢复该图层。");

            var a = source.StartPoint;
            var b = source.EndPoint;
            var delta = b - a;
            var length = delta.Length;
            if (length <= Tolerance || double.IsInfinity(length) || double.IsNaN(length))
                throw new ArgumentException("直线长度过短或坐标异常，无法插入折断符号。");
            var unit = delta / length;
            var projection = (request.PickPoint - a).DotProduct(unit) / length;
            // Keep the break fully clear of both endpoints, even if the click lands there.
            if (double.IsNaN(projection) || double.IsInfinity(projection)) projection = 0.5;
            projection = Math.Max(0.2, Math.Min(0.8, projection));
            var center = a + delta * projection;
            var width = request.SymbolWidth > 0 ? request.SymbolWidth : length * 0.14;
            width = Math.Min(width, Math.Min(projection, 1 - projection) * length * 1.6);
            if (width <= Tolerance) throw new InvalidOperationException("直线太短，无法绘制折断符号。");

            // XY-plane first; vertical/3D lines get a stable fallback plane and a 3D polyline.
            var sideways = Vector3d.ZAxis.CrossProduct(unit);
            if (sideways.Length <= Tolerance) sideways = Vector3d.YAxis.CrossProduct(unit);
            sideways = sideways.GetNormal();
            var half = width * 0.5;
            var rise = width * 0.30;
            var vertices = new[]
            {
                a,
                center - unit * half,
                center - unit * (half * 0.35) + sideways * rise,
                center + unit * (half * 0.35) - sideways * rise,
                center + unit * half,
                b
            };

            Entity replacement;
            if (Math.Abs(a.Z - b.Z) <= Tolerance && Math.Abs(a.Z - center.Z) <= Tolerance)
            {
                var poly = new Polyline(vertices.Length) { Closed = false, Elevation = a.Z };
                for (var i = 0; i < vertices.Length; i++)
                    poly.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y), 0, 0, 0);
                replacement = poly;
            }
            else
            {
                replacement = new Polyline3d(Poly3dType.SimplePoly, new Point3dCollection(vertices), false);
            }
            var result = new Result
            {
                success = true,
                dry_run = dryRun,
                original_handle = source.Handle.ToString(),
                entity_type = replacement is Polyline ? "LWPOLYLINE" : "POLYLINE3D",
                symbol_width = width
            };
            try
            {
                replacement.SetPropertiesFrom(source);
                if (dryRun) return result;
                var space = (BlockTableRecord)tr.GetObject(ctx.Database.CurrentSpaceId, OpenMode.ForWrite);
                space.AppendEntity(replacement);
                tr.AddNewlyCreatedDBObject(replacement, true);
                result.new_handle = replacement.Handle.ToString();
                source.UpgradeOpen();
                source.Erase();
                return result;
            }
            finally
            {
                if (replacement.ObjectId.IsNull) replacement.Dispose();
            }
        }
    }
}
