using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.Windows;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Toolbox;

namespace YangCAD.AutoCADHost.UI
{
    public static class RibbonBootstrapper
    {
        private const string TabId = "YANGCAD_AGENT_TAB";
        private static bool _idleHooked;
        private static DateTime _notBeforeUtc;

        // Startup-safe entry point. Never construct WPF ribbon images directly from IExtensionApplication.Initialize.
        public static void ScheduleEnsureRibbon()
        {
            if (_idleHooked) return;
            // Ribbon is optional. Give Autodesk several seconds to finish host/UI startup before
            // touching AdWindows/WPF. MCP core must never depend on this path.
            _notBeforeUtc = DateTime.UtcNow.AddSeconds(5);
            Application.Idle += OnIdle;
            _idleHooked = true;
        }

        public static void EnsureRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                ScheduleEnsureRibbon();
                return;
            }

            foreach (var tab in ribbon.Tabs) if (tab.Id == TabId) return;

            // Remember Autodesk's selected tab before adding our custom tab. Adding a tab
            // late during startup must not steal the native Home/Civil 3D ribbon focus.
            RibbonIcons.UseLightTheme = Convert.ToInt32(Application.GetSystemVariable("COLORTHEME")) == 1;
            var previousTab = ribbon.ActiveTab;
            var newTab = new RibbonTab { Id = TabId, Title = "YangCAD" };

            AddPanel(newTab, "常用工具",
                Button("工具箱", "YANGCAD_TOOLBOX ", "ControlCenter32.png"),
                Button("比例转换", "YGSCALE ", "ControlCenter32.png"),
                Button("提取大样", "YGDETAIL ", "ControlCenter32.png"),
                Menu("边界编辑", "detail_extract",
                    MenuItem("直线折断", "YGBREAKLINE ", "scale_convert"),
                    MenuItem("删除边界外", "YGOUTSIDE ", "hatch_clip")));

            AddPanel(newTab, "图层与填充",
                Button("图层工具", "YGLAYER ", "ControlCenter32.png"),
                Menu("图层操作", "layer_merge",
                    MenuItem("图层开关", "YGLONOFF ", "layer_off"),
                    MenuItem("合并图层", "YGLMERGE ", "layer_merge"),
                    MenuItem("图层冻结", "YGLFREEZE ", "layer_freeze"),
                    MenuItem("图层锁定", "YGLLOCK ", "layer_lock")),
                Button("剪裁填充", "YGHCLIP ", "ControlCenter32.png"),
                RememberedMenu("填充编辑", "hatch_clip",
                    MenuItem("拆分填充", "YGHSPLIT ", "hatch_split"),
                    MenuItem("合并填充", "YGHUNION ", "hatch_union"),
                    MenuItem("取消填充关联", "YGHDIS ", "hatch_disasso"),
                    MenuItem("重建填充边界", "YGHBOUND ", "hatch_bound"),
                    MenuItem("重设填充基点", "YGHBASE ", "hatch_base")));

            AddPanel(newTab, "文字与标注",
                Button("递增复制", "YGINC ", "ControlCenter32.png"),
                Button("引线修复", "YGLEADERFIX ", "ControlCenter32.png"),
                RememberedMenu("文字编辑", "text_align_lr",
                    MenuItem("文字对齐", "YGTALIGN ", "text_align_lr"),
                    MenuItem("沿线或圆弧排字", "YGTALONG ", "text_arc"),
                    MenuItem("文字拆合", "YGTSPLITJOIN ", "text_join"),
                    MenuItem("文字清洗", "YGTCLEAN ", "text_space"),
                    MenuItem("加删前后缀", "YGTAFFIX ", "text_affix"),
                    MenuItem("文字刷换", "YGTCONTENT ", "text_brush")),
                Menu("标注工具", "dim_check",
                    MenuItem("标注检查", "YGDIMCHECK ", "dim_check"),
                    MenuItem("标注修值", "YGDIMEDIT ", "dim_fix"),
                    MenuItem("标注坡度", "YGSLOPE ", "slope_mark"),
                    MenuItem("文字加框", "YGTBOX ", "text_frame"),
                    MenuItem("文字与标注翻正", "TXTUP ", "text_up")));

