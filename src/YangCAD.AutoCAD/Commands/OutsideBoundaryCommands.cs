using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class OutsideBoundaryCommands
    {
        private sealed class BoundaryInput
        {
            public List<Point2d> Vertices { get; } = new List<Point2d>();
            public ObjectId SourceId { get; set; } = ObjectId.Null;
        }

        [CommandMethod("YGOUTSIDE", CommandFlags.Modal)]
        public void RemoveOutside()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                ed.WriteMessage("\n[YangCAD] 删除/修剪边界外的图元，先只读预览，再确认一次。\n");
                var opts = new PromptKeywordOptions(
                    "\n边界方式 [Rectangle(矩形)/Select(已有闭合多段线)/Draw(逐点绘制)] <Rectangle>：");
                opts.Keywords.Add("Rectangle");
                opts.Keywords.Add("Select");
                opts.Keywords.Add("Draw");
                opts.Keywords.Default = "Rectangle";
                opts.AllowNone = true;
                var mode = ed.GetKeywords(opts);
                if (mode.Status != PromptStatus.OK && mode.Status != PromptStatus.None) return;
                var choice = mode.Status == PromptStatus.None ? "Rectangle" : mode.StringResult;
                BoundaryInput? boundary = string.Equals(choice, "Select", StringComparison.OrdinalIgnoreCase)
                    ? SelectBoundary(doc)
                    : string.Equals(choice, "Draw", StringComparison.OrdinalIgnoreCase)
                        ? DrawBoundary(ed) : RectangleBoundary(ed);
                if (boundary == null) return;
                // Reject invalid/self-intersecting polygons before traversing the DWG.
                var validated = new PolygonBoundary2d(boundary.Vertices);
                var request = new OutsideBoundaryService.Request {
                    Vertices = boundary.Vertices, BoundaryId = boundary.SourceId
                };
                var preview = CadTransactionRunner.ReadContext(doc,
                    ctx => OutsideBoundaryService.Preview(ctx, request));
                ed.WriteMessage(
                    "\n[YGOUTSIDE 只读预览] 边界 " + validated.Count +
                    " 条直边；扫描 " + preview.scanned +
                    "；待删除 " + preview.erased +
                    "；待修剪 " + preview.trimmed +
                    "；新曲线段 " + preview.new_fragments +
                    "；安全跳过 " + preview.skipped + "。\n");
                foreach (var warning in preview.warnings)
                    ed.WriteMessage("  - " + warning + "\n");

                if (preview.erased + preview.trimmed == 0)
                {
                    ed.WriteMessage("\n[YangCAD] 没有符合条件的修改，图纸未更改。\n");
                    return;
                }
                var confirm = new PromptKeywordOptions(
                    "\n确认删除以上边界外图元并精确修剪跨界曲线？[Yes(确认)/No(取消)] <No>：");
                confirm.Keywords.Add("Yes");
                confirm.Keywords.Add("No");
                confirm.Keywords.Default = "No";
                confirm.AllowNone = true;
                var answer = ed.GetKeywords(confirm);
                if (answer.Status != PromptStatus.OK ||
                    !string.Equals(answer.StringResult, "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n[YangCAD] 已取消；没有修改任何图元。\n");
                    return;
                }

                var result = CadTransactionRunner.WriteContext(doc, false,
                    ctx => OutsideBoundaryService.Execute(ctx, request, preview.fingerprint));
                ed.WriteMessage("\n[YangCAD] 已执行：删除 " + result.erased +
                    "、修剪 " + result.trimmed +
                    "、创建内部曲线段 " + result.new_fragments +
                    "。请用原生 _.U 验证撤销；本版本不声明未测的 Undo PASS。\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGOUTSIDE failed.", ex);
                ed.WriteMessage("\n[YangCAD] 删除边界外失败：事务未正常完成；" +
                                "如有疑问请检查原图与 Undo。" + ex.Message + "\n");
            }
        }

        private static BoundaryInput? RectangleBoundary(Editor ed)
        {
            var first = ed.GetPoint("\n矩形边界第一个角点：");
            if (first.Status != PromptStatus.OK) return null;
            var opposite = ed.GetCorner(new PromptCornerOptions("\n对角点：", first.Value));
            if (opposite.Status != PromptStatus.OK) return null;
            double l = Math.Min(first.Value.X, opposite.Value.X);
            double r = Math.Max(first.Value.X, opposite.Value.X);
            double b = Math.Min(first.Value.Y, opposite.Value.Y);
            double t = Math.Max(first.Value.Y, opposite.Value.Y);
            var input = new BoundaryInput();
            input.Vertices.Add(new Point2d(l, b));
            input.Vertices.Add(new Point2d(r, b));
            input.Vertices.Add(new Point2d(r, t));
            input.Vertices.Add(new Point2d(l, t));
            return input;
        }

        private static BoundaryInput? SelectBoundary(Document doc)
        {
            var ed = doc.Editor;
            var opt = new PromptEntityOptions("\n选择当前空间的闭合直边 LWPOLYLINE：");
            opt.SetRejectMessage("\n请选择闭合二维多段线，而不是块或外部参照。");
            opt.AddAllowedClass(typeof(Polyline), true);
            var picked = ed.GetEntity(opt);
            if (picked.Status != PromptStatus.OK) return null;
            return CadTransactionRunner.ReadContext(doc, ctx =>
            {
                var poly = (Polyline)ctx.Transaction.GetObject(
                    picked.ObjectId, OpenMode.ForRead, false);
                if (poly.OwnerId != ctx.Database.CurrentSpaceId)
                    throw new InvalidOperationException("边界必须是当前模型或布局空间的独立对象。");
                if (!poly.Closed || poly.NumberOfVertices < 3)
                    throw new ArgumentException("边界必须闭合并有至少三个顶点。");
                if (poly.Normal.Length < 1e-8 ||
                    poly.Normal.GetNormal().DotProduct(Vector3d.ZAxis) < 0.999999)
                    throw new NotSupportedException("当前仅支持 WCS XY 平面边界。");
                if (poly.HasWidth)
                    throw new NotSupportedException("不能使用带线宽的多段线作为边界。");
                var input = new BoundaryInput { SourceId = picked.ObjectId };
                for (var i = 0; i < poly.NumberOfVertices; i++)
                {
                    if (Math.Abs(poly.GetBulgeAt(i)) > 1e-10)
                        throw new NotSupportedException(
                            "检测到 bulge 圆弧边界。此版本不作直线化近似，已安全取消。");
                    input.Vertices.Add(poly.GetPoint2dAt(i));
                }
                return input;
            });
        }

        private static BoundaryInput? DrawBoundary(Editor ed)
        {
            ed.WriteMessage("\n依次指定边界直线顶点，回车闭合；不在图上创建临时边界。\n");
            var first = ed.GetPoint("\n第一个顶点：");
            if (first.Status != PromptStatus.OK) return null;
            var input = new BoundaryInput();
            input.Vertices.Add(new Point2d(first.Value.X, first.Value.Y));
            var last = first.Value;
            while (input.Vertices.Count < 128)
            {
                var opt = new PromptPointOptions("\n下一顶点（回车闭合，Esc 取消）：") {
                    AllowNone = true, UseBasePoint = true, BasePoint = last
                };
                var next = ed.GetPoint(opt);
                if (next.Status == PromptStatus.None) break;
                if (next.Status != PromptStatus.OK) return null;
                if (Math.Abs(next.Value.Z - first.Value.Z) > 1e-7)
                    throw new ArgumentException("手绘边界各顶点必须位于同一 XY 标高。");
                input.Vertices.Add(new Point2d(next.Value.X, next.Value.Y));
                last = next.Value;
            }
            if (input.Vertices.Count < 3)
                throw new ArgumentException("边界至少需要三个不同顶点。");
            return input;
        }
    }
}
