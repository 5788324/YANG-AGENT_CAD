using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class LeaderRepairCommands
    {
        [CommandMethod("YGLEADERFIX", CommandFlags.Modal)]
        public void RebuildLeader()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                ed.WriteMessage("\n[YangCAD] 选择旧引线、炸开的连续 LINE 或开放 Polyline（一次只重建一条）。\n");
                var sourceSelection = ed.GetSelection(new PromptSelectionOptions
                { MessageForAdding = "\n请选择引线或连续线段：" });
                if (sourceSelection.Status != PromptStatus.OK || sourceSelection.Value == null) return;

                var textOption = new PromptEntityOptions("\n选择要关联的 TEXT/MTEXT（回车表示无文字）：");
                textOption.AllowNone = true;
                var textResult = ed.GetEntity(textOption);
                if (textResult.Status != PromptStatus.OK && textResult.Status != PromptStatus.None) return;
                var textId = textResult.Status == PromptStatus.OK ? textResult.ObjectId : Autodesk.AutoCAD.DatabaseServices.ObjectId.Null;

                var ids = sourceSelection.Value.GetObjectIds();
                string[] handles = Array.Empty<string>();
                string textHandle = "";
                CadTransactionRunner.Read(doc, tr =>
                {
                    handles = ids.Select(id => tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false))
                        .OfType<Autodesk.AutoCAD.DatabaseServices.Entity>()
                        .Select(e => e.Handle.ToString()).ToArray();
                    if (!textId.IsNull)
                        textHandle = ((Autodesk.AutoCAD.DatabaseServices.Entity)tr.GetObject(textId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead)).Handle.ToString();
                    return 0;
                });
                Autodesk.AutoCAD.Geometry.Point3d? arrow = null;
                if (string.IsNullOrWhiteSpace(textHandle))
                {
                    var point = ed.GetPoint("\n请选择箭头端点（用于确定引线方向）：");
                    if (point.Status != PromptStatus.OK) return;
                    arrow = point.Value;
                }

                var request = new LeaderRepairService.Request
                {
                    SourceHandles = handles,
                    TextHandle = textHandle,
                    ArrowPoint = arrow,
                    KeepOriginals = false
                };
                // Validate/plan before changing the drawing. No heuristic text matching.
                CadTransactionRunner.ReadContext(doc, ctx => LeaderRepairService.Rebuild(ctx, request, true));
                var result = CadTransactionRunner.WriteContext(doc, false, ctx => LeaderRepairService.Rebuild(ctx, request, false));
                ed.WriteMessage("\n[YangCAD] 已创建原生 MLeader，Handle：" + result.new_mleader_handle +
                    "；文字关联：" + (result.text_associated ? "是" : "否") +
                    "；原对象已在本次事务中替换。" +
                    "\n" + result.warning + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGLEADERFIX failed.", ex);
                ed.WriteMessage("\n[YangCAD] 引线修复失败，未提交本次修改：" + ex.Message + "\n");
            }
        }
    }
}
