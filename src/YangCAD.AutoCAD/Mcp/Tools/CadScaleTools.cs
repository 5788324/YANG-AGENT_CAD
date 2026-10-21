using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public sealed class CadScaleConvertTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.scale.convert",
            Description = "图纸比例转换。standard_1to1 保持真实几何不变并调整模型空间注释显示尺度；scaled_drawing 按旧比例/新比例缩放几何并恢复纸面文字、标注、引线、Hatch、线型大小。默认 dry_run。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handles = ReadHandles(context.Args);
            var oldScale = ToolJson.Double(context.Args, "old_scale")
                ?? throw new ArgumentException("缺少参数：old_scale");
            var newScale = ToolJson.Double(context.Args, "new_scale")
                ?? throw new ArgumentException("缺少参数：new_scale");
            var mode = ScaleConversionService.ParseMode(ToolJson.String(context.Args, "mode") ?? "standard_1to1");
            var point = ToolJson.DoubleArray(context.Args, "base_point") ?? new[] { 0.0, 0.0, 0.0 };
            if (point.Length < 2 || point.Length > 3) throw new ArgumentException("base_point 必须是 [X,Y] 或 [X,Y,Z]。");

            var request = new ScaleConversionService.Request
            {
                Handles = handles,
                Mode = mode,
                OldScale = oldScale,
                NewScale = newScale,
                BasePoint = new Point3d(point[0], point[1], point.Length > 2 ? point[2] : 0.0),
                PreserveTextPaperSize = ToolJson.Bool(context.Args, "preserve_text_paper_size") ?? true,
                PreserveDimensionPaperSize = ToolJson.Bool(context.Args, "preserve_dimension_paper_size") ?? true,
                PreserveLeaderPaperSize = ToolJson.Bool(context.Args, "preserve_leader_paper_size") ?? true,
                PreserveHatchPaperScale = ToolJson.Bool(context.Args, "preserve_hatch_paper_scale") ?? true,
                PreserveLinetypePaperScale = ToolJson.Bool(context.Args, "preserve_linetype_paper_scale") ?? true
            };

            return CadTransactionRunner.WriteContext(context.Document, context.DryRun, execution =>
                ScaleConversionService.Convert(execution, request, context.DryRun));
        }

        private static string[] ReadHandles(IReadOnlyDictionary<string, object?> args)
        {
            var values = ToolJson.List(args, "handles") ?? throw new ArgumentException("缺少参数：handles");
            var handles = values.Select(value => Convert.ToString(value)?.Trim() ?? "")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (handles.Length == 0) throw new ArgumentException("handles 至少包含一个实体 Handle。");
            return handles;
        }
    }
}
