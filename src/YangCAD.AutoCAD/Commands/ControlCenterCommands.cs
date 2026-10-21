using System;
using System.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.UI;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class ControlCenterCommands
    {
        private static ControlCenterWindow? _window;

        [CommandMethod("YANGCAD")]
        public void OpenControlCenter()
        {
            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
                    _window.Activate();
                    return;
                }

                _window = new ControlCenterWindow();
                _window.Closed += (_, __) => _window = null;
                AcApp.ShowModelessWindow(_window);
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YANGCAD command failed.", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\n[YangCAD] 控制中心启动失败：" + ex.Message +
                    "\n[YangCAD] 日志：%LOCALAPPDATA%\\YangCADAgent\\logs\\runtime.log\n");
            }
        }


        [CommandMethod("YANGCAD_MCP")]
        public void OpenMcp()
        {
            try
            {
                McpWindowManager.Show();
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YANGCAD_MCP command failed.", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\n[YangCAD] MCP 窗口启动失败：" + ex.Message +
                    "\n[YangCAD] 日志：%LOCALAPPDATA%\\YangCADAgent\\logs\\runtime.log\n");
            }
        }

        [CommandMethod("YANGCAD_MCP_STATUS")]
        public void McpStatus()
        {
            try
            {
                RuntimeHost.EnsureInitialized();
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var snapshot = YangCAD.AutoCADHost.Mcp.InstanceRegistry.Snapshot();
                var settings = UserSettings.Load();
                doc?.Editor.WriteMessage(
                    $"\nYangCAD MCP: 127.0.0.1:{(RuntimeHost.Gateway != null && RuntimeHost.Gateway.IsRunning ? RuntimeHost.Gateway.Port : 0)}; tools={RuntimeHost.Tools.Describe().Length}; PID={snapshot.ProcessId}" +
                    $"\nDocument={snapshot.Document}; Session={snapshot.DocumentId}" +
                    $"\nWrites={settings.AllowWrites}; Erase={settings.AllowErase}; Command={settings.AllowCommandExecution}; Extensions={settings.AllowExtensionInvoke}\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YANGCAD_MCP_STATUS failed.", ex);
                AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[YangCAD] MCP 未就绪：" + ex.Message + "\n");
            }
        }
    }
}
