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
            var previousTab = ribbon.ActiveTab;
            var newTab = new RibbonTab { Id = TabId, Title = "YangCAD" };

            AddPanel(newTab, "常用工具",
                Button("工具箱", "YANGCAD_TOOLBOX ", "ControlCenter32.png"),
                Button("比例转换", "YGSCALE ", "ControlCenter32.png"),
                Button("提取大样", "YGDETAIL ", "ControlCenter32.png"),
                Menu("边界编辑", "detail_extract",
                    MenuItem("多边形大样", "YGDETAILPOLY ", "detail_extract"),
                    MenuItem("直线折断", "YGBREAKLINE ", "scale_convert"),
                    MenuItem("删除边界外", "YGOUTSIDE ", "hatch_clip")));

            AddPanel(newTab, "图层与填充",
                Button("图层工具", "YGLAYER ", "ControlCenter32.png"),
                Menu("图层操作", "layer_merge",
                    MenuItem("图层开关", "YGLONOFF ", "layer_off"),
                    MenuItem("合并图层", "YGLMERGE ", "layer_merge"),
                    MenuItem("图层冻结", "YGLFREEZE ", "layer_freeze"),
                    MenuItem("图层锁定", "YGLLOCK ", "layer_lock")),
                Menu("填充编辑", "hatch_clip",
                    MenuItem("剪裁填充", "YGHCLIP ", "hatch_clip"),
                    MenuItem("挖出并保留独立填充", "YGHCLIPSPLIT ", "hatch_clip_split"),
                    MenuItem("仅保留框内填充", "YGHCLIPINT ", "hatch_clip"),
                    MenuItem("拆分填充", "YGHSPLIT ", "hatch_split"),
                    MenuItem("合并填充", "YGHUNION ", "hatch_union"),
                    MenuItem("取消填充关联", "YGHDIS ", "hatch_disasso"),
                    MenuItem("重建填充边界", "YGHBOUND ", "hatch_bound"),
                    MenuItem("重设填充基点", "YGHBASE ", "hatch_base")));

            AddPanel(newTab, "文字与标注",
                Menu("文字对齐", "text_align_lr",
                    MenuItem("文字左对齐", "YGTL ", "text_align_lr"),
                    MenuItem("水平居中对齐", "YGTC ", "text_align_lr"),
                    MenuItem("文字右对齐", "YGTR ", "text_align_lr"),
                    MenuItem("文字顶对齐", "YGTT ", "text_align_lr"),
                    MenuItem("文字底对齐", "YGTB ", "text_align_lr"),
                    MenuItem("垂直居中对齐", "YGTV ", "text_align_lr"),
                    MenuItem("按线投影对齐", "YGTLINE ", "text_align_lr"),
                    MenuItem("按弧环绕对齐", "YGTARC ", "text_arc"),
                    MenuItem("整理段落", "TXAL ", "text_align")),
                Menu("文字编辑", "text_increment",
                    MenuItem("递增复制", "YGINC ", "text_increment"),
                    MenuItem("加删前后缀", "YGTAFFIX ", "text_affix"),
                    MenuItem("刷文字内容", "YGTBRUSH ", "text_brush"),
                    MenuItem("互换文字内容", "YGTSWAP ", "text_swap"),
                    MenuItem("复制文字特性", "YGTPROP ", "text_prop"),
                    MenuItem("选择大小写转换方式", "YGTCASE ", "text_case"),
                    MenuItem("智能互转中英标点", "YGTPUNC ", "text_punc"),
                    MenuItem("文字排版设置", "YGTSET ", "text_settings")),
                Menu("拆合与清理", "text_join",
                    MenuItem("连接文字", "YGTJOIN ", "text_join"),
                    MenuItem("单行转多行", "YGTS2M ", "text_join"),
                    MenuItem("多行转单行", "YGTM2S ", "text_join"),
                    MenuItem("拆成单字", "YGTEXP ", "text_join"),
                    MenuItem("选择空格或换行清理方式", "YGTSPACE ", "text_space")),
                Menu("标注工具", "dim_check",
                    MenuItem("引线修复", "YGLEADERFIX ", "leader_fix"),
                    MenuItem("筛选改字标注", "YGDIMTXT ", "dim_override"),
                    MenuItem("标注坡度", "YGSLOPE ", "slope_mark"),
                    MenuItem("矩形或圆形文字框", "YGTBOX ", "text_frame"),
                    MenuItem("跑道形文字框", "YGTBOXSLOT ", "text_box_slot"),
                    MenuItem("带遮罩文字框", "YGTBOXMASK ", "text_box_mask"),
                    MenuItem("标注尾数检查", "DIMCHK ", "dim_check"),
                    MenuItem("标注取整", "DIMFIX ", "dim_fix"),
                    MenuItem("恢复标注原值", "DIMRE ", "dim_restore"),
                    MenuItem("翻转标注文字", "DIMFZ ", "dim_flip"),
                    MenuItem("让文字朝上", "TXTUP ", "text_up"),
                    MenuItem("标注检查与取整设置", "DFSET ", "dim_fix")));

            AddPanel(newTab, "结构绘图",
                Menu("双线", "double_line",
                    MenuItem("绘制双线", "YGDL ", "double_line"),
                    MenuItem("修改双线宽度", "YGDLW ", "double_line")),
                Menu("梁柱板", "beam_mark",
                    MenuItem("梁尺寸标注", "BMK ", "beam_mark"),
                    MenuItem("柱尺寸标注", "CMK ", "col_mark"),
                    MenuItem("布置板受力符号", "SLB ", "slab_sym"),
                    MenuItem("预览板格", "SLBP ", "slab_sym"),
                    MenuItem("修正板受力符号", "SLBFIX ", "slab_fix"),
                    MenuItem("设置板轮廓与梁线", "SLBLAYER ", "slab_layer"),
                    MenuItem("板符号设置", "SLBSET ", "slab_sym")));

            AddPanel(newTab, "图纸与引用",
                Button("Revit 预处理", "YGREVIT ", "ControlCenter32.png"),
                Menu("引用内嵌", "image_embed",
                    MenuItem("内嵌当前图纸", "YGEMBED ", "image_embed"),
                    MenuItem("批量内嵌文件夹", "YGEMBEDBATCH ", "folder_embed")));

            AddPanel(newTab, "连接与设置",
                Button("快捷键", "YGKEYS ", "ControlCenter32.png"),
                Button("MCP", "YANGCAD_MCP ", "Mcp32.png"),
                Button("控制中心", "YANGCAD ", "ControlCenter32.png"));

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

            var trimmed = command.Trim();
            var catalog = CadToolCatalog.All.FirstOrDefault(x =>
                string.Equals(x.Command, trimmed, StringComparison.OrdinalIgnoreCase));
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
