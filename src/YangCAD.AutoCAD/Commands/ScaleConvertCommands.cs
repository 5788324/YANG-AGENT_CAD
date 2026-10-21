using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.AutoCADHost.UI;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class ScaleConvertCommands
    {
        [CommandMethod("YGSCALE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void ScaleConvert()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;

            try
            {
                var selection = editor.SelectImplied();
                if (selection.Status != PromptStatus.OK || selection.Value == null || selection.Value.Count == 0)
                {
                    var options = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n请选择需要转换比例的详图对象："
                    };
                    selection = editor.GetSelection(options);
                }
                if (selection.Status != PromptStatus.OK || selection.Value == null || selection.Value.Count == 0) return;

                string[] handles = Array.Empty<string>();
                CadTransactionRunner.Read(document, tr =>
                {
                    handles = selection.Value.GetObjectIds()
                        .Select(id => tr.GetObject(id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead, false))
                        .OfType<Autodesk.AutoCAD.DatabaseServices.Entity>()
                        .Select(entity => entity.Handle.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return 0;
                });
                if (handles.Length == 0)
                {
                    editor.WriteMessage("\n[YangCAD] 没有可转换的 Entity。\n");
                    return;
                }

                var window = new ScaleConvertWindow(handles.Length);
                Application.ShowModalWindow(window);
                if (window.DialogResult != true) return;

                var basePoint = Point3d.Origin;
                if (window.SelectedMode == ScaleConversionService.DrawingMode.ScaledDrawing && window.PickBasePoint)
                {
                    var point = editor.GetPoint("\n请选择缩放基点：");
                    if (point.Status != PromptStatus.OK) return;
                    basePoint = point.Value;
                }

                var request = new ScaleConversionService.Request
                {
                    Handles = handles,
                    Mode = window.SelectedMode,
                    OldScale = window.OldScale,
                    NewScale = window.NewScale,
                    BasePoint = basePoint,
                    PreserveTextPaperSize = window.PreserveTextPaperSize,
                    PreserveDimensionPaperSize = window.PreserveDimensionPaperSize,
                    PreserveLeaderPaperSize = window.PreserveLeaderPaperSize,
                    PreserveHatchPaperScale = window.PreserveHatchPaperScale,
                    PreserveLinetypePaperScale = window.PreserveLinetypePaperScale
                };

                var result = CadTransactionRunner.WriteContext(document, false, context =>
                    ScaleConversionService.Convert(context, request, dryRun: false));

                editor.SetImpliedSelection(Array.Empty<Autodesk.AutoCAD.DatabaseServices.ObjectId>());
                var headline = result.processed > 0 ? "比例转换完成" : "没有对象被转换";
                editor.WriteMessage(
                    "\n[YangCAD] " + headline +
                    $"\n  模式：{(result.mode == "standard_1to1" ? "1:1 实尺寸" : "缩尺绘图")}" +
                    $"\n  比例：1:{result.old_scale:0.####} → 1:{result.new_scale:0.####}" +
                    $"\n  几何倍率：{result.geometry_factor:0.####}; 注释倍率：{result.annotation_factor:0.####}" +
                    $"\n  已处理：{result.processed}; 跳过：{result.skipped}; 失败：{result.failed}" +
                    "\n  本次操作使用单一事务；请用一次 Ctrl+Z 验证完整撤销。\n");

                if (result.issues.Length > 0)
                {
                    foreach (var group in result.issues.GroupBy(x => x.reason).Take(8))
                        editor.WriteMessage($"  - {group.Key}：{group.Count()}\n");
                }
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGSCALE failed.", ex);
                editor.WriteMessage("\n[YangCAD] 比例转换失败：" + ex.Message + "\n");
            }
        }
    }
}
