using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class DetailExtractCommands
    {
        private sealed class PolygonInput
        {
            public List<Point2d> Vertices { get; set; } = new List<Point2d>();
            public ObjectId SourceId { get; set; } = ObjectId.Null;
            public double Elevation { get; set; }
        }

        [CommandMethod("YGDETAIL", CommandFlags.Modal)]
        public void ExtractDetail()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var mode = new PromptKeywordOptions(
                    "\n提取大样边界 [Rectangle(矩形)/Select(选择闭合多段线)/Draw(逐点绘制)] <Rectangle>：");
                mode.Keywords.Add("Rectangle");
                mode.Keywords.Add("Select");
                mode.Keywords.Add("Draw");
                mode.Keywords.Default = "Rectangle";
                mode.AllowNone = true;
                var selectedMode = ed.GetKeywords(mode);
                if (selectedMode.Status != PromptStatus.OK &&
                    selectedMode.Status != PromptStatus.None) return;
                var name = selectedMode.Status == PromptStatus.None
                    ? "Rectangle" : selectedMode.StringResult;

                if (string.Equals(name, "Rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    ExtractRectangle(doc);
                    return;
                }
                var input = string.Equals(name, "Select", StringComparison.OrdinalIgnoreCase)
                    ? SelectBoundary(doc) : DrawBoundary(ed);
                if (input == null) return;
                // Validation is required before any write; self-intersection and degenerate
                // polygons must never partially modify the drawing.
                var boundary = new PolygonBoundary2d(input.Vertices);
                ed.WriteMessage("\n边界确认：" + boundary.Count + " 条直边；原图不会删除。\n");
                var destination = ed.GetPoint("\n大样放置位置：");
                if (destination.Status != PromptStatus.OK) return;
                var factor = AskFactor(ed);
                if (!factor.HasValue) return;
                var request = new PolygonDetailExtractService.Request
                {
                    Vertices = input.Vertices,
                    BoundaryId = input.SourceId,
                    BoundaryElevation = input.Elevation,
                    Destination = destination.Value,
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
                RuntimeLogger.Error("YGDETAIL failed.", ex);
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

        private static void ExtractRectangle(Document doc)
        {
            var ed = doc.Editor;
            ed.WriteMessage("\n[YangCAD] 选择矩形大样，源图不会删除。\n");
            var first = ed.GetPoint("\n大样裁切框第一个角点：");
            if (first.Status != PromptStatus.OK) return;
            var secondOpt = new PromptCornerOptions("\n对角点：", first.Value);
            var second = ed.GetCorner(secondOpt);
            if (second.Status != PromptStatus.OK) return;
            var destination = ed.GetPoint("\n在图中点一下大样放置位置：");
            if (destination.Status != PromptStatus.OK) return;
            var factor = AskFactor(ed);
            if (!factor.HasValue) return;
            var request = new DetailExtractService.Request
            {
                FirstCorner = first.Value, OppositeCorner = second.Value,
                Destination = destination.Value, Factor = factor.Value,
                KeepDimensionsAndLeaders = true, DrawFrame = true
            };
            var result = CadTransactionRunner.WriteContext(doc, false,
                ctx => DetailExtractService.Extract(ctx, request, false));
            ed.WriteMessage("\n[YangCAD] 矩形大样完成：" + result.copied_entities +
                " 个图元；精确裁切 " + result.clipped_sources +
                " 个来源；跳过 " + result.skipped_sources + "；原图未删除。\n");
            foreach (var warning in result.warnings) ed.WriteMessage("  - " + warning + "\n");
        }

        private static PolygonInput SelectBoundary(Document doc)
        {
            var ed = doc.Editor;
            var opts = new PromptEntityOptions("\n选择已闭合的二维多段线（仅直线边界）：");
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
                if (!poly.Closed || poly.NumberOfVertices < 3)
                    throw new ArgumentException("请选择已闭合且至少三个顶点的多段线。");
                if (poly.Normal.GetNormal().DotProduct(Vector3d.ZAxis) < 0.999999)
                    throw new NotSupportedException("当前仅支持 WCS XY 平面的多段线边界。");
                if (poly.HasWidth)
                    throw new NotSupportedException("边界不能有线宽，请使用零宽度闭合多段线。");
                var points = new List<Point2d>();
                for (var i = 0; i < poly.NumberOfVertices; i++)
                {
                    if (Math.Abs(poly.GetBulgeAt(i)) > 1e-10)
                        throw new NotSupportedException("检测到 bulge 弧段边界；当前版本不作直线近似，请改用直边多段线。");
                    points.Add(poly.GetPoint2dAt(i));
                }
                return new PolygonInput {
                    Vertices = points, SourceId = picked.ObjectId, Elevation = poly.Elevation
                };
            });
        }

        private static PolygonInput DrawBoundary(Editor ed)
        {
            ed.WriteMessage("\n[YangCAD] 逐点指定闭合直边区域，至少三点，回车结束；不会在原位置绘制边界。\n");
            var first = ed.GetPoint("\n第一个顶点：");
            if (first.Status != PromptStatus.OK) return null;
            var input = new PolygonInput { Elevation = first.Value.Z };
            input.Vertices.Add(new Point2d(first.Value.X, first.Value.Y));
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
                if (Math.Abs(point.Value.Z - input.Elevation) > 1e-7)
                    throw new ArgumentException("绘制边界必须在同一 WCS XY 标高平面。");
                input.Vertices.Add(new Point2d(point.Value.X, point.Value.Y));
                last = point.Value;
            }
            if (input.Vertices.Count < 3)
                throw new ArgumentException("闭合多边形至少需要三个顶点。");
            return input;
        }
    }
}
