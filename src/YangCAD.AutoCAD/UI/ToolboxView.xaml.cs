using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Toolbox;
using YangCAD.AutoCADHost.Services.Cad;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
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
        private DispatcherTimer? _embedStatusTimer;
        private string _embedStatusFile = "";
        private string _embedOutputFolder = "";
        private string _embedLastState = "";

        public sealed class EmbedStatusDto
        {
            public string state { get; set; } = "";
            public int total_files { get; set; }
            public int passed { get; set; }
            public int failed { get; set; }
            public int partial { get; set; }
            public int skipped { get; set; }
            public string fatal_error { get; set; } = "";
        }

        public ToolboxView(ToolboxRegistry registry)
        {
            _registry = registry;
            InitializeComponent();
            ThemeManager.Apply(Resources, UserSettings.Load().Theme);
            RefreshTools();
            Unloaded += (_, __) => StopEmbedStatusMonitor();
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
            RevitQuickActions.Visibility = _selected.Id == "project.revit.prepare" ? Visibility.Visible : Visibility.Collapsed;
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
            AvailabilityText.Text = canExecute ? "" : "请先打开一张 CAD 图纸。";
            AvailabilityText.Visibility = canExecute ? Visibility.Collapsed : Visibility.Visible;
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
                {
                    ShowResult(true, "任务已启动（尚未完成）", $"共 {embed.file_count} 份 DWG。\n输出目录：{embed.output_folder}\n状态文件：{embed.status_path}\n下方将自动显示进度和最终结果。\n\n" + FormatResult(result));
                    StartEmbedStatusMonitor(embed);
                }
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

        private void QuickRevitPrepareClick(object sender, RoutedEventArgs e)
        {
            if (!_registry.CanExecute("project.revit.prepare"))
            {
                ShowResult(false, "请先打开图纸", "当前没有可用的 DWG。");
                return;
            }
            try
            {
                RuntimeLogger.Info("Toolbox GUI execute begin tool=project.revit.prepare shortcut=one_click");
                var result = _registry.Execute("project.revit.prepare",
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));
                RuntimeLogger.Info("Toolbox GUI execute completed tool=project.revit.prepare shortcut=one_click");
                if (result is RevitExchangeService.Result exchange)
                {
                    ShowResult(true, "已生成 Revit 图纸副本",
                        $"DWG：{exchange.output_dwg}\n已整理：{exchange.classified_entities} 个对象；未转换：{exchange.unsupported_count} 个。\n" +
                        "原图未修改。详细 MAP/REPORT 与 DWG 位于同一输出目录。" +
                        (exchange.warnings.Length > 0 ? "\n注意：" + string.Join("；", exchange.warnings.Take(4)) : ""));
                }
                else ShowResult(true, "预处理完成", FormatResult(result));
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("Toolbox GUI one-click Revit preparation failed", ex);
                ShowResult(false, "预处理失败，原图未修改", ex.Message);
            }
        }

        private void QuickLeaderRepairClick(object sender, RoutedEventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                ShowResult(false, "请先打开图纸", "需要在 CAD 绘图区选择要修复的引线。");
                return;
            }
            // Send a native modal command to the CAD editor. Selection must not run inside a WPF callback.
            // This reports only that the command was queued, NOT that the repair succeeded.
            doc.SendStringToExecute("_.YGLEADERFIX ", true, false, false);
            ShowResult(true, "请在绘图区选择引线", "1. 选择旧引线或炸开的连续线段，回车。\n2. 选择旁边的文字；没有文字直接回车。\n3. 无文字时拾取箭头端点。\n原生 MLeader 创建成功后才会替换原对象。");
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
                    $"待处理 DWG：{result.file_count}\n输出：{result.output_folder}\n状态文件：{result.status_path}\n\n不会覆盖源 DWG；下方将自动显示进度和最终结果。");
                StartEmbedStatusMonitor(result);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox GUI quick embed failed", ex);
                ShowResult(false, label + "启动失败", ex.Message);
            }
        }

        private void StartEmbedStatusMonitor(ImageEmbedBatchService.Result job)
        {
            StopEmbedStatusMonitor();
            _embedStatusFile = job.status_path;
            _embedOutputFolder = job.output_folder;
            _embedLastState = "";
            OpenEmbedOutputButton.Visibility = Visibility.Visible;
            _embedStatusTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            _embedStatusTimer.Tick += EmbedStatusTick;
            _embedStatusTimer.Start();
        }

        private void StopEmbedStatusMonitor()
        {
            if (_embedStatusTimer == null) return;
            _embedStatusTimer.Stop();
            _embedStatusTimer.Tick -= EmbedStatusTick;
            _embedStatusTimer = null;
        }

        private void EmbedStatusTick(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_embedStatusFile) || !File.Exists(_embedStatusFile)) return;
            try
            {
                var job = JsonCodec.Deserialize<EmbedStatusDto>(File.ReadAllText(_embedStatusFile));
                if (job == null || string.IsNullOrWhiteSpace(job.state)) return;
                var state = job.state.Trim().ToUpperInvariant();
                var terminal = state == "PASS" || state == "PARTIAL" || state == "FAIL";
                var headline = terminal ? $"引用处理完成：{state}" : $"引用处理进行中：{state}";
                var details = $"总数：{job.total_files}；成功：{job.passed}；部分成功：{job.partial}；失败：{job.failed}；跳过：{job.skipped}\n输出：{_embedOutputFolder}\n状态：{_embedStatusFile}";
                if (!string.IsNullOrWhiteSpace(job.fatal_error)) details += "\n错误：" + job.fatal_error;
                if (terminal || !string.Equals(_embedLastState, details, StringComparison.Ordinal))
                {
                    ShowResult(state == "PASS" || !terminal, headline, details);
                    _embedLastState = details;
                }
                if (terminal) StopEmbedStatusMonitor();
            }
            catch (IOException) { /* Worker may be atomically replacing status.json; retry next tick. */ }
            catch (System.Exception ex)
            {
                StopEmbedStatusMonitor();
                RuntimeLogger.Error("Image embed status monitor failed.", ex);
                ShowResult(false, "无法解析任务状态", ex.Message + "\n状态文件：" + _embedStatusFile);
            }
        }

        private void OpenEmbedOutputClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_embedOutputFolder) || !Directory.Exists(_embedOutputFolder))
            {
                ShowResult(false, "输出目录尚不存在", "请检查 Worker 最终状态，部分失败任务可能没有生成输出副本。");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _embedOutputFolder + "\"") { UseShellExecute = true });
            }
            catch (System.Exception ex) { ShowResult(false, "打开输出目录失败", ex.Message); }
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
