using System;
using System.IO;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.AutoCADHost.UI;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class ToolboxCommands
    {
        [CommandMethod("YANGCAD_TOOLBOX")]
        public void OpenToolbox()
        {
            try
            {
                RuntimeHost.EnsureInitialized();
                ToolboxPaletteManager.Show();
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YANGCAD_TOOLBOX failed.", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[YangCAD] 工具箱启动失败：" + ex.Message + "\n");
            }
        }

        [CommandMethod("YY")]
        public void OpenToolboxShort() => OpenToolbox();

        [CommandMethod("YGREVIT", CommandFlags.Modal)]
        public void RevitPrepare()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var result = CadTransactionRunner.ReadContext(doc, ctx =>
                    RevitExchangeService.Export(ctx.Database, ctx.Transaction, doc.Name, new RevitExchangeService.Request()));
                ed.WriteMessage(
                    $"\n[YangCAD] 已生成 Revit 图纸副本：{result.output_dwg}" +
                    $"\n  已整理对象：{result.classified_entities} 个；跳过：{result.unsupported_count} 个；原图未修改。\n");
                ToolboxPaletteManager.ShowRevitTaskResult(result);
                if (result.warnings.Length > 0)
                    ed.WriteMessage("  注意：" + string.Join("；", result.warnings.Take(4)) + "\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGREVIT failed.", ex);
                ed.WriteMessage("\n[YangCAD] Revit 预处理失败：" + ex.Message + "\n");
            }
        }

        [CommandMethod("YGREFEMBED", CommandFlags.Modal)]
        public void EmbedReferences()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var options = new PromptKeywordOptions("\n引用内嵌 <当前图纸>：") { AllowNone = true };
            options.Keywords.Add("C", "C", "当前图纸(C)");
            options.Keywords.Add("B", "B", "批量文件夹(B)");
            options.Keywords.Default = "C";
            var mode = doc.Editor.GetKeywords(options);
            if (mode.Status == PromptStatus.None || (mode.Status == PromptStatus.OK && mode.StringResult == "C"))
                EmbedCurrentDrawing();
            else if (mode.Status == PromptStatus.OK && mode.StringResult == "B")
                EmbedFolderBatch();
        }

        [CommandMethod("YGEMBED", CommandFlags.Modal)]
        public void EmbedCurrentDrawing()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                if (string.IsNullOrWhiteSpace(doc.Name) || !File.Exists(doc.Name))
                {
                    ed.WriteMessage("\n[YangCAD] 请先将当前图纸保存到磁盘，再执行 YGEMBED。\n");
                    return;
                }
                if (Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0)
                {
                    ed.WriteMessage("\n[YangCAD] 当前图纸有未保存修改，请先 Ctrl+S 保存后再执行 YGEMBED。\n");
                    return;
                }
                ed?.WriteMessage("\n[YangCAD] 引用内嵌会暂时占用并清空系统剪贴板。");
                var result = ImageEmbedBatchService.Start(new ImageEmbedBatchService.Request
                {
                    InputPath = doc.Name,
                    IncludeSubfolders = true,
                    BindDrawingXrefs = true,
                    RequireAllImages = true
                });
                ToolboxPaletteManager.TrackEmbed(result);
                ed.WriteMessage(
                    $"\n[YangCAD] 已启动当前图纸引用内嵌（共 {result.file_count} 份，原图不覆盖）：" +
                    $"\n  输出目录：{result.output_folder}" +
                    $"\n  进度文件：{result.status_path}\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGEMBED failed.", ex);
                ed.WriteMessage("\n[YangCAD] 当前图纸内嵌启动失败：" + ex.Message + "\n");
            }
        }

        [CommandMethod("YGEMBEDBATCH", CommandFlags.Modal)]
        public void EmbedFolderBatch()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            try
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要批量内嵌图片与外参的 DWG 文件夹（含子文件夹）",
                    ShowNewFolderButton = false
                })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    ed?.WriteMessage("\n[YangCAD] 引用内嵌会暂时占用并清空系统剪贴板。");
                    var result = ImageEmbedBatchService.Start(new ImageEmbedBatchService.Request
                    {
                        InputPath = dialog.SelectedPath,
                        IncludeSubfolders = true,
                        BindDrawingXrefs = true,
                        RequireAllImages = true
                    });
                    ToolboxPaletteManager.TrackEmbed(result);
                    ed?.WriteMessage(
                        $"\n[YangCAD] 已启动文件夹批量内嵌（待处理 DWG：{result.file_count} 份，原图不覆盖）：" +
                        $"\n  输出目录：{result.output_folder}" +
                        $"\n  进度文件：{result.status_path}\n");
                }
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGEMBEDBATCH failed.", ex);
                ed?.WriteMessage("\n[YangCAD] 批量内嵌启动失败：" + ex.Message + "\n");
            }
        }
    }
}
