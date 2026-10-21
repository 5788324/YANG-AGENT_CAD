using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.UI;

[assembly: ExtensionApplication(typeof(YangCAD.AutoCADHost.ExtensionApplication))]

namespace YangCAD.AutoCADHost
{
    public sealed class ExtensionApplication : IExtensionApplication
    {
        private static bool _documentHooked;
        private static bool _diagnosticsHooked;

        public void Initialize()
        {
            // P0 rule: the AutoCAD Start page is NOT a document/command context.  Do not start the
            // MCP runtime, touch AdWindows, or query CAD system variables until a real DWG is active.
            RuntimeLogger.Info("YangCAD.AutoCAD Initialize begin (start-page safe; MCP deferred until DocumentActivated).");
            HookDiagnostics();
            HookDocumentActivated();

            try
            {
                // NETLOAD/autoload can also happen after a drawing is already active.  This read is
                // intentionally the only Autodesk host probe in Initialize; all heavy work is gated.
                if (AcApp.DocumentManager.MdiActiveDocument != null)
                    StartForActiveDocument("Initialize-active-document");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("Initial active-document probe failed; YangCAD remains deferred.", ex);
            }

            RuntimeLogger.Info("YangCAD.AutoCAD Initialize return.");
        }

        private static void HookDocumentActivated()
        {
            if (_documentHooked) return;
            AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            _documentHooked = true;
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            StartForActiveDocument("DocumentActivated");
        }

        public static void StartForActiveDocument(string reason)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    RuntimeLogger.Info("MCP start skipped (no active DWG). reason=" + reason);
                    return;
                }

                RuntimeLogger.Info("MCP start gate opened. reason=" + reason + ", document=" + doc.Name);
                RuntimeHost.EnsureInitialized();
                RibbonBootstrapper.ScheduleEnsureRibbon();
                ToolboxPaletteManager.RefreshContext();
                RuntimeLogger.Info("MCP runtime ready after document activation. Gateway=127.0.0.1:" + RuntimeHost.Gateway.Port);
            }
            catch (System.Exception ex)
            {
                // Never rethrow from an Autodesk document event.
                RuntimeLogger.Error("Deferred MCP startup failed; host remains alive. reason=" + reason, ex);
            }
        }

        private static void HookDiagnostics()
        {
            if (_diagnosticsHooked) return;
            _diagnosticsHooked = true;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    RuntimeLogger.Error("AppDomain unhandled exception. terminating=" + e.IsTerminating,
                        e.ExceptionObject as System.Exception ?? new System.Exception(Convert.ToString(e.ExceptionObject)));
                }
                catch { }
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try { RuntimeLogger.Error("TaskScheduler unobserved exception.", e.Exception); }
                catch { }
            };
        }

        public void Terminate()
        {
            RuntimeLogger.Info("YangCAD.AutoCAD Terminate.");
            if (_documentHooked)
            {
                try { AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated; } catch { }
                _documentHooked = false;
            }
            McpWindowManager.Shutdown();
            ToolboxPaletteManager.Shutdown();
            RibbonBootstrapper.Shutdown();
            RuntimeHost.Shutdown();
        }
    }
}
