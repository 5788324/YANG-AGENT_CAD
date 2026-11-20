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
                var boundary = ReadBoundary(doc);
                if (boundary == null) return;
                var request = new OutsideBoundaryService.Request
                {
                    Vertices = boundary.Vertices,
                    BoundaryId = boundary.SourceId
                };
                var result = CadTransactionRunner.WriteContext(doc, false,
                    ctx => OutsideBoundaryService.Execute(ctx, request));
                if (result.erased + result.trimmed == 0)
                {
                    ed.WriteMessage("\n[YangCAD] 没有需要删除或修剪的图元。");
                    return;
                }

                ed.WriteMessage($"\n[YangCAD] 已删除 {result.erased} 个、修剪 {result.trimmed} 个，" +
                    $"跳过 {result.skipped} 个。按 U 撤销。\n");
                foreach (var warning in result.warnings) ed.WriteMessage("  - " + warning + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGOUTSIDE failed.", ex);
                ed.WriteMessage("\n[YangCAD] 删除边界外失败：" + ex.Message + "\n");
            }
        }

        private static BoundaryInput? ReadBoundary(Document doc)
        {
            var ed = doc.Editor;
            var options = new PromptPointOptions("\n指定保留矩形的第一个角点：");
            options.Keywords.Add("Select", "Select", "选闭合多段线(Select)");
            options.Keywords.Add("Draw", "Draw", "逐点绘制(Draw)");
            var first = ed.GetPoint(options);
            if (first.Status == PromptStatus.Keyword)
                return first.StringResult == "Select" ? SelectBoundary(doc) : DrawBoundary(ed);
            if (first.Status != PromptStatus.OK) return null;
            var opposite = ed.GetCorner(new PromptCornerOptions("\n对角点：", first.Value));
            if (opposite.Status != PromptStatus.OK) return null;
            var input = new BoundaryInput();
            var corners = PolygonBoundary2d.RectangleCornersInWorld(first.Value, opposite.Value, ed.CurrentUserCoordinateSystem);
            foreach (var corner in corners) input.Vertices.Add(new Point2d(corner.X, corner.Y));
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
            var ucs = ed.CurrentUserCoordinateSystem;
            PolygonBoundary2d.RequireHorizontalUcs(ucs);
            var firstWorld = first.Value.TransformBy(ucs);
            var input = new BoundaryInput();
            input.Vertices.Add(new Point2d(firstWorld.X, firstWorld.Y));
            var last = first.Value;
            while (input.Vertices.Count < 128)
            {
                var opt = new PromptPointOptions("\n下一顶点（回车闭合，Esc 取消）：") {
                    AllowNone = true, UseBasePoint = true, BasePoint = last
                };
                var next = ed.GetPoint(opt);
                if (next.Status == PromptStatus.None) break;
                if (next.Status != PromptStatus.OK) return null;
                var world = next.Value.TransformBy(ucs);
                if (Math.Abs(world.Z - firstWorld.Z) > 1e-7)
                    throw new ArgumentException("手绘边界各顶点必须位于同一 XY 标高。");
                input.Vertices.Add(new Point2d(world.X, world.Y));
                last = next.Value;
            }
            if (input.Vertices.Count < 3)
                throw new ArgumentException("边界至少需要三个不同顶点。");
            return input;
        }
    }
}
