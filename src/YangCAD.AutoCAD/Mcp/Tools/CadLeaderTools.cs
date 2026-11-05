using System;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    public sealed class CadLeaderRebuildTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.leader.rebuild",
            Description = "把一条旧 Leader、无文字 MLeader 或连通的 LINE/开放 Polyline 重建为原生 MLeader；可显式关联 TEXT/MTEXT。默认 dry_run，不自动匹配邻近文字。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var list = ToolJson.List(context.Args, "source_handles")
                ?? throw new ArgumentException("source_handles 不能为空。");
            var handles = list.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            var arrow = ToolJson.DoubleArray(context.Args, "arrow_point");
            if (arrow != null && (arrow.Length < 2 || arrow.Length > 3))
                throw new ArgumentException("arrow_point 必须是 [X,Y] 或 [X,Y,Z]。");
            var request = new LeaderRepairService.Request
            {
                SourceHandles = handles,
                TextHandle = ToolJson.String(context.Args, "text_handle") ?? "",
                ArrowPoint = arrow == null ? (Point3d?)null : new Point3d(arrow[0], arrow[1], arrow.Length == 3 ? arrow[2] : 0),
                KeepOriginals = ToolJson.Bool(context.Args, "keep_originals") ?? false
            };
            return CadTransactionRunner.WriteContext(context.Document, context.DryRun,
                execution => LeaderRepairService.Rebuild(execution, request, context.DryRun));
        }
    }
}
