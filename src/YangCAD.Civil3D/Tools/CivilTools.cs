using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.Core.Tools;

namespace YangCAD.Civil3DHost.Tools
{
    public sealed class CivilStatusTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.status", Description = "Civil 3D API 状态。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var d = CivilApplication.ActiveDocument;
            return new { available = d != null, document = c.Document.Name, surface_count = d?.GetSurfaceIds().Count ?? 0, alignment_count = d?.GetAlignmentIds().Count ?? 0 };
        }
    }

    public sealed class CivilSurfaceListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.surface.list", Description = "列出 Surface。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var d = CivilApplication.ActiveDocument ?? throw new InvalidOperationException("Civil 3D 当前没有活动 CivilDocument。");
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var rows = new List<object>();
                foreach (ObjectId id in d.GetSurfaceIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Autodesk.Civil.DatabaseServices.Surface surface)
                        rows.Add(new { handle = id.Handle.ToString(), name = surface.Name, type = surface.GetType().Name });
                }
                return new { count = rows.Count, surfaces = rows };
            });
        }
    }

    public sealed class CivilAlignmentListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.alignment.list", Description = "列出 Alignment 与桩号范围。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var d = CivilApplication.ActiveDocument ?? throw new InvalidOperationException("Civil 3D 当前没有活动 CivilDocument。");
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var rows = new List<object>();
                foreach (ObjectId id in d.GetAlignmentIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Alignment a)
                        rows.Add(new { handle = id.Handle.ToString(), name = a.Name, starting_station = a.StartingStation, ending_station = a.EndingStation, length = a.Length });
                }
                return new { count = rows.Count, alignments = rows };
            });
        }
    }

    public sealed class CivilAlignmentStationOffsetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.alignment.station_offset", Description = "XY → Alignment 桩号/偏距。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var x = ToolJson.Double(c.Args, "x") ?? throw new ArgumentException("缺少参数：x");
            var y = ToolJson.Double(c.Args, "y") ?? throw new ArgumentException("缺少参数：y");
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var alignment = tr.GetObject(EntityHelpers.FromHandle(c.Database, handle), OpenMode.ForRead) as Alignment
                    ?? throw new InvalidOperationException("Handle 不是 Alignment。");
                double station = 0, offset = 0;
                alignment.StationOffset(x, y, ref station, ref offset);
                return new { handle, alignment = alignment.Name, x, y, station, offset };
            });
        }
    }
}
