using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.AutoCADHost.Toolbox;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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

        private static readonly string[] AllowedQuickCommands =
        {
            "YGLAYER", "YGLONOFF", "YGLMERGE", "YGLFREEZE", "YGLLOCK",
            "YGDETAIL", "YGDETAILPOLY", "YGBREAKLINE", "YGOUTSIDE", "YGSCALE", "YGDL", "YGDLW",
            "BMK", "BMKFIX", "CMK", "SLB", "SLBFIX", "SLBP", "SLBLAYER", "SLBSET",
            "YGLEADERFIX", "YGDIMTXT", "YGSLOPE", "YGTBOX",
            "DIMCHK", "DIMFIX", "DIMRE", "DIMFZ", "TXTUP", "DFSET", "TXAL",
            "YGREVIT", "YGEMBED", "YGEMBEDBATCH",
            "YGKEYS", "YANGCAD"
        };

        private readonly ToolboxRegistry _registry;
        private ToolboxToolDescriptor? _selected;
        private string _selectedAliasCommand = "";
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
            Loaded += (_, __) => SetQuickButtonTooltips();
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

        private void SetQuickButtonTooltips()
        {
            if (ToolListPane == null) return;
            foreach (var button in FindVisualChildren<Button>(ToolListPane))
            {
                if (!(button.Tag is string cmd)) continue;
                var item = CadToolCatalog.All.FirstOrDefault(x =>
                    string.Equals(x.Command, cmd, StringComparison.OrdinalIgnoreCase));
                if (item == null) continue;
                button.ToolTip = $"{item.Description}\n命令：{item.Command}（推荐缩写：{item.DefaultAliasHint}）";
            }
        }

        private static string? TryFindActivePgpPath()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                return doc == null ? null : ShortcutAliasService.FindActivePgpPath(doc.Database);
            }
            catch { return null; }
        }

        private static string? TryGetCurrentAlias(string pgpPath, string command)
        {
            try { return ShortcutAliasService.CurrentAlias(pgpPath, command); }
            catch { return null; }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) yield return typed;
                foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
            }
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

        // Quick commands route through the normal AutoCAD command context; no duplicate business logic.
        private void QuickNativeCommandClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is string command)) return;
            var allowed = AllowedQuickCommands.Contains(command, StringComparer.OrdinalIgnoreCase) ||
                          CadToolCatalog.All.Any(x => string.Equals(x.Command, command, StringComparison.OrdinalIgnoreCase));
            if (!allowed) return;
            if (TryStartNativeCommand(command))
                SetQuickStatus("");
        }

        private bool TryStartNativeCommand(string command)
        {
            var document = AcApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                SetQuickStatus("请先打开图纸后使用命令。");
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(Convert.ToString(AcApp.GetSystemVariable("CMDNAMES"))))
                {
                    SetQuickStatus("请先结束当前 CAD 命令。");
                    return false;
                }

                document.SendStringToExecute(command + " ", true, false, false);
                return true;
            }
            catch (Exception ex)
            {
                SetQuickStatus("无法启动命令：" + ex.Message);
                return false;
            }
        }

        private void SetQuickStatus(string message)
        {
            QuickAliasStatus.Text = message;
            QuickAliasStatus.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void QuickToolRightClick(object sender, MouseButtonEventArgs e)
        {
            if (!((sender as Button)?.Tag is string command)) return;
            e.Handled = true;
            _selectedAliasCommand = command;
            QuickAliasEditor.Visibility = Visibility.Visible;
            var item = CadToolCatalog.All.FirstOrDefault(x =>
                string.Equals(x.Command, command, StringComparison.OrdinalIgnoreCase));
            var name = item?.Name ?? command;
            QuickAliasLabel.Text = $"{name} ({command})";

            var pgpPath = TryFindActivePgpPath();
            var existing = !string.IsNullOrEmpty(pgpPath) ? TryGetCurrentAlias(pgpPath!, command) : null;
            QuickAliasInput.Text = existing ?? "";
            SetQuickStatus(string.IsNullOrEmpty(existing) ? $"建议缩写：{item?.DefaultAliasHint ?? command}" : "");
            QuickAliasInput.Focus();
            QuickAliasInput.SelectAll();
        }

        private void CloseAliasEditorClick(object sender, RoutedEventArgs e)
        {
            QuickAliasEditor.Visibility = Visibility.Collapsed;
            _selectedAliasCommand = "";
            SetQuickStatus("");
        }

        private void QuickAliasSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedAliasCommand))
            {
                SetQuickStatus("请先右键点击上方任意工具按钮，再输入快捷键。");
                return;
            }
            var pgpPath = TryFindActivePgpPath();
            if (string.IsNullOrEmpty(pgpPath))
            {
                SetQuickStatus("请先打开一张图纸再修改快捷键。");
                return;
            }
            try
            {
                var alias = QuickAliasInput.Text.Trim();
                if (string.IsNullOrEmpty(alias))
                {
                    var removed = ShortcutAliasService.Remove(pgpPath!, _selectedAliasCommand);
                    SetQuickStatus(removed ? "已清除快捷键，点“应用”生效。" : "该命令暂无自定义快捷键。");
                }
                else
                {
                    var saved = ShortcutAliasService.Save(pgpPath!, _selectedAliasCommand, alias, allowExistingOverride: false);
                    SetQuickStatus($"已保存 {saved} → {_selectedAliasCommand}，点“应用”在当前图纸生效。");
                }
            }
            catch (Exception ex)
            {
                SetQuickStatus(ex.Message);
            }
        }

        private void QuickAliasApplyClick(object sender, RoutedEventArgs e)
        {
            if (TryStartNativeCommand("_.REINIT"))
                SetQuickStatus("在“重新初始化”窗口中勾选“PGP 文件”并确定。");
        }

        private void ToolClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is string id)) return;
            _selected = _registry.Descriptors.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (_selected == null) return;

            QuickAliasEditor.Visibility = Visibility.Collapsed;
            SetQuickStatus("");
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
            if (_selected.Id == "project.image.embed")
            {
                _embedLastState = "";
                EmbedStatusTick(this, EventArgs.Empty);
            }
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
            _selected = null;
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
                if (_selected.Id == "project.image.embed" &&
                    (!values.TryGetValue("input_path", out var input) || string.IsNullOrWhiteSpace(input as string)) &&
                    GetSavedCurrentDrawing() == null)
                    return;

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
            if (!TryStartNativeCommand("YGLEADERFIX")) return;
            SetQuickStatus("");
            ShowResult(true, "请在绘图区框选引线与文字", "直接框选炸开的引线线段和旁边文字，回车即可合成原生 MLeader。");
        }

        private void QuickEmbedCurrentClick(object sender, RoutedEventArgs e)
        {
            var path = GetSavedCurrentDrawing();
            if (path != null) RunImageQuickAction(path, "当前图纸");
        }

        private string? GetSavedCurrentDrawing()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || string.IsNullOrWhiteSpace(doc.Name) || !File.Exists(doc.Name))
            {
                ShowResult(false, "请先保存图纸", "请先将当前 DWG 保存到磁盘。为避免把未保存更改误作最终结果，请保存后再执行一键内嵌。");
                return null;
            }
            if (System.Convert.ToInt32(AcApp.GetSystemVariable("DBMOD")) != 0)
            {
                ShowResult(false, "请先保存更改", "当前图纸有未保存修改。请 Ctrl+S 后再运行，避免输出副本缺少当前更改。");
                return null;
            }
            return doc.Name;
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
                var terminal = state == "PASS" || state == "PARTIAL" || state == "FAIL" || state == "SKIPPED";
                var headline = terminal ? $"引用处理完成：{state}" : $"引用处理进行中：{state}";
                var details = $"总数：{job.total_files}；成功：{job.passed}；部分成功：{job.partial}；失败：{job.failed}；跳过：{job.skipped}\n输出：{_embedOutputFolder}\n状态：{_embedStatusFile}";
                if (!string.IsNullOrWhiteSpace(job.fatal_error)) details += "\n错误：" + job.fatal_error;
                if (_selected?.Id == "project.image.embed" &&
                    (terminal || !string.Equals(_embedLastState, details, StringComparison.Ordinal)))
                {
                    ShowResult(state == "PASS" || !terminal, headline, details, showEmbedOutput: true);
                    _embedLastState = details;
                }
                if (terminal) StopEmbedStatusMonitor();
            }
            catch (IOException) { /* Worker may be atomically replacing status.json; retry next tick. */ }
            catch (System.Exception ex)
            {
                StopEmbedStatusMonitor();
                RuntimeLogger.Error("Image embed status monitor failed.", ex);
                if (_selected?.Id == "project.image.embed")
                    ShowResult(false, "无法解析任务状态", ex.Message + "\n状态文件：" + _embedStatusFile, showEmbedOutput: true);
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

        private void ShowResult(bool success, string title, string text, bool showEmbedOutput = false)
        {
            ResultTitleText.Text = title;
            ResultText.Text = string.IsNullOrWhiteSpace(text) ? (success ? "操作已完成。" : "未返回详细错误。") : text;
            ResultTitleText.SetResourceReference(TextBlock.ForegroundProperty, success ? "SuccessBrush" : "DangerBrush");
            OpenEmbedOutputButton.Visibility = showEmbedOutput ? Visibility.Visible : Visibility.Collapsed;
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
