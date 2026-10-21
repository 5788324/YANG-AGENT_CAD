using System;
using System.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.UI
{
    public static class McpWindowManager
    {
        private static McpWindow? _window;

        public static void Show()
        {
            RuntimeHost.EnsureInitialized();
            if (_window != null && _window.IsLoaded)
            {
                if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
                _window.Activate();
                return;
            }

            _window = new McpWindow();
            _window.Closed += (_, __) => _window = null;
            AcApp.ShowModelessWindow(_window);
        }

        public static void ApplyTheme(string theme)
        {
            try { _window?.ApplyTheme(theme); }
            catch (Exception ex) { RuntimeLogger.Error("MCP window theme update failed.", ex); }
        }

        public static void Shutdown()
        {
            try { _window?.Close(); } catch { }
            _window = null;
        }
    }
}
