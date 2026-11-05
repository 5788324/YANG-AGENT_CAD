using System;
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
                ed.WriteMessage("\n[YangCAD] 框出需要提取的大样范围，原图保留不动。\n");
                var first = ed.GetPoint("\n大样裁切框第一个角点：");
                if (first.Status != PromptStatus.OK) return;
                var secondOpt = new PromptCornerOptions("\n对角点：", first.Value);
                var second = ed.GetCorner(secondOpt);
                if (second.Status != PromptStatus.OK) return;
                var destination = ed.GetPoint("\n在图中点一下大样放置位置：");
                if (destination.Status != PromptStatus.OK) return;
                var multiplier = new PromptDoubleOptions("\n大样放大倍数 <1>（回车不放大）：")
                {
                    DefaultValue = 1, UseDefaultValue = true, AllowZero = false, AllowNegative = false
                };
                var factor = ed.GetDouble(multiplier);
                if (factor.Status != PromptStatus.OK && factor.Status != PromptStatus.None) return;
                var request = new DetailExtractService.Request
                {
                    FirstCorner = first.Value, OppositeCorner = second.Value,
                    Destination = destination.Value,
                    Factor = factor.Status == PromptStatus.None ? 1 : factor.Value,
                    KeepDimensionsAndLeaders = true, DrawFrame = true
                };
                var result = CadTransactionRunner.WriteContext(doc, false, ctx =>
                    DetailExtractService.Extract(ctx, request, false));
                ed.WriteMessage($"\n[YangCAD] 大样已提取：{result.copied_entities} 个图元；" +
                    $"精确裁切 {result.clipped_sources} 个来源；跳过 {result.skipped_sources} 个；原图未删除。\n");
                foreach (var warning in result.warnings) ed.WriteMessage("  - " + warning + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGDETAIL failed.", ex);
                ed.WriteMessage("\n[YangCAD] 提取大样失败，已撤销本次修改：" + ex.Message + "\n");
            }
        }
    }
}