            AddPanel(newTab, "结构绘图",
                Button("智能双线", "YGDL ", "ControlCenter32.png"),
                Menu("梁柱板", "beam_mark",
                    MenuItem("梁尺寸标注", "BMK ", "beam_mark"),
                    MenuItem("柱尺寸标注", "CMK ", "col_mark"),
                    MenuItem("布置板受力符号", "SLB ", "slab_sym"),
                    MenuItem("修正板受力符号", "SLBFIX ", "slab_fix")));

            AddPanel(newTab, "图纸与引用",
                Button("Revit 预处理", "YGREVIT ", "ControlCenter32.png"),
                Button("引用内嵌", "YGREFEMBED ", "ControlCenter32.png"));

            AddPanel(newTab, "连接与设置",
                Button("MCP", "YANGCAD_MCP ", "Mcp32.png"),
                Menu("设置", "control_center",
                    MenuItem("快捷键", "YGKEYS ", "shortcut_keys"),
                    MenuItem("控制中心", "YANGCAD ", "control_center")));

            ribbon.Tabs.Add(newTab);
            // Autodesk may automatically activate a newly inserted ribbon tab. Restore only
            // when the new YangCAD tab became active; never override a valid user selection.
            if (ribbon.ActiveTab == newTab)
            {
                RibbonTab? restoreTab = previousTab;
                if (restoreTab == null || restoreTab == newTab)
                {
                    foreach (var tab in ribbon.Tabs)
                    {
                        if (tab == newTab) continue;
                        if ((tab.Id != null && tab.Id.IndexOf(".Home", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            string.Equals(tab.Title, "常用", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tab.Title, "Home", StringComparison.OrdinalIgnoreCase))
                        { restoreTab = tab; break; }
                    }
                }
                if (restoreTab != null && restoreTab != newTab)
                {
                    try { ribbon.ActiveTab = restoreTab; }
                    catch (System.Exception ex) { RuntimeLogger.Error("Could not restore native ribbon tab after YangCAD tab addition.", ex); }
                }
            }
        }

        public static void Shutdown() => UnhookIdle();

        private static void OnIdle(object? sender, EventArgs e)
        {
            try
            {
                if (DateTime.UtcNow < _notBeforeUtc) return;
                if (ComponentManager.Ribbon == null) return;
                if (Application.DocumentManager.MdiActiveDocument == null) return;
                var activeCommands = Convert.ToString(Application.GetSystemVariable("CMDNAMES"));
                if (!string.IsNullOrWhiteSpace(activeCommands)) return;
            }
            catch
            {
                return;
            }

            // One late attempt only. Any UI regression disables Ribbon for this session but leaves
            // YANGCAD/YANGCAD_MCP_STATUS commands and MCP Core alive.
            UnhookIdle();
            try
            {
                EnsureRibbon();
                RuntimeLogger.Info("Ribbon initialized after startup warm-up.");
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Ribbon initialization disabled for this session.", ex);
            }
        }

        private static void UnhookIdle()
        {
            if (!_idleHooked) return;
            try { Application.Idle -= OnIdle; } catch { }
            _idleHooked = false;
        }

        private static void AddPanel(RibbonTab tab, string title, params RibbonItem[] items)
        {
            var source = new RibbonPanelSource { Title = title };
            foreach (var item in items) source.Items.Add(item);
            tab.Panels.Add(new RibbonPanel { Source = source });
        }

        private static RibbonButton Button(string text, string command, string iconFile) =>
            BuildButton(text, command, command.Trim(), iconFile, RibbonItemSize.Large, Orientation.Vertical);

        private static RibbonButton MenuItem(string text, string command, string iconKey) =>
            BuildButton(text, command, iconKey, "ControlCenter32.png", RibbonItemSize.Standard, Orientation.Horizontal);

        private static RibbonSplitButton RememberedMenu(string title, string iconKey, params RibbonButton[] items)
        {
            var menu = Menu(title, iconKey, items);
            menu.IsSplit = true;
            menu.IsSynchronizedWithCurrentItem = true;
            return menu;
        }

        private static RibbonSplitButton Menu(string title, string iconKey, params RibbonButton[] items)
        {
            var menu = new RibbonSplitButton
            {
                Text = title,
                ShowText = true,
                ShowImage = true,
                Image = RibbonIcons.Create(iconKey, 16),
                LargeImage = RibbonIcons.Create(iconKey, 32),
                IsSplit = false,
                IsSynchronizedWithCurrentItem = false,
                Size = RibbonItemSize.Large,
                Orientation = Orientation.Vertical,
                ListStyle = RibbonSplitButtonListStyle.List,
                ListImageSize = RibbonImageSize.Standard
            };

            foreach (var item in items) menu.Items.Add(item);
            return menu;
        }

        private static RibbonButton BuildButton(
            string text,
            string command,
            string iconKeyOrCommand,
            string fallbackIconFile,
            RibbonItemSize size,
            System.Windows.Controls.Orientation orientation)
        {
            var trimmed = command.Trim();
            var catalog = CadToolCatalog.All.FirstOrDefault(x =>
                string.Equals(x.Command, trimmed, StringComparison.OrdinalIgnoreCase));
            iconKeyOrCommand = catalog?.IconKey ?? iconKeyOrCommand;
            ImageSource? small = null;
            ImageSource? large = null;
            try
            {
                small = RibbonIcons.Create(iconKeyOrCommand, 16) ?? LoadBitmap(fallbackIconFile, 16);
                large = RibbonIcons.Create(iconKeyOrCommand, 32) ?? LoadBitmap(fallbackIconFile, 32);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Ribbon vector icon failed; falling back to bitmap resource: " + iconKeyOrCommand, ex);
                try
                {
                    small = LoadBitmap(fallbackIconFile, 16);
                    large = LoadBitmap(fallbackIconFile, 32);
                }
                catch (Exception fallbackEx)
                {
                    RuntimeLogger.Error("Ribbon bitmap fallback also failed: " + fallbackIconFile, fallbackEx);
                }
            }

            text = catalog?.Name ?? text;
            var button = new RibbonButton
            {
                Name = text,
                Text = text,
                ShowText = true,
                ShowImage = (size == RibbonItemSize.Large ? large : small) != null,
                Size = size,
                Orientation = orientation,
                Image = small,
                LargeImage = large,
                CommandHandler = new Handler(command),
                ToolTip = BuildToolTip(text, trimmed, catalog)
            };
            return button;
        }

        private static RibbonToolTip BuildToolTip(string text, string trimmed, CadToolItem? catalog)
        {
            var content = catalog?.Description ?? ("执行 YangCAD 命令：" + trimmed);
            var hint = catalog != null ? $"推荐缩写：{catalog.DefaultAliasHint}" : "";
            return new RibbonToolTip
            {
                Title = text,
                Command = trimmed,
                Content = string.IsNullOrEmpty(hint) ? content : content + "\n" + hint
            };
        }

        private static BitmapImage LoadBitmap(string file, int size)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/YangCAD.AutoCAD;component/UI/Icons/" + file, UriKind.Absolute);
            image.DecodePixelWidth = size;
            image.DecodePixelHeight = size;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private sealed class Handler : ICommand
        {
            private readonly string _command;
            public Handler(string command) => _command = command;
            public bool CanExecute(object? parameter) => true;
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public void Execute(object? parameter)
            {
                var document = Application.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                if (!string.IsNullOrWhiteSpace(Convert.ToString(Application.GetSystemVariable("CMDNAMES"))))
                {
                    document.Editor.WriteMessage("\n[YangCAD] 请先结束当前 CAD 命令。\n");
                    return;
                }
                document.SendStringToExecute(_command, true, false, false);
            }
        }
    }
}
