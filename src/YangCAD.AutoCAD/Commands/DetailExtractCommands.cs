using System;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class DetailExtractCommands
    {
        [CommandMethod("YGDETAIL", CommandFlags.Modal)]
        public void ExtractDetail()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var settings = UserSettings.Load();
                var defaultFactor = settings.DetailScaleFactor > 0 ? settings.DetailScaleFactor : 1.0;

                ed.WriteMessage("\n[YangCAD] 框出需要提取的大样范围（原图保留不动）。\n");
                var start = new PromptPointOptions("\n大样裁切框第一个角点：");
                start.Keywords.Add("S", "S", "选闭合多段线(S)");
                start.Keywords.Add("D", "D", "逐点多边形(D)");
                var first = ed.GetPoint(start);
                if (first.Status == PromptStatus.Keyword)
                {
                    PolygonDetailCommands.ExtractDetail(doc, first.StringResult == "S" ? "Select" : "Draw");
                    return;
                }
                if (first.Status != PromptStatus.OK) return;
                var secondOpt = new PromptCornerOptions("\n对角点：", first.Value);
                var second = ed.GetCorner(secondOpt);
                if (second.Status != PromptStatus.OK) return;
                var destination = ed.GetPoint("\n在图中点一下大样放置位置：");
                if (destination.Status != PromptStatus.OK) return;
                var multiplier = new PromptDoubleOptions($"\n大样放大倍数 <{defaultFactor:0.####}>（回车直接沿用）：")
                {
                    DefaultValue = defaultFactor, UseDefaultValue = true, AllowZero = false, AllowNegative = false
                };
                var factor = ed.GetDouble(multiplier);
                if (factor.Status != PromptStatus.OK && factor.Status != PromptStatus.None) return;
                var chosenFactor = factor.Status == PromptStatus.None ? defaultFactor : factor.Value;

                var ucs = ed.CurrentUserCoordinateSystem;
                var corners = PolygonBoundary2d.RectangleCornersInWorld(first.Value, second.Value, ucs);
                var destinationWorld = destination.Value.TransformBy(ucs);
                var edge = (corners[1] - corners[0]).GetNormal();
                int copied, skipped;
                string[] warnings;
                if (Math.Abs(edge.X) <= 1e-10 || Math.Abs(edge.Y) <= 1e-10)
                {
                    var request = new DetailExtractService.Request
                    {
                        FirstCorner = corners[0], OppositeCorner = corners[2],
                        Destination = destinationWorld,
                        Factor = chosenFactor,
                        KeepDimensionsAndLeaders = true, DrawFrame = true
                    };
                    var result = CadTransactionRunner.WriteContext(doc, false, ctx =>
                        DetailExtractService.Extract(ctx, request, false));
                    copied = result.copied_entities;
                    skipped = result.skipped_sources;
                    warnings = result.warnings;
                }
                else
                {
                    var request = new PolygonDetailExtractService.Request
                    {
                        Vertices = corners.Select(point => new Point2d(point.X, point.Y)).ToArray(),
                        BoundaryElevation = corners[0].Z,
                        PlacementAnchor = corners[0],
                        Destination = destinationWorld,
                        Factor = chosenFactor,
                        KeepDimensionsAndLeaders = true, DrawFrame = true
                    };
                    var result = CadTransactionRunner.WriteContext(doc, false, ctx =>
                        PolygonDetailExtractService.Extract(ctx, request, false));
                    copied = result.copied_entities;
                    skipped = result.skipped_sources;
                    warnings = result.warnings;
                }

                settings.DetailScaleFactor = chosenFactor;
                try { settings.Save(); } catch { }

                ed.WriteMessage($"\n[YangCAD] 大样已提取：{copied} 个图元（放大 {chosenFactor:0.####} 倍" +
                    (skipped > 0 ? $"，跳过 {skipped} 个无法裁切对象" : "") +
                    "）。\n");
                foreach (var warning in warnings) ed.WriteMessage("  - " + warning + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGDETAIL failed.", ex);
                ed.WriteMessage("\n[YangCAD] 提取大样失败，已撤销本次修改：" + ex.Message + "\n");
            }
        }
    }
}
