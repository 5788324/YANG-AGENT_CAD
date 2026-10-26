using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Toolbox;
using YangCAD.AutoCADHost.Services.Cad;
using System.IO;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ToolboxView : UserControl
    {
        private sealed class ToolCardItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string CategoryPath { get; set; } = "";
            public string DisplayStatus { get; set; } = "";
        }

        private readonly ToolboxRegistry _registry;
        private ToolboxToolDescriptor? _selected;

        public ToolboxView(ToolboxRegistry registry)
        {
            _registry = registry;
            InitializeComponent();
            ThemeManager.Apply(Resources, UserSettings.Load().Theme);
            RefreshTools();
        }

        public void ApplyTheme(string theme) => ThemeManager.Apply(Resources, theme);

        public void RefreshContext()
        {
            RefreshTools();
            if (_selected != null) UpdateSelectedState();
        }

        private void RefreshTools()
        {
            if (ToolItems == null) return;
            ToolItems.ItemsSource = _registry.Descriptors
                .Where(x => x.DeclaredStatus != ToolboxDeclaredStatus.Hidden && x.DeclaredStatus != ToolboxDeclaredStatus.Planned)
                .Select(ToCard)
                .ToList();
        }

        private ToolCardItem ToCard(ToolboxToolDescriptor descriptor)
        {
            var status = _registry.CanExecute(descriptor.Id) ? descriptor.StatusText : "当前不可用";
            return new ToolCardItem
            {
                Id = descriptor.Id,
                Name = descriptor.Name,
                Description = descriptor.Description,
                CategoryPath = descriptor.CategoryPath,
                DisplayStatus = status
            };
        }

        private void ToolClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is string id)) return;
            _selected = _registry.Descriptors.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (_selected == null) return;

            DetailNameText.Text = _selected.Name;
            DetailCategoryText.Text = _selected.CategoryPath;
            DetailDescriptionText.Text = _selected.Description;
            ParameterPanel.SetParameters(_selected.ParameterShape);
            ImageQuickActions.Visibility = _selected.Id == "project.image.embed" ? Visibility.Visible : Visibility.Collapsed;
            ResultBorder.Visibility = Visibility.Collapsed;
            UpdateSelectedState();
            ToolListPane.Visibility = Visibility.Collapsed;
            ToolDetailPane.Visibility = Visibility.Visible;
        }

        private void UpdateSelectedState()
        {
            if (_selected == null) return;
            var canExecute = _registry.CanExecute(_selected.Id);
            DetailStatusText.Text = canExecute ? _selected.StatusText : "当前不可用";
            AvailabilityText.Text = canExecute
                ? "当前宿主与文档条件满足，可以执行。"
                : "当前宿主/文档条件不满足；执行按钮已禁用。";
            ExecuteButton.IsEnabled = canExecute;
            PreviewButton.Visibility = _selected.SupportsDryRun ? Visibility.Visible : Visibility.Collapsed;
            PreviewButton.IsEnabled = canExecute;
        }

        private void BackToList(object sender, RoutedEventArgs e) => ShowList();

        private void ShowList()
        {
            if (ToolListPane == null || ToolDetailPane == null) return;
            ToolDetailPane.Visibility = Visibility.Collapsed;
            ToolListPane.Visibility = Visibility.Visible;
            RefreshTools();
        }

        private void PreviewSelectedTool(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            ParameterPanel.ClearValidation();
            ResultBorder.Visibility = Visibility.Collapsed;
            if (!ParameterPanel.TryGetValues(out var values, out var error))
            {
                ShowResult(false, "参数校验未通过", error);
                return;
            }

            try
            {
                RuntimeLogger.Info("Toolbox GUI preview begin tool=" + _selected.Id);
                var result = _registry.Preview(_selected.Id, values);
                RuntimeLogger.Info("Toolbox GUI preview completed tool=" + _selected.Id);
                ShowResult(true, "预览完成（未修改图纸）", FormatResult(result));
                UpdateSelectedState();
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox tool preview failed: " + _selected.Id, ex);
                ShowResult(false, "预览失败", ex.Message);
                UpdateSelectedState();
            }
        }

        private void ExecuteSelectedTool(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            ParameterPanel.ClearValidation();
            ResultBorder.Visibility = Visibility.Collapsed;
            if (!ParameterPanel.TryGetValues(out var values, out var error))
            {
                ShowResult(false, "参数校验未通过", error);
                return;
            }

            try
            {
                RuntimeLogger.Info("Toolbox GUI execute begin tool=" + _selected.Id);
                var result = _registry.Execute(_selected.Id, values);
                RuntimeLogger.Info("Toolbox GUI execute completed tool=" + _selected.Id);
                if (result is ImageEmbedBatchService.Result embed && embed.started)
                    ShowResult(true, "任务已启动（尚未完成）", $"共 {embed.file_count} 份 DWG。\n输出目录：{embed.output_folder}\n状态文件：{embed.status_path}\n以 status.json 的最终 PASS/PARTIAL/FAIL 判断结果。\n\n" + FormatResult(result));
                else
                    ShowResult(true, "执行完成", FormatResult(result));
                UpdateSelectedState();
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox tool failed: " + _selected.Id, ex);
                ShowResult(false, "执行失败", ex.Message);
                UpdateSelectedState();
            }
        }

        private void QuickEmbedCurrentClick(object sender, RoutedEventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || string.IsNullOrWhiteSpace(doc.Name) || !File.Exists(doc.Name))
            {
                ShowResult(false, "请先保存图纸", "请先将当前 DWG 保存到磁盘。为避免把未保存更改误作最终结果，请保存后再执行一键内嵌。");
                return;
            }
            if (System.Convert.ToInt32(Application.GetSystemVariable("DBMOD")) != 0)
            {
                ShowResult(false, "请先保存更改", "当前图纸有未保存修改。请 Ctrl+S 后再运行，避免输出副本缺少当前更改。");
                return;
            }
            RunImageQuickAction(doc.Name, "当前图纸");
        }

        private void QuickEmbedFolderClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择要批量内嵌引用的 DWG 文件夹（默认包含子文件夹）",
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                RunImageQuickAction(dialog.SelectedPath, "文件夹批量");
            }
        }

        private void RunImageQuickAction(string inputPath, string label)
        {
            try
            {
                RuntimeLogger.Info("Toolbox GUI quick embed begin mode=" + label);
                var result = ImageEmbedBatchService.Start(new ImageEmbedBatchService.Request
                {
                    InputPath = inputPath,
                    IncludeSubfolders = true,
                    BindDrawingXrefs = true,
                    RequireAllImages = true
                });
                RuntimeLogger.Info("Toolbox GUI quick embed worker started job=" + result.job_id);
                ShowResult(true, label + "任务已启动（等待 Worker 完成）",
                    $"待处理 DWG：{result.file_count}\n输出：{result.output_folder}\n查看最终结果：{result.status_path}\n\n不会覆盖源 DWG；批量内嵌结果不能只凭 Worker 已启动判断成功。");
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox GUI quick embed failed", ex);
                ShowResult(false, label + "启动失败", ex.Message);
            }
        }

        private void ShowResult(bool success, string title, string text)
        {
            ResultTitleText.Text = title;
            ResultText.Text = string.IsNullOrWhiteSpace(text) ? (success ? "操作已完成。" : "未返回详细错误。") : text;
            ResultTitleText.SetResourceReference(TextBlock.ForegroundProperty, success ? "SuccessBrush" : "DangerBrush");
            ResultBorder.Visibility = Visibility.Visible;
        }

        private void CopyResultClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ResultText.Text)) Clipboard.SetText(ResultText.Text);
        }

        private static string FormatResult(object? result)
        {
            if (result == null) return "操作已完成。";
            if (result is string text) return text;
            try { return JsonCodec.Serialize(result, indented: true); }
            catch { return Convert.ToString(result) ?? "操作已完成。"; }
        }
    }
}
