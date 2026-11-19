using System;
using System.Collections.Generic;
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

        [CommandMethod("YGLMERGE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Merge() => PromptAndExecute("merge");

        [CommandMethod("YGLONOFF", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Visibility() => PromptAndExecute("off");

        [CommandMethod("YGLFREEZE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Freeze() => PromptAndExecute("freeze");

        [CommandMethod("YGLLOCK", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Lock() => PromptAndExecute("lock");

        private static void PromptAndExecute(string operation)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var opLabel = operation == "merge" ? "合并" :
                    operation == "off" ? "关闭/显示" :
                    operation == "freeze" ? "冻结/解冻" : "锁定/解锁";

                var sources = SelectLayersOrNested(doc,
                    $"\n框选或点选要{opLabel}的对象 [点选块/外参内部图层(N)]：");
                if (sources.Length == 0) return;

                if (operation == "merge")
                {
                    var target = PickLayer(doc, "点击目标图层上的对象（支持块/外参）");
                    if (string.IsNullOrWhiteSpace(target)) return;

                    var filtered = sources.Where(s => !string.Equals(s, target, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (filtered.Length == 0)
                    {
                        ed.WriteMessage("\n[YangCAD] 所选对象均已在目标图层 " + target + " 中。\n");
                        return;
                    }

                    var results = CadTransactionRunner.WriteContext(doc, false, ctx =>
                        filtered.Select(src => LayerQuickService.Merge(ctx, src, target, false)).ToArray());
                    var movedTotal = results.Sum(r => r.moved);
                    ed.WriteMessage($"\n[YangCAD] 已将 {filtered.Length} 个图层（共 {movedTotal} 个对象）合并到 {target}。\n");
                    ed.Regen();
                    return;
                }

                var currentLayers = CadTransactionRunner.ReadContext(doc, LayerQuickService.List)
                    .ToDictionary(x => x.name, StringComparer.OrdinalIgnoreCase);

                var skipped = new List<string>();
                var changed = new List<LayerQuickService.Result>();
                CadTransactionRunner.WriteContext(doc, false, ctx =>
                {
                    foreach (var src in sources)
                    {
                        if (!currentLayers.TryGetValue(src, out var info)) continue;
                        var nextVal = operation == "off" ? !info.off : operation == "freeze" ? !info.frozen : !info.locked;
                        if (info.current && nextVal && operation != "lock")
                        {
                            skipped.Add(src + "(当前图层)");
                            continue;
                        }
                        changed.Add(LayerQuickService.State(ctx, src, operation, nextVal, false));
                    }
                    return changed.Count;
                });

                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                foreach (var r in changed)
                    ed.WriteMessage($"\n[YangCAD] {r.source_layer}：{r.message}");
                if (skipped.Count > 0)
                    ed.WriteMessage("\n[YangCAD] 已跳过：" + string.Join("、", skipped));
                ed.WriteMessage("\n");
                if (changed.Count > 0) ed.Regen();
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("Layer quick command failed.", ex);
                ed.WriteMessage("\n[YangCAD] 图层操作失败：" + ex.Message + "\n");
            }
        }

        private static string[] SelectLayersOrNested(Autodesk.AutoCAD.ApplicationServices.Document doc, string prompt)
        {
            var ed = doc.Editor;
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                return ExtractLayerNames(doc, implied.Value.GetObjectIds());

            var pickNested = false;
            var opts = new PromptSelectionOptions { MessageForAdding = prompt };
            opts.Keywords.Add("N", "N", "点选块内图层(N)");
            opts.KeywordInput += (_, args) =>
            {
                if (string.Equals(args.Input, "N", StringComparison.OrdinalIgnoreCase))
                {
                    pickNested = true;
                    throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.OK);
                }
            };

            PromptSelectionResult sel;
            try
            {
                sel = ed.GetSelection(opts);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) when (pickNested)
            {
                var nested = PickLayer(doc);
                return string.IsNullOrWhiteSpace(nested) ? Array.Empty<string>() : new[] { nested };
            }

            if (pickNested)
            {
                var nested = PickLayer(doc);
                return string.IsNullOrWhiteSpace(nested) ? Array.Empty<string>() : new[] { nested };
            }
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
                return Array.Empty<string>();
            return ExtractLayerNames(doc, sel.Value.GetObjectIds());
        }

        private static string[] ExtractLayerNames(Autodesk.AutoCAD.ApplicationServices.Document doc, ObjectId[] ids)
        {
            return CadTransactionRunner.Read(doc, tr =>
                ids.Select(id => tr.GetObject(id, OpenMode.ForRead, false))
                   .OfType<Entity>()
                   .Select(e => e.Layer)
                   .Where(n => !string.IsNullOrWhiteSpace(n))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray());
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
