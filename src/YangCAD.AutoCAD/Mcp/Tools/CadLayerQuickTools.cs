using System;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public sealed class CadLayerMergeTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        { Name = "cad.layer.merge", Description = "将当前 DWG 所有本地图元（含普通块定义）从源图层移到目标图层；外参依赖图层不允许合并。默认 dry_run。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext c)
        {
            var source = ToolJson.RequiredString(c.Args, "source_layer");
            var target = ToolJson.RequiredString(c.Args, "target_layer");
            return CadTransactionRunner.WriteContext(c.Document, c.DryRun, ctx => LayerQuickService.Merge(ctx, source, target, c.DryRun));
        }
    }

    public abstract class CadLayerStateTool : ICadTool
    {
        protected abstract string Kind { get; }
        protected abstract string Flag { get; }
        protected abstract string ToolName { get; }
        public ToolDescriptor Descriptor => new ToolDescriptor
        { Name = ToolName, Description = "在当前 DWG 中更改图层状态，包括外参依赖图层。默认 dry_run，不修改源 DWG 外参。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext c)
        {
            var layer = ToolJson.RequiredString(c.Args, "layer");
            var value = ToolJson.Bool(c.Args, Flag) ?? throw new ArgumentException("缺少布尔参数：" + Flag);
            return CadTransactionRunner.WriteContext(c.Document, c.DryRun, ctx => LayerQuickService.State(ctx, layer, Kind, value, c.DryRun));
        }
    }
    public sealed class CadLayerVisibilityTool : CadLayerStateTool
    {
        protected override string Kind => "off";
        protected override string Flag => "off";
        protected override string ToolName => "cad.layer.visibility";
    }
    public sealed class CadLayerFreezeTool : CadLayerStateTool
    {
        protected override string Kind => "freeze";
        protected override string Flag => "frozen";
        protected override string ToolName => "cad.layer.freeze";
    }
    public sealed class CadLayerLockTool : CadLayerStateTool
    {
        protected override string Kind => "lock";
        protected override string Flag => "locked";
        protected override string ToolName => "cad.layer.lock";
    }

    public sealed class CadDetailExtractTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        { Name = "cad.detail.extract", Description = "从当前空间按矩形裁切提取大样，放置到目标点；原图保留，复杂跨界实体安全跳过。默认 dry_run。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext c)
        {
            var a = Point(c.Args, "first_corner");
            var b = Point(c.Args, "opposite_corner");
            var d = Point(c.Args, "destination");
            var request = new DetailExtractService.Request
            {
                FirstCorner = a, OppositeCorner = b, Destination = d,
                Factor = ToolJson.Double(c.Args, "factor") ?? 1,
                KeepDimensionsAndLeaders = ToolJson.Bool(c.Args, "keep_dimensions_and_leaders") ?? true,
                DrawFrame = ToolJson.Bool(c.Args, "draw_frame") ?? true
            };
            return CadTransactionRunner.WriteContext(c.Document, c.DryRun,
                ctx => DetailExtractService.Extract(ctx, request, c.DryRun));
        }
        private static Point3d Point(System.Collections.Generic.IReadOnlyDictionary<string, object?> args, string key)
        {
            var values = ToolJson.DoubleArray(args, key) ?? throw new ArgumentException("缺少坐标：" + key);
            if (values.Length != 2 && values.Length != 3) throw new ArgumentException(key + " 必须是 [X,Y] 或 [X,Y,Z]。");
            return new Point3d(values[0], values[1], values.Length == 3 ? values[2] : 0);
        }
    }
}
