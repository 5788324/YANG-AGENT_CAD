using System;
using System.Collections.Generic;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    /// <summary>
    /// Product-level MCP adapters.  GUI and MCP intentionally call the same Request/Service/Result
    /// business implementation; these classes contain protocol argument binding only.
    /// </summary>
    public sealed class ProjectRevitPrepareTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "project.revit.prepare",
            Description = "Revit 图纸预处理产品功能。读取当前活动 DWG，输出 yangcad-rvt-exchange/1.0 的 *_RVT.dwg / *_RVT_MAP.json / *_RVT_REPORT.json。默认 dry_run。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var request = new RevitExchangeService.Request
            {
                OutputFolder = (ToolJson.String(context.Args, "output_folder") ?? "").Trim(),
                OutputSuffix = (ToolJson.String(context.Args, "output_suffix") ?? "_RVT").Trim(),
                ScanBlockDefinitions = ToolJson.Bool(context.Args, "scan_blocks") ?? true,
                IncludePaperSpace = ToolJson.Bool(context.Args, "include_paperspace") ?? true,
                NormalizeGeometryOverrides = ToolJson.Bool(context.Args, "normalize_geometry") ?? true
            };

            return CadTransactionRunner.ReadContext(context.Document, cad =>
                context.DryRun
                    ? RevitExchangeService.Preview(cad.Database, cad.Transaction, context.Document.Name, request)
                    : RevitExchangeService.Export(cad.Database, cad.Transaction, context.Document.Name, request));
        }
    }

    public sealed class ProjectImageEmbedTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "project.image.embed",
            Description = "批量图片内嵌产品功能。输入单个 DWG 或目录，dry_run 返回输出计划；real 启动独立 AutoCAD worker 并持续写 status.json。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var timeout = ToolJson.Int(context.Args, "command_timeout_seconds") ?? 90;
            if (timeout < 30 || timeout > 600)
                throw new ArgumentOutOfRangeException("command_timeout_seconds", "command_timeout_seconds 必须在 30..600。 ");

            var request = new ImageEmbedBatchService.Request
            {
                InputPath = (ToolJson.String(context.Args, "input_path") ?? "").Trim(),
                OutputFolder = (ToolJson.String(context.Args, "output_folder") ?? "").Trim(),
                IncludeSubfolders = ToolJson.Bool(context.Args, "include_subfolders") ?? true,
                ShowWorker = ToolJson.Bool(context.Args, "show_worker") ?? false,
                RequireAllImages = ToolJson.Bool(context.Args, "require_all_images") ?? true,
                CommandTimeoutSeconds = timeout
            };

            return context.DryRun ? ImageEmbedBatchService.Preview(request) : ImageEmbedBatchService.Start(request);
        }
    }
}
