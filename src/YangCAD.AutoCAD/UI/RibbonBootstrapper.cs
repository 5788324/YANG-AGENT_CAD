using System;
using System.Collections.Generic;
using System.Linq;
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

            var quickSource = new RibbonPanelSource { Title = "常用工具" };
            quickSource.Items.Add(Button("工具箱", "YANGCAD_TOOLBOX ", "ControlCenter32.png"));
            quickSource.Items.Add(Button("比例转换", "YGSCALE ", "ControlCenter32.png"));
            SetDialogLauncher(quickSource, "打开控制中心与外观设置 (YANGCAD)", "YANGCAD ");

            var layerSource = new RibbonPanelSource { Title = "图层" };
            layerSource.Items.Add(Button("图层工具", "YGLAYER ", "ControlCenter32.png"));
            layerSource.Items.Add(CreateRowStack(
                SmallItem("图层开关", "YGLONOFF ", "layer_off"),
                SmallItem("合并图层", "YGLMERGE ", "layer_merge"),
                SmallItem("图层冻结", "YGLFREEZE ", "layer_freeze")));
            layerSource.Items.Add(CreateRowStack(
                SmallItem("图层锁定", "YGLLOCK ", "layer_lock")));
            SetDialogLauncher(layerSource, "打开图层面板与清理设置 (YGLAYER)", "YGLAYER ");

            var textSource = new RibbonPanelSource { Title = "文字与排版" };
            textSource.Items.Add(SplitLarge("文字对齐",
                MenuItem("文字左对齐 (YGTL)", "YGTL ", "text_align_lr"),
                MenuItem("水平居中对齐 (YGTC)", "YGTC ", "text_align_lr"),
                MenuItem("文字右对齐 (YGTR)", "YGTR ", "text_align_lr"),
                MenuItem("文字顶对齐 (YGTT)", "YGTT ", "text_align_lr"),
                MenuItem("文字底对齐 (YGTB)", "YGTB ", "text_align_lr"),
                MenuItem("垂直居中对齐 (YGTV)", "YGTV ", "text_align_lr"),
                MenuItem("按线投影对齐 (YGTLINE)", "YGTLINE ", "text_align_lr"),
                MenuItem("按弧环绕对齐 (YGTARC)", "YGTARC ", "text_arc"),
                MenuItem("按段落整理对齐 (TXAL)", "TXAL ", "text_align")));
            textSource.Items.Add(LargeItem("递增复制", "YGINC ", "text_increment"));
            textSource.Items.Add(CreateRowStack(
                SplitSmall("去空格/换行",
                    MenuItem("交互选择清理模式 (YGTSPACE)", "YGTSPACE ", "text_space"),
                    MenuItem("去除多余空格 (YGTSPACEM)", "YGTSPACEM ", "text_space"),
                    MenuItem("去换行并拼接 (YGTNOLINE)", "YGTNOLINE ", "text_space"),
                    MenuItem("去除多余空行 (YGTTRIMLINE)", "YGTTRIMLINE ", "text_space")),
                SplitSmall("改大小写",
                    MenuItem("交互选择大小写 (YGTCASE)", "YGTCASE ", "text_case"),
                    MenuItem("全大写 UPPER (YGTUPPER)", "YGTUPPER ", "text_case"),
                    MenuItem("全小写 lower (YGTLOWER)", "YGTLOWER ", "text_case"),
                    MenuItem("首字母大写 Title (YGTTITLE)", "YGTTITLE ", "text_case"),
                    MenuItem("驼峰命名 camelCase (YGTCAMEL)", "YGTCAMEL ", "text_case")),
                SplitSmall("中英标点",
                    MenuItem("智能互转标点 (YGTPUNC)", "YGTPUNC ", "text_punc"),
                    MenuItem("全部转为英文标点 (YGTPUNCE)", "YGTPUNCE ", "text_punc"),
                    MenuItem("全部转为中文标点 (YGTPUNCC)", "YGTPUNCC ", "text_punc"))));
            textSource.Items.Add(CreateRowStack(
                SplitSmall("拆合文字",
                    MenuItem("连接文字 (YGTJOIN)", "YGTJOIN ", "text_join"),
                    MenuItem("单行转多行 (YGTS2M)", "YGTS2M ", "text_join"),
                    MenuItem("多行转单行 (YGTM2S)", "YGTM2S ", "text_join"),
                    MenuItem("文字炸碎为单字 (YGTEXP)", "YGTEXP ", "text_join")),
                SplitSmall("刷/换内容",
                    MenuItem("刷文字内容 (YGTBRUSH)", "YGTBRUSH ", "text_brush"),
                    MenuItem("文字内容互换 (YGTSWAP)", "YGTSWAP ", "text_swap"),
                    MenuItem("文字改特性刷 (YGTPROP)", "YGTPROP ", "text_prop")),
                SmallItem("加删前后缀", "YGTAFFIX ", "text_affix")));
            SetDialogLauncher(textSource, "文字排版默认参数设置 (YGTSET)", "YGTSET ");

            var modifyAnnotSource = new RibbonPanelSource { Title = "详图与标注" };
            modifyAnnotSource.Items.Add(Button("提取大样", "YGDETAIL ", "ControlCenter32.png"));
            modifyAnnotSource.Items.Add(LargeItem("引线修复", "YGLEADERFIX ", "leader_fix"));
            modifyAnnotSource.Items.Add(SplitLarge("填充工具",
                MenuItem("剪裁填充图案 (YGHCLIP / hhJ)", "YGHCLIP ", "hatch_clip"),
                MenuItem("挖出并保留独立填充 (YGHCLIPSPLIT)", "YGHCLIPSPLIT ", "hatch_clip_split"),
                MenuItem("局部裁切-仅留框内 (YGHCLIPINT)", "YGHCLIPINT ", "hatch_clip"),
                MenuItem("拆分填充图案 (YGHSPLIT / hhX)", "YGHSPLIT ", "hatch_split"),
                MenuItem("合并填充图案 (YGHUNION / hhU)", "YGHUNION ", "hatch_union"),
                MenuItem("去除填充关联 (YGHDIS / hhGL)", "YGHDIS ", "hatch_disasso"),
                MenuItem("提取重建边界 (YGHBOUND / hhB)", "YGHBOUND ", "hatch_bound"),
                MenuItem("重设填充基点 (YGHBASE / hhI)", "YGHBASE ", "hatch_base")));
            modifyAnnotSource.Items.Add(CreateRowStack(
                SmallItem("找改值标注", "YGDIMTXT ", "dim_override"),
                SmallItem("标注坡度", "YGSLOPE ", "slope_mark"),
                SplitSmall("文字加框",
                    MenuItem("矩形/圆框文字 (YGTBOX)", "YGTBOX ", "text_frame"),
                    MenuItem("圆角跑道框 (YGTBOXSLOT)", "YGTBOXSLOT ", "text_box_slot"),
                    MenuItem("遮罩外框 (YGTBOXMASK)", "YGTBOXMASK ", "text_box_mask"))));
            modifyAnnotSource.Items.Add(CreateRowStack(
                SplitSmall("标注检查",
                    MenuItem("标注尾数检查 (DIMCHK)", "DIMCHK ", "dim_check"),
                    MenuItem("标注取整 (DIMFIX)", "DIMFIX ", "dim_fix"),
                    MenuItem("恢复原值 (DIMRE)", "DIMRE ", "dim_restore"),
                    MenuItem("翻转标注文字 (DIMFZ)", "DIMFZ ", "dim_flip")),
                SmallItem("文字向上", "TXTUP ", "text_up"),
                SmallItem("按弧环绕", "YGTARC ", "text_arc")));
            SetDialogLauncher(modifyAnnotSource, "标注检查/取整默认参数设置 (DFSET)", "DFSET ");

            var structSource = new RibbonPanelSource { Title = "结构与双线" };
            structSource.Items.Add(LargeItem("智能双线", "YGDL ", "double_line"));
            structSource.Items.Add(LargeItem("梁尺寸标注", "BMK ", "beam_mark"));
            structSource.Items.Add(LargeItem("柱尺寸标注", "CMK ", "col_mark"));
            structSource.Items.Add(CreateRowStack(
                SplitSmall("板受力符号",
                    MenuItem("连续布置板符号 (SLB)", "SLB ", "slab_sym"),
                    MenuItem("单点插入板符号 (SLBP)", "SLBP ", "slab_sym"),
                    MenuItem("板符号尺寸修正 (SLBFIX)", "SLBFIX ", "slab_fix")),
                SmallItem("板梁图层", "SLBLAYER ", "slab_layer")));
            SetDialogLauncher(structSource, "板符号尺寸与线宽设置 (SLBSET)", "SLBSET ");

            var specialSource = new RibbonPanelSource { Title = "专项与批处理" };
            specialSource.Items.Add(LargeItem("Revit预处理", "YGREVIT ", "revit_prep"));
            specialSource.Items.Add(SplitLarge("图片内嵌",
                MenuItem("当前图纸图片内嵌 (YGEMBED)", "YGEMBED ", "image_embed"),
                MenuItem("文件夹批量内嵌 (YGEMBEDBATCH)", "YGEMBEDBATCH ", "folder_embed")));

            var systemSource = new RibbonPanelSource { Title = "连接与设置" };
            systemSource.Items.Add(LargeItem("快捷键", "YGKEYS ", "shortcut_keys"));
            systemSource.Items.Add(Button("MCP", "YANGCAD_MCP ", "Mcp32.png"));
            systemSource.Items.Add(Button("控制中心", "YANGCAD ", "ControlCenter32.png"));
            SetDialogLauncher(systemSource, "自定义命令快捷键 (YGKEYS)", "YGKEYS ");

            newTab.Panels.Add(new RibbonPanel { Source = quickSource });
            newTab.Panels.Add(new RibbonPanel { Source = layerSource });
            newTab.Panels.Add(new RibbonPanel { Source = textSource });
            newTab.Panels.Add(new RibbonPanel { Source = modifyAnnotSource });
            newTab.Panels.Add(new RibbonPanel { Source = structSource });
            newTab.Panels.Add(new RibbonPanel { Source = specialSource });
            newTab.Panels.Add(new RibbonPanel { Source = systemSource });

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

        private static RibbonRowPanel CreateRowStack(params RibbonItem[] items)
        {
            var rowPanel = new RibbonRowPanel();
            for (var i = 0; i < items.Length; i++)
            {
                if (i > 0) rowPanel.Items.Add(new RibbonRowBreak());
                rowPanel.Items.Add(items[i]);
            }
            return rowPanel;
        }

        private static RibbonButton Button(string text, string command, string iconFile)
        {
            return BuildButton(text, command, command.Trim(), iconFile, RibbonItemSize.Large, System.Windows.Controls.Orientation.Vertical);
        }

        private static RibbonButton LargeItem(string text, string command, string iconKey)
        {
            return BuildButton(text, command, iconKey, "ControlCenter32.png", RibbonItemSize.Large, System.Windows.Controls.Orientation.Vertical);
        }

        private static RibbonButton SmallItem(string text, string command, string iconKey)
        {
            return BuildButton(text, command, iconKey, "ControlCenter32.png", RibbonItemSize.Standard, System.Windows.Controls.Orientation.Horizontal);
        }

        private static RibbonButton MenuItem(string text, string command, string iconKey)
        {
            return BuildButton(text, command, iconKey, "ControlCenter32.png", RibbonItemSize.Standard, System.Windows.Controls.Orientation.Horizontal);
        }

        private static RibbonSplitButton SplitLarge(string groupText, params RibbonButton[] items)
        {
            return BuildSplitButton(groupText, RibbonItemSize.Large, System.Windows.Controls.Orientation.Vertical, items);
        }

        private static RibbonSplitButton SplitSmall(string groupText, params RibbonButton[] items)
        {
            return BuildSplitButton(groupText, RibbonItemSize.Standard, System.Windows.Controls.Orientation.Horizontal, items);
        }

        private static RibbonSplitButton BuildSplitButton(
            string groupText,
            RibbonItemSize size,
            System.Windows.Controls.Orientation orientation,
            params RibbonButton[] items)
        {
            var split = new RibbonSplitButton
            {
                Text = groupText,
                ShowText = true,
                ShowImage = true,
                IsSplit = true,
                IsSynchronizedWithCurrentItem = true,
                Size = size,
                Orientation = orientation,
                ListStyle = RibbonSplitButtonListStyle.Icon,
                ListImageSize = RibbonImageSize.Standard
            };

            foreach (var item in items)
            {
                if (item != null) split.Items.Add(item);
            }
            if (split.Items.Count > 0)
            {
                split.Current = split.Items[0];
            }
            return split;
        }

        private static void SetDialogLauncher(RibbonPanelSource source, string tooltipText, string command)
        {
            if (source == null) return;
            source.DialogLauncher = new RibbonCommandItem
            {
                Text = tooltipText,
                CommandHandler = new Handler(command),
                ToolTip = BuildToolTip(tooltipText, command)
            };
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

            var button = new RibbonButton
            {
                Text = text,
                ShowText = true,
                ShowImage = (size == RibbonItemSize.Large ? large : small) != null,
                Size = size,
                Orientation = orientation,
                Image = small,
                LargeImage = large,
                CommandHandler = new Handler(command),
                ToolTip = BuildToolTip(text, command)
            };
            return button;
        }

        private static RibbonToolTip BuildToolTip(string text, string command)
        {
            var trimmed = (command ?? "").Trim();
            var catalog = CadToolCatalog.All.FirstOrDefault(x =>
                string.Equals(x.Command, trimmed, StringComparison.OrdinalIgnoreCase));
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
                var prefix = "";
                try
                {
                    var activeCommands = Convert.ToString(Application.GetSystemVariable("CMDNAMES"));
                    if (!string.IsNullOrWhiteSpace(activeCommands)) prefix = "\x03\x03";
                }
                catch { }
                document.SendStringToExecute(prefix + _command, true, false, false);
            }
        }
    }
}
