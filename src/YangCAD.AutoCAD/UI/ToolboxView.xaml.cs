using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.AutoCADHost.Toolbox;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ToolboxView : UserControl
    {
        private sealed class ToolGroup
        {
            public string Key { get; }
            public string Name { get; }
            public bool IsExpanded { get; }
            public object[] Tools { get; }

            public ToolGroup(string key, string name, bool isExpanded, IEnumerable<CadToolItem> tools)
            {
                Key = key;
                Name = name;
                IsExpanded = isExpanded;
                Tools = tools.Select(tool => (object)new
                {
                    tool.Command,
                    tool.ShortName,
                    Help = $"{tool.Name}\n{tool.Description}\n{tool.Command}（{tool.DefaultAliasHint}）"
                }).ToArray();
            }
        }

        private static readonly (string Key, string Name)[] GroupOrder =
        {
            ("修改", "绘图与编辑"), ("图层", "图层"), ("结构", "结构绘图"), ("标注", "标注"),
            ("文字", "文字编辑与对齐"), ("填充", "填充编辑"), ("专项", "图纸与引用"), ("系统", "设置")
        };

        private readonly ToolboxRegistry _registry;
        private ToolboxToolDescriptor? _selected;
        private string _selectedAliasCommand = "";
        private DispatcherTimer? _embedStatusTimer;
        private string _embedStatusFile = "";
        private string _embedLastState = "";
        private string _taskOutputFolder = "";
        private string[] _favorites = Array.Empty<string>();
        private string[] _recent = Array.Empty<string>();
        private string[] _collapsedGroups = Array.Empty<string>();

        public sealed class EmbedStatusDto
        {
            public string state { get; set; } = "";
            public int total_files { get; set; }
            public int passed { get; set; }
            public int failed { get; set; }
            public int partial { get; set; }
            public int skipped { get; set; }
            public string fatal_error { get; set; } = "";
            public EmbedItemDto[] items { get; set; } = Array.Empty<EmbedItemDto>();
        }

        public sealed class EmbedItemDto
        {
            public string status { get; set; } = "";
            public string error { get; set; } = "";
        }

        public ToolboxView(ToolboxRegistry registry)
        {
            _registry = registry;
            InitializeComponent();
            var settings = UserSettings.Load();
            ThemeManager.Apply(Resources, settings.Theme);
            _favorites = settings.ToolboxFavorites;
            _recent = settings.ToolboxRecent;
            _collapsedGroups = settings.ToolboxCollapsedGroups;
            RefreshTools();
            Loaded += (_, __) => ResumeEmbedStatusMonitor();
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
            if (ToolGroups == null || ToolSearchBox == null) return;
            var query = ToolSearchBox.Text.Trim();
            var terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var catalog = CadToolCatalog.All.Where(tool => tool.Command != "YANGCAD_TOOLBOX");
            var visible = catalog.Where(tool =>
                (!tool.IsSecondary || terms.Length > 0) &&
                terms.All(term => $"{tool.Name} {tool.ShortName} {tool.Command} {tool.DefaultAliasHint} {tool.Category}"
                    .IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var groups = new List<ToolGroup>();

            if (terms.Length == 0)
            {
                var favorites = _favorites.Select(id => catalog.FirstOrDefault(tool => tool.Id == id))
                    .OfType<CadToolItem>().ToArray();
                if (favorites.Length > 0)
                    groups.Add(new ToolGroup("favorites", "常用", !_collapsedGroups.Contains("favorites"), favorites));

                var recent = _recent.Where(id => !_favorites.Contains(id))
                    .Select(id => visible.FirstOrDefault(tool => tool.Id == id))
                    .OfType<CadToolItem>().Take(6).ToArray();
                if (recent.Length > 0)
                    groups.Add(new ToolGroup("recent", "最近使用", !_collapsedGroups.Contains("recent"), recent));
            }

            foreach (var (key, name) in GroupOrder)
            {
                var tools = visible.Where(tool => tool.Category == key).ToArray();
                if (tools.Length > 0)
                    groups.Add(new ToolGroup(key, name, terms.Length > 0 || !_collapsedGroups.Contains(key), tools));
            }
            ToolGroups.ItemsSource = groups;
            EmptySearchText.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToolSearchChanged(object sender, TextChangedEventArgs e) => RefreshTools();

        private void ToolGroupExpansionChanged(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(sender, e.OriginalSource) || !(sender is Expander group) ||
                !(group.Tag is string key) || !string.IsNullOrWhiteSpace(ToolSearchBox?.Text)) return;
            var collapsed = _collapsedGroups.Contains(key);
            if (collapsed == !group.IsExpanded) return;
            _collapsedGroups = group.IsExpanded ? _collapsedGroups.Where(x => x != key).ToArray()
                : _collapsedGroups.Concat(new[] { key }).ToArray();
            var settings = UserSettings.Load();
            settings.ToolboxCollapsedGroups = _collapsedGroups;
            settings.Save();
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

        // Use the same native command entry points as the Ribbon and command line.
        private void QuickNativeCommandClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is string command)) return;
            var tool = CadToolCatalog.All.FirstOrDefault(item => item.Command == command);
            if (tool == null || !TryStartNativeCommand(command)) return;
            SetQuickStatus("");
            if (_recent.FirstOrDefault() == tool.Id) return;
            _recent = new[] { tool.Id }.Concat(_recent.Where(id => id != tool.Id)).Take(6).ToArray();
            var settings = UserSettings.Load();
            settings.ToolboxRecent = _recent;
            settings.Save();
            RefreshTools();
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
                var busy = !string.IsNullOrWhiteSpace(Convert.ToString(AcApp.GetSystemVariable("CMDNAMES")));
                if (busy && !CadToolCatalog.CanInterruptActiveCommand(command))
                {
                    SetQuickStatus("请先结束当前 CAD 命令。");
                    return false;
                }

                document.SendStringToExecute((busy ? "\u0003\u0003" : "") + command + " ", true, false, false);
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
            var descriptorId = command == "YGREVIT" ? "project.revit.prepare" :
                command == "YGREFEMBED" || command == "YGEMBED" || command == "YGEMBEDBATCH" ? "project.image.embed" : null;
            if (!(sender is Button button)) return;
            var item = CadToolCatalog.All.FirstOrDefault(tool => tool.Command == command);
            if (item == null) return;
            var menu = new ContextMenu { PlacementTarget = button };
            var pinned = _favorites.Contains(item.Id);
            var favorite = new MenuItem { Header = pinned ? "从常用移除" : "置顶到常用" };
            favorite.Click += (_, __) => ToggleFavorite(item.Id);
            menu.Items.Add(favorite);
            var shortcut = new MenuItem { Header = "设置命令缩写" };
            shortcut.Click += (_, __) => EditAlias(command);
            menu.Items.Add(shortcut);
            if (descriptorId != null)
            {
                var parameters = new MenuItem { Header = "任务参数与预览" };
                parameters.Click += (_, __) => ShowTool(descriptorId);
                menu.Items.Add(parameters);
            }
            menu.IsOpen = true;
        }

        private void ToggleFavorite(string id)
        {
            _favorites = _favorites.Contains(id) ? _favorites.Where(item => item != id).ToArray()
                : new[] { id }.Concat(_favorites).ToArray();
            var settings = UserSettings.Load();
            settings.ToolboxFavorites = _favorites;
            settings.Save();
            RefreshTools();
        }

        private void EditAlias(string command)
        {
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
                    if (removed) ApplyAlias("已清除并重新加载快捷键。");
                    else SetQuickStatus("该命令暂无自定义快捷键。");
                }
                else
                {
                    var saved = ShortcutAliasService.Save(pgpPath!, _selectedAliasCommand, alias, allowExistingOverride: false);
                    ApplyAlias($"已应用 {saved} → {_selectedAliasCommand}。");
                }
            }
            catch (Exception ex)
            {
                SetQuickStatus(ex.Message);
            }
        }

        private void ApplyAlias(string success)
        {
            if (ShortcutAliasService.TryReloadActivePgp()) SetQuickStatus(success);
            else if (TryStartNativeCommand("_.REINIT"))
                SetQuickStatus("在“重新初始化”窗口中勾选“PGP 文件”并确定。");
        }

        private void ShowTool(string id)
        {
            _selected = _registry.Descriptors.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (_selected == null) return;

            QuickAliasEditor.Visibility = Visibility.Collapsed;
            SetQuickStatus("");
            DetailNameText.Text = _selected.Name;
            DetailCategoryText.Text = _selected.CategoryPath;
            DetailDescriptionText.Text = _selected.Description;
            ParameterPanel.SetParameters(_selected.ParameterShape.Where(parameter =>
                parameter.Key != "scan_blocks" && parameter.Key != "show_worker" &&
                parameter.Key != "command_timeout_seconds").ToArray());
            ImageQuickActions.Visibility = _selected.Id == "project.image.embed" ? Visibility.Visible : Visibility.Collapsed;
            RevitQuickActions.Visibility = _selected.Id == "project.revit.prepare" ? Visibility.Visible : Visibility.Collapsed;
            ResultBorder.Visibility = Visibility.Collapsed;
            UpdateSelectedState();
            ToolListPane.Visibility = Visibility.Collapsed;
            ToolSearchPanel.Visibility = Visibility.Collapsed;
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
            AvailabilityText.Text = canExecute ? "" : "当前不可用，请先打开一张 CAD 图纸。";
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
            ToolSearchPanel.Visibility = Visibility.Visible;
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
                    StartEmbedStatusMonitor(embed);
                }
                else
                {
                    if (result is RevitExchangeService.Result exchange) ShowRevitTaskResult(exchange);
                    else ShowResult(true, "执行完成", FormatResult(result));
                }
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
                if (result is RevitExchangeService.Result exchange) ShowRevitTaskResult(exchange);
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
                StartEmbedStatusMonitor(result);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox GUI quick embed failed", ex);
                ShowResult(false, label + "启动失败", ex.Message);
            }
        }

        internal void StartEmbedStatusMonitor(ImageEmbedBatchService.Result job)
        {
            StopEmbedStatusMonitor();
            ResultBorder.Visibility = Visibility.Collapsed;
            _embedStatusFile = job.status_path;
            _embedLastState = "";
            SetTaskStatus("正在内嵌引用…", $"待处理 {job.file_count} 份图纸", job.output_folder);
            ResumeEmbedStatusMonitor();
        }

        private void ResumeEmbedStatusMonitor()
        {
            if (_embedStatusTimer != null || string.IsNullOrEmpty(_embedStatusFile)) return;
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
                var headline = state switch
                {
                    "PASS" => "内嵌完成 · 原图未改动",
                    "PARTIAL" => "内嵌部分完成 · 原图未改动",
                    "SKIPPED" => "没有生成新副本",
                    "FAIL" => "内嵌失败 · 原图未改动",
                    _ => "正在内嵌引用…"
                };
                var error = string.IsNullOrWhiteSpace(job.fatal_error)
                    ? job.items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.error))?.error
                    : job.fatal_error;
                var details = $"成功 {job.passed} / 跳过 {job.skipped} / 失败 {job.failed}" +
                    (job.partial == 0 ? "" : $" / 部分成功 {job.partial}");
                if (terminal && !string.IsNullOrWhiteSpace(error)) details += "\n" + error;
                if (_embedLastState != details || terminal)
                {
                    TaskStatusTitle.Text = headline;
                    TaskStatusDetails.Text = details;
                    _embedLastState = details;
                }
                TaskOutputButton.Visibility = terminal && state != "FAIL" && Directory.Exists(_taskOutputFolder)
                    ? Visibility.Visible : Visibility.Collapsed;
                if (terminal) StopEmbedStatusMonitor();
            }
            catch (IOException) { /* Worker may be atomically replacing status.json; retry next tick. */ }
            catch (System.Exception ex)
            {
                StopEmbedStatusMonitor();
                RuntimeLogger.Error("Image embed status monitor failed.", ex);
                TaskStatusTitle.Text = "无法读取引用任务状态";
                TaskStatusDetails.Text = ex.Message;
            }
        }

        internal void ShowRevitTaskResult(RevitExchangeService.Result result)
        {
            if (_embedStatusTimer != null) return;
            _embedStatusFile = "";
            SetTaskStatus("Revit 副本已生成 · 原图未改动",
                $"已整理 {result.classified_entities} 个 / 跳过 {result.unsupported_count} 个",
                Path.GetDirectoryName(result.output_dwg) ?? "");
        }

        internal void ShowTaskFailure(string title, string error)
        {
            if (_embedStatusTimer != null) return;
            _embedStatusFile = "";
            SetTaskStatus(title, error, "");
        }

        private void SetTaskStatus(string title, string details, string folder)
        {
            _taskOutputFolder = folder;
            TaskStatusTitle.Text = title;
            TaskStatusDetails.Text = details;
            TaskOutputButton.Visibility = Directory.Exists(folder) && _embedStatusFile.Length == 0
                ? Visibility.Visible : Visibility.Collapsed;
            TaskStatusBar.Visibility = Visibility.Visible;
        }

        private void CloseTaskStatusClick(object sender, RoutedEventArgs e)
            => TaskStatusBar.Visibility = Visibility.Collapsed;

        private void OpenTaskOutputClick(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_taskOutputFolder))
            {
                TaskStatusDetails.Text = "输出目录尚未生成。";
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _taskOutputFolder + "\"") { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                TaskStatusDetails.Text = "打开输出目录失败：" + ex.Message;
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

        private static string FormatResult(object? result) => result switch
        {
            ImageEmbedBatchService.Result image => $"预计处理 {image.file_count} 份图纸，原图不会改动。",
            RevitExchangeService.Result revit => $"已识别 {revit.classified_entities} 个对象，跳过 {revit.unsupported_count} 个。",
            string message => message,
            _ => "操作已完成。"
        };
    }
}
