using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.Civil3DHost.Tools;

[assembly: ExtensionApplication(typeof(YangCAD.Civil3DHost.CivilExtensionApplication))]

namespace YangCAD.Civil3DHost
{
    public sealed class CivilExtensionApplication : IExtensionApplication
    {
        private static bool _hooked;
        private static bool _registered;

        public void Initialize()
        {
            // Do not call PRODUCT/Civil APIs while Civil 3D is still showing the Start page.
            RuntimeLogger.Info("YangCAD.Civil3D Initialize deferred until DocumentActivated.");
            if (!_hooked)
            {
                AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                _hooked = true;
            }
            try
            {
                if (AcApp.DocumentManager.MdiActiveDocument != null) TryRegister("Initialize-active-document");
            }
            catch (System.Exception ex) { RuntimeLogger.Error("Civil initial active-document probe failed.", ex); }
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e) => TryRegister("DocumentActivated");

        private static void TryRegister(string reason)
        {
            if (_registered) return;
            try
            {
                if (AcApp.DocumentManager.MdiActiveDocument == null) return;
                if (!IsCivil3DHost()) return;

                // Some Civil 3D 2024 builds report PRODUCT="AutoCAD". Once the Civil runtime
                // probe succeeds, publish the real host identity to cad.status/UI/health.
                CadHostInfo.SetProductOverride("Civil 3D");
                YangCAD.AutoCADHost.ExtensionApplication.StartForActiveDocument("Civil-" + reason);
                YangCAD.AutoCADHost.Mcp.InstanceRegistry.RefreshCurrentIfChanged();
                var tools = YangCAD.AutoCADHost.RuntimeHost.Tools;
                tools.Register(new CivilStatusTool());
                tools.Register(new CivilSurfaceListTool());
                tools.Register(new CivilAlignmentListTool());
                tools.Register(new CivilAlignmentStationOffsetTool());
                tools.Register(new CivilStructureListTool());
                tools.Register(new CivilStructureGetTool());
                tools.Register(new CivilStructureUpdateTool());
                tools.Register(new CivilPipeListTool());
                tools.Register(new CivilPipeGetTool());
                tools.Register(new CivilPipeUpdateTool());
                tools.Register(new CivilSurfaceGetTool());
                tools.Register(new CivilSurfaceElevationTool());
                tools.Register(new CivilSurfaceBoundariesTool());
                tools.Register(new CivilAlignmentPointLocationTool());
                tools.Register(new CivilProfileListTool());
                tools.Register(new CivilProfileGetTool());
                tools.Register(new CivilProfileElevationTool());
                tools.Register(new CivilProfilePviListTool());
                tools.Register(new CivilCogoPointListTool());
                tools.Register(new CivilCogoPointGetTool());
                tools.Register(new CivilPressureNetworkListTool());
                tools.Register(new CivilPressureNetworkGetTool());
                tools.Register(new CivilFeatureLineListTool());
                tools.Register(new CivilFeatureLineGetTool());
                tools.Register(new CivilFeatureLinePointsTool());
                tools.Register(new CivilFeatureLineDiagnoseTool());
                tools.Register(new CivilCorridorListTool());
                tools.Register(new CivilCorridorGetTool());
                tools.Register(new CivilCorridorBaselinesTool());
                tools.Register(new CivilCorridorDiagnoseTool());
                _registered = true;
                RuntimeLogger.Info("YangCAD.Civil3D tools registered after active DWG. total_tools=" + tools.Describe().Length);
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YangCAD.Civil3D deferred registration failed; AutoCAD MCP core remains isolated.", ex);
            }
        }

        public void Terminate()
        {
            if (_hooked)
            {
                try { AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated; } catch { }
                _hooked = false;
            }
        }

        private static bool IsCivil3DHost()
        {
            try
            {
                var product = Convert.ToString(AcApp.GetSystemVariable("PRODUCT")) ?? "";
                if (product.IndexOf("Civil 3D", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                // Some Civil 3D 2024 builds report PRODUCT="AutoCAD" (verified on this machine).
                // Fall back to the TASK-001-proven runtime probe: CivilApplication.ActiveDocument
                // resolves only inside a real Civil 3D process and throws in plain AutoCAD /
                // Object Enabler hosts, preserving host isolation.
                try
                {
                    var civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
                    return civilDoc != null;
                }
                catch { return false; }
            }
            catch { return false; }
        }
    }
}
