using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
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
                ed.WriteMessage("\n[YangCAD] 可直接框选炸开的引线和文字（会自动忽略实心箭头）；也可分步点选。\n");
                var sourceSelection = ed.GetSelection(new PromptSelectionOptions
                { MessageForAdding = "\n请框选引线或线段（可连带框选文字）：" });
                if (sourceSelection.Status != PromptStatus.OK || sourceSelection.Value == null) return;

                var ids = sourceSelection.Value.GetObjectIds();
                string[] handles = Array.Empty<string>();
                string textHandle = "";
                CadTransactionRunner.Read(doc, tr =>
                {
                    var entities = ids.Select(id => tr.GetObject(id, OpenMode.ForRead, false))
                        .OfType<Entity>()
                        .ToArray();
                    var texts = entities.Where(e => e is DBText || e is MText).ToArray();
                    if (texts.Length == 1)
                        textHandle = texts[0].Handle.ToString();

                    // Ignore Solid arrow fills and any text already captured above when building linework chain.
                    handles = entities
                        .Where(e => !(e is Solid) && !(e is DBText) && !(e is MText))
                        .Select(e => e.Handle.ToString())
                        .ToArray();
                    return 0;
                });

                if (handles.Length == 0)
                {
                    ed.WriteMessage("\n[YangCAD] 未选中有效的引线或线段。\n");
                    return;
                }

                if (string.IsNullOrWhiteSpace(textHandle))
                {
                    var textOption = new PromptEntityOptions("\n选择要关联的 TEXT/MTEXT（回车表示无文字）：");
                    textOption.AllowNone = true;
                    var textResult = ed.GetEntity(textOption);
                    if (textResult.Status != PromptStatus.OK && textResult.Status != PromptStatus.None) return;
                    var textId = textResult.Status == PromptStatus.OK ? textResult.ObjectId : ObjectId.Null;
                    if (!textId.IsNull)
                    {
                        CadTransactionRunner.Read(doc, tr =>
                        {
                            textHandle = ((Entity)tr.GetObject(textId, OpenMode.ForRead)).Handle.ToString();
                            return 0;
                        });
                    }
                }

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
                ed.WriteMessage("\n[YangCAD] 已创建原生 MLeader" +
                    "（文字关联：" + (result.text_associated ? "是" : "否") +
                    "），原线段已替换，支持 Ctrl+Z 撤销。\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGLEADERFIX failed.", ex);
                ed.WriteMessage("\n[YangCAD] 引线修复失败：" + ex.Message + "\n");
            }
        }
    }
}
