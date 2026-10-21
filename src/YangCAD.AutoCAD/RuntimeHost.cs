using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.AutoCADHost.Mcp.Generic;
using YangCAD.AutoCADHost.Mcp.Extensions;

namespace YangCAD.AutoCADHost
{
    public static class RuntimeHost
    {
        private static readonly object Sync = new object();
        private static bool _initialized;

        public static ToolRegistry Tools { get; } = new ToolRegistry();
        public static CadRequestBroker Broker { get; private set; } = null!;
        public static LocalGateway Gateway { get; private set; } = null!;

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;

                // Hard P0 gate: RuntimeHost is document-bound infrastructure. The Start page has no
                // Document/Editor/command context, so never create the Gateway/Broker there.
                if (Application.DocumentManager.MdiActiveDocument == null)
                    throw new InvalidOperationException("YangCAD MCP 仅在真实 DWG 激活后启动；当前仍处于 Start 页/无活动文档状态。");

                RuntimeLogger.Info("RuntimeHost registering MCP core tools.");
                RegisterCoreTools();
                RuntimeLogger.Info("RuntimeHost core tools ready. count=" + Tools.Describe().Length);

                var broker = new CadRequestBroker(Tools);
                LocalGateway? gateway = null;
                try
                {
                    RuntimeLogger.Info("RuntimeHost starting Broker.");
                    broker.Start();
                    gateway = new LocalGateway(broker);
                    RuntimeLogger.Info("RuntimeHost starting Gateway.");
                    gateway.Start();
                    Broker = broker;
                    Gateway = gateway;
                    _initialized = true;
                    RuntimeLogger.Info("RuntimeHost MCP core initialized. Gateway=127.0.0.1:" + gateway.Port);
                }
                catch (Exception ex)
                {
                    RuntimeLogger.Error("RuntimeHost MCP core initialization failed.", ex);
                    try { gateway?.Dispose(); } catch { }
                    try { broker.Dispose(); } catch { }
                    InstanceRegistry.DeleteCurrent();
                    throw;
                }

                // Generic fallback is valuable, but it is optional infrastructure. A type-load or
                // registration regression here must not prevent the proven TASK-001 MCP core from starting.
                RuntimeLogger.Info("RuntimeHost registering optional Generic MCP tools.");
                RegisterOptional("cad.object.describe", () => new CadObjectDescribeTool());
                RegisterOptional("cad.property.get", () => new CadPropertyGetTool());
                RegisterOptional("cad.property.set", () => new CadPropertySetTool());
                RegisterOptional("cad.api.describe", () => new CadApiDescribeTool());
                RegisterOptional("cad.api.invoke", () => new CadApiInvokeTool());
                RegisterOptional("cad.command.run", () => new CadCommandRunTool());
                RegisterOptional("cad.extension.list", () => new CadExtensionListTool());
                RegisterOptional("cad.extension.invoke", () => new CadExtensionInvokeTool());

                // TASK-003 business Tools are also optional-at-startup. A regression in a new
                // productivity handler must never take down the TASK-001 MCP Core/Gateway.
                RuntimeLogger.Info("RuntimeHost registering TASK-003 CAD Productivity tools.");
                RegisterOptional("cad.line.create", () => new CadLineCreateTool());
                RegisterOptional("cad.polyline.create", () => new CadPolylineCreateTool());
                RegisterOptional("cad.text.create", () => new CadTextCreateTool());
                RegisterOptional("cad.block.list", () => new CadBlockListTool());
                RegisterOptional("cad.block.insert", () => new CadBlockInsertTool());

                RuntimeLogger.Info("RuntimeHost registering product-level project tools.");
                RegisterOptional("project.revit.prepare", () => new ProjectRevitPrepareTool());
                RegisterOptional("project.image.embed", () => new ProjectImageEmbedTool());
                RegisterOptional("cad.scale.convert", () => new CadScaleConvertTool());

                RuntimeLogger.Info("RuntimeHost registering TASK-006A Production Hygiene tools.");
                RegisterOptional("cad.polyline.inspect", () => new CadPolylineInspectTool());
                RegisterOptional("cad.polyline.clean", () => new CadPolylineCleanTool());
                RegisterOptional("cad.layer.audit", () => new CadLayerAuditTool());
                RegisterOptional("cad.layer.cleanup", () => new CadLayerCleanupTool());
                RegisterOptional("cad.drawing.audit", () => new CadDrawingAuditTool());
                RuntimeLogger.Info("RuntimeHost optional registration complete. total_tools=" + Tools.Describe().Length);
            }
        }

        private static void RegisterCoreTools()
        {
            Tools.Register(new CadStatusTool());
            Tools.Register(new CadDocumentInfoTool());
            Tools.Register(new CadSelectionReadTool());
            Tools.Register(new CadEntityGetTool());
            Tools.Register(new CadEntityQueryTool());
            Tools.Register(new CadEntityUpdateTool());
            Tools.Register(new CadEntityTransformTool());
            Tools.Register(new CadEntityEraseTool());
            Tools.Register(new CadLayerListTool());
            Tools.Register(new CadLayerEnsureTool());
        }

        private static void RegisterOptional(string name, Func<ICadTool> factory)
        {
            try
            {
                Tools.Register(factory());
                RuntimeLogger.Info("Optional tool registered: " + name);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Optional tool disabled for this session: " + name, ex);
            }
        }

        public static void EnsureInitialized() { if (!_initialized) Initialize(); }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (!_initialized) return;
                try { Gateway?.Dispose(); } catch { }
                try { Broker?.Dispose(); } catch { }
                InstanceRegistry.DeleteCurrent();
                _initialized = false;
            }
        }
    }
}
