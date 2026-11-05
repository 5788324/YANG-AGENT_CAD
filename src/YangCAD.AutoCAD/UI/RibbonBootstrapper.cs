using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.Windows;
using YangCAD.AutoCADHost.Infrastructure;

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
            var quickPanel = new RibbonPanel { Source = quickSource };
            var systemSource = new RibbonPanelSource { Title = "连接与设置" };
            var systemPanel = new RibbonPanel { Source = systemSource };

            // Use embedded bitmap resources. Runtime vector images are valid WPF ImageSource values, but Autodesk
            // ribbon code is historically exercised with BitmapSource/BitmapImage. Keeping bitmap
            // assets also prevents vector parsing/rendering from participating in host startup.
            quickSource.Items.Add(Button("工具箱", "YANGCAD_TOOLBOX ", "ControlCenter32.png"));
            quickSource.Items.Add(Button("比例转换", "YGSCALE ", "ControlCenter32.png"));
            quickSource.Items.Add(Button("图层工具", "YGLAYER ", "ControlCenter32.png"));
            quickSource.Items.Add(Button("提取大样", "YGDETAIL ", "ControlCenter32.png"));
            systemSource.Items.Add(Button("MCP", "YANGCAD_MCP ", "Mcp32.png"));
            systemSource.Items.Add(Button("控制中心", "YANGCAD ", "ControlCenter32.png"));
            newTab.Panels.Add(quickPanel);
            newTab.Panels.Add(systemPanel);
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

        private static RibbonButton Button(string text, string command, string iconFile)
        {
            ImageSource? small = null;
            ImageSource? large = null;
            try
            {
                small = LoadBitmap(iconFile, 16);
                large = LoadBitmap(iconFile, 32);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Ribbon icon failed; falling back to text-only button: " + iconFile, ex);
            }

            return new RibbonButton
            {
                Text = text,
                ShowText = true,
                ShowImage = large != null,
                Size = RibbonItemSize.Large,
                Image = small,
                LargeImage = large,
                CommandHandler = new Handler(command)
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
