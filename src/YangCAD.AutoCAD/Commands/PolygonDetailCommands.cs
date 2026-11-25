using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class PolygonDetailCommands
    {
        private sealed class PolygonInput
        {
            public List<Point2d> Vertices { get; set; } = new List<Point2d>();
            public List<double>? Bulges { get; set; }
            public ObjectId SourceId { get; set; } = ObjectId.Null;
            public double Elevation { get; set; }
        }

        [CommandMethod("YGDETAILPOLY", CommandFlags.Modal)]
        public void ExtractDetail()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (doc != null) ExtractDetail(doc, null);
        }

        internal static void ExtractDetail(Document doc, string? initialMode)
        {
            var ed = doc.Editor;
            try
            {
                var name = initialMode;
                if (name == null)
                {
                    var mode = new PromptKeywordOptions(
                        "\n多边形大样 [Select(选择闭合多段线)/Draw(逐点绘制)] <Select>：");
                    mode.Keywords.Add("Select");
                    mode.Keywords.Add("Draw");
                    mode.Keywords.Default = "Select";
                    mode.AllowNone = true;
                    var selectedMode = ed.GetKeywords(mode);
                    if (selectedMode.Status != PromptStatus.OK &&
                        selectedMode.Status != PromptStatus.None) return;
                    name = selectedMode.Status == PromptStatus.None
                        ? "Select" : selectedMode.StringResult;
                }

                var input = string.Equals(name, "Select", StringComparison.OrdinalIgnoreCase)
                    ? SelectBoundary(doc) : DrawBoundary(ed);
                if (input == null) return;
                // Validation is required before any write; self-intersection and degenerate
                // polygons must never partially modify the drawing.
                var boundary = new PolygonBoundary2d(input.Vertices, input.Bulges);
                ed.WriteMessage("\n边界确认：" + boundary.Count + " 条边；原图不会删除。\n");
                var destination = ed.GetPoint("\n大样放置位置：");
                if (destination.Status != PromptStatus.OK) return;
                var factor = AskFactor(ed);
                if (!factor.HasValue) return;
                var request = new PolygonDetailExtractService.Request
                {
                    Vertices = input.Vertices,
                    Bulges = input.Bulges,
                    BoundaryId = input.SourceId,
                    BoundaryElevation = input.Elevation,
                    Destination = destination.Value.TransformBy(ed.CurrentUserCoordinateSystem),
                    Factor = factor.Value,
                    DrawFrame = true,
                    KeepDimensionsAndLeaders = true
                };
                var result = CadTransactionRunner.WriteContext(doc, false,
                    ctx => PolygonDetailExtractService.Extract(ctx, request, false));
                ed.WriteMessage("\n[YangCAD] 多边形大样完成：" + result.copied_entities +
                    " 个图元，裁切来源 " + result.clipped_sources +
                    "，跳过 " + result.skipped_sources + "；原图保留。\n");
                foreach (var warning in result.warnings)
                    ed.WriteMessage("  - " + warning + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGDETAILPOLY failed.", ex);
                ed.WriteMessage("\n[YangCAD] 操作取消或失败，事务未提交：" + ex.Message + "\n");
            }
        }

        private static double? AskFactor(Editor ed)
        {
            var opts = new PromptDoubleOptions("\n大样放大倍数 <1>（回车不放大）：")
            {
                DefaultValue = 1, UseDefaultValue = true,
                AllowZero = false, AllowNegative = false
            };
            var r = ed.GetDouble(opts);
            return (r.Status == PromptStatus.OK || r.Status == PromptStatus.None)
                ? (r.Status == PromptStatus.None ? 1 : r.Value) : (double?)null;
        }

        private static PolygonInput? SelectBoundary(Document doc)
        {
            var ed = doc.Editor;
            var opts = new PromptEntityOptions("\n选择已闭合的二维多段线（可含圆弧）：");
            opts.SetRejectMessage("\n请选择 LWPOLYLINE，不接受块、外参或普通 LINE。");
            opts.AddAllowedClass(typeof(Polyline), true);
            var picked = ed.GetEntity(opts);
            if (picked.Status != PromptStatus.OK) return null;
            return CadTransactionRunner.ReadContext(doc, ctx =>
            {
                var poly = (Polyline)ctx.Transaction.GetObject(
                    picked.ObjectId, OpenMode.ForRead, false);
                if (poly.OwnerId != ctx.Database.CurrentSpaceId)
                    throw new InvalidOperationException("只能选择当前空间的独立多段线。");
                if (!poly.Closed || poly.NumberOfVertices < 2)
                    throw new ArgumentException("请选择已闭合且至少两个顶点的多段线。");
                if (poly.Normal.GetNormal().DotProduct(Vector3d.ZAxis) < 0.999999)
                    throw new NotSupportedException("当前仅支持 WCS XY 平面的多段线边界。");
                if (poly.HasWidth)
                    throw new NotSupportedException("边界不能有线宽，请使用零宽度闭合多段线。");
                var points = new List<Point2d>();
                var bulges = new List<double>();
                for (var i = 0; i < poly.NumberOfVertices; i++)
                {
                    points.Add(poly.GetPoint2dAt(i));
                    bulges.Add(poly.GetBulgeAt(i));
                }
                return new PolygonInput {
                    Vertices = points, Bulges = bulges, SourceId = picked.ObjectId, Elevation = poly.Elevation
                };
            });
        }

        private static PolygonInput? DrawBoundary(Editor ed)
        {
            ed.WriteMessage("\n[YangCAD] 逐点指定闭合直边区域，至少三点，回车结束；不会在原位置绘制边界。\n");
            var first = ed.GetPoint("\n第一个顶点：");
            if (first.Status != PromptStatus.OK) return null;
            var ucs = ed.CurrentUserCoordinateSystem;
            PolygonBoundary2d.RequireHorizontalUcs(ucs);
            var firstWorld = first.Value.TransformBy(ucs);
            var input = new PolygonInput { Elevation = firstWorld.Z };
            input.Vertices.Add(new Point2d(firstWorld.X, firstWorld.Y));
            var last = first.Value;
            while (input.Vertices.Count < 128)
            {
                var opts = new PromptPointOptions("\n下一个顶点（回车结束并闭合，Esc 取消）：")
                {
                    AllowNone = true, UseBasePoint = true, BasePoint = last
                };
                var point = ed.GetPoint(opts);
                if (point.Status == PromptStatus.None) break;
                if (point.Status != PromptStatus.OK) return null;
                var world = point.Value.TransformBy(ucs);
                if (Math.Abs(world.Z - input.Elevation) > 1e-7)
                    throw new ArgumentException("绘制边界必须在同一 WCS XY 标高平面。");
                input.Vertices.Add(new Point2d(world.X, world.Y));
                last = point.Value;
            }
            if (input.Vertices.Count < 3)
                throw new ArgumentException("闭合多边形至少需要三个顶点。");
            return input;
        }
    }
}
