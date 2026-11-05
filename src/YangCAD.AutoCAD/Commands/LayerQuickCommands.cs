using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.AutoCADHost.UI;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class LayerQuickCommands
    {
        [CommandMethod("YGLAYER", CommandFlags.Modal)]
        public void ShowTools()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                string selected = "";
                // The list is vital: once a layer is OFF/FROZEN it cannot be picked in the drawing.
                // The dialog must always permit restoring it without editing the DWG externally.
                for (var attempts = 0; attempts < 3; attempts++)
                {
                    var layers = CadTransactionRunner.ReadContext(doc, LayerQuickService.List);
                    var window = new LayerQuickWindow(layers, selected);
                    Application.ShowModalWindow(window);
                    if (window.DialogResult != true) return;
                    if (window.Operation == "pick")
                    {
                        selected = PickLayer(doc);
                        if (string.IsNullOrWhiteSpace(selected)) return;
                        continue;
                    }
                    Execute(doc, window.Operation, window.SourceName, window.TargetName, window.Value);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGLAYER failed.", ex);
                doc.Editor.WriteMessage("\n[YangCAD] 图层操作失败：" + ex.Message + "\n");
            }
        }

        [CommandMethod("YGLMERGE", CommandFlags.Modal)] public void Merge() => PromptAndExecute("merge");
        [CommandMethod("YGLONOFF", CommandFlags.Modal)] public void Visibility() => PromptAndExecute("off");
        [CommandMethod("YGLFREEZE", CommandFlags.Modal)] public void Freeze() => PromptAndExecute("freeze");
        [CommandMethod("YGLLOCK", CommandFlags.Modal)] public void Lock() => PromptAndExecute("lock");

        private static void PromptAndExecute(string operation)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                var source = PickLayer(doc);
                if (string.IsNullOrWhiteSpace(source)) return;
                if (operation == "merge")
                {
                    var target = PickLayer(doc, "选择目标图层上的对象");
                    if (string.IsNullOrWhiteSpace(target)) return;
                    Execute(doc, operation, source, target, false);
                    return;
                }
                var existing = CadTransactionRunner.ReadContext(doc, ctx => LayerQuickService.List(ctx)
                    .First(x => string.Equals(x.name, source, StringComparison.OrdinalIgnoreCase)));
                var value = operation == "off" ? !existing.off : operation == "freeze" ? !existing.frozen : !existing.locked;
                Execute(doc, operation, source, "", value);
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("Layer quick command failed.", ex);
                doc.Editor.WriteMessage("\n[YangCAD] 图层操作失败：" + ex.Message + "\n");
            }
        }

        private static void Execute(Autodesk.AutoCAD.ApplicationServices.Document doc, string operation, string source, string target, bool value)
        {
            var result = CadTransactionRunner.WriteContext(doc, false, ctx => operation == "merge"
                ? LayerQuickService.Merge(ctx, source, target, false)
                : LayerQuickService.State(ctx, source, operation, value, false));
            doc.Editor.WriteMessage($"\n[YangCAD] {result.source_layer}：{result.message}" +
                (operation == "merge" ? $" 已移动 {result.moved} 个对象到 {result.target_layer}；跳过 {result.skipped}。" : "") +
                "\n");
            doc.Editor.Regen();
        }

        private static string PickLayer(Autodesk.AutoCAD.ApplicationServices.Document doc, string prompt = "点击要操作的图元（支持块内部、DWG 外参图元）")
        {
            var ed = doc.Editor;
            var picked = ed.GetNestedEntity("\n" + prompt + "：");
            if (picked.Status != PromptStatus.OK) return "";
            var pair = CadTransactionRunner.Read(doc, tr =>
            {
                var entity = tr.GetObject(picked.ObjectId, OpenMode.ForRead) as Entity
                    ?? throw new InvalidOperationException("点击的不是可识别 CAD 对象。");
                var containers = picked.GetContainers();
                var names = containers.Select(id => tr.GetObject(id, OpenMode.ForRead, false)).OfType<BlockReference>().ToArray();
                var insertion = names.Length > 0 ? names[names.Length - 1].Layer : entity.Layer;
                if (names.Length == 0) return new[] { entity.Layer, entity.Layer, "false" };

                var table = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                var inner = entity.Layer;
                if (!table.Has(inner))
                {
                    // Xref entities can expose their original local layer name. Resolve the
                    // host's XREF|Layer symbol without opening/modifying the xref source DWG.
                    foreach (var br in names)
                    {
                        var block = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        var candidate = block?.Name + "|" + inner;
                        if (!string.IsNullOrWhiteSpace(candidate) && table.Has(candidate))
                        { inner = candidate; break; }
                    }
                }
                if (!table.Has(inner)) inner = insertion;
                return new[] { inner, insertion, "true" };
            });
            if (pair[2] == "false") return pair[0];
            ed.WriteMessage("\n[YangCAD] 此对象位于块/参照中。\n");
            var opts = new PromptKeywordOptions("\n操作 [内部对象图层(I)/整个块或参照插入图层(B)] <I>：") { AllowNone = true };
            opts.Keywords.Add("I");
            opts.Keywords.Add("B");
            var answer = ed.GetKeywords(opts);
            if (answer.Status == PromptStatus.Cancel) return "";
            return answer.Status == PromptStatus.OK &&
                   string.Equals(answer.StringResult, "B", StringComparison.OrdinalIgnoreCase) ? pair[1] : pair[0];
        }
    }
}
