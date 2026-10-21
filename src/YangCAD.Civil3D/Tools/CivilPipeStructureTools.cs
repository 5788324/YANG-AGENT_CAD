using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.Core.Tools;
using CivilNetwork = Autodesk.Civil.DatabaseServices.Network;

namespace YangCAD.Civil3DHost.Tools
{
    internal static class CivilPipeStructureHelpers
    {
        public static CivilDocument ActiveCivilDocument()
            => CivilApplication.ActiveDocument ?? throw new InvalidOperationException("Civil 3D 当前没有活动 CivilDocument。");

        public static Structure StructureFromHandle(CadToolContext context, Transaction transaction, string handle, OpenMode mode)
        {
            var id = EntityHelpers.FromHandle(context.Database, handle);
            return transaction.GetObject(id, mode) as Structure
                ?? throw new InvalidOperationException("Handle 不是 Civil 3D gravity Structure。");
        }

        public static Pipe PipeFromHandle(CadToolContext context, Transaction transaction, string handle, OpenMode mode)
        {
            var id = EntityHelpers.FromHandle(context.Database, handle);
            return transaction.GetObject(id, mode) as Pipe
                ?? throw new InvalidOperationException("Handle 不是 Civil 3D gravity Pipe。");
        }

        public static object StructureSnapshot(Structure structure)
        {
            var connected = new List<string>();
            for (var i = 0; i < structure.ConnectedPipesCount; i++)
            {
                try
                {
                    var id = structure.get_ConnectedPipe(i);
                    if (!id.IsNull) connected.Add(id.Handle.ToString());
                }
                catch { }
            }

            return new
            {
                handle = structure.ObjectId.Handle.ToString(),
                name = structure.Name,
                network = structure.NetworkName,
                description = structure.Description,
                location = EntityHelpers.Pt(structure.Location),
                rim_elevation = structure.RimElevation,
                sump_elevation = structure.SumpElevation,
                sump_depth = structure.SumpDepth,
                control_sump_by = structure.ControlSumpBy.ToString(),
                automatic_rim_surface_adjustment = structure.AutomaticRimSurfaceAdjustment,
                surface_adjustment_value = structure.SurfaceAdjustmentValue,
                surface = structure.RefSurfaceName,
                style = structure.StyleName,
                connected_pipe_count = structure.ConnectedPipesCount,
                connected_pipe_handles = connected.ToArray(),
                is_reference = structure.IsReferenceObject
            };
        }

        public static object PipeSnapshot(Pipe pipe)
        {
            var start = pipe.StartPoint;
            var end = pipe.EndPoint;
            var innerHeight = pipe.InnerHeight;
            double length2d;
            try { length2d = pipe.Length2DCenterToCenter; }
            catch
            {
                // Fallback only for unusual/legacy part states. For curved gravity pipes the
                // Civil API length is preferred; straight XY chord is not treated as authoritative.
                length2d = Math.Sqrt(
                    (end.X - start.X) * (end.X - start.X) +
                    (end.Y - start.Y) * (end.Y - start.Y));
            }
            var startInvert = start.Z - innerHeight * 0.5;
            var endInvert = end.Z - innerHeight * 0.5;
            double? signedSlope = length2d > 1e-9
                ? (double?)((endInvert - startInvert) / length2d)
                : null;

            return new
            {
                handle = pipe.ObjectId.Handle.ToString(),
                name = pipe.Name,
                network = pipe.NetworkName,
                description = pipe.Description,
                start_point = EntityHelpers.Pt(start),
                end_point = EntityHelpers.Pt(end),
                start_center_elevation = start.Z,
                end_center_elevation = end.Z,
                start_invert_elevation = startInvert,
                end_invert_elevation = endInvert,
                inner_height = innerHeight,
                inner_diameter_or_width = pipe.InnerDiameterOrWidth,
                outer_height = pipe.OuterHeight,
                outer_diameter_or_width = pipe.OuterDiameterOrWidth,
                length_2d_center_to_center = length2d,
                slope_absolute = pipe.Slope,
                slope_signed_start_to_end = signedSlope,
                start_structure_handle = HandleOrNull(pipe.StartStructureId),
                end_structure_handle = HandleOrNull(pipe.EndStructureId),
                style = pipe.StyleName,
                is_reference = pipe.IsReferenceObject
            };
        }

        private static string? HandleOrNull(ObjectId id) => id.IsNull ? null : id.Handle.ToString();

        public static IEnumerable<ObjectId> AllStructureIds(CivilDocument civil, Transaction transaction, string? networkName)
        {
            foreach (ObjectId networkId in civil.GetPipeNetworkIds())
            {
                if (!(transaction.GetObject(networkId, OpenMode.ForRead) is CivilNetwork network)) continue;
                if (!string.IsNullOrWhiteSpace(networkName) &&
                    !string.Equals(network.Name, networkName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (ObjectId id in network.GetStructureIds()) yield return id;
            }
        }

        public static IEnumerable<ObjectId> AllPipeIds(CivilDocument civil, Transaction transaction, string? networkName)
        {
            foreach (ObjectId networkId in civil.GetPipeNetworkIds())
            {
                if (!(transaction.GetObject(networkId, OpenMode.ForRead) is CivilNetwork network)) continue;
                if (!string.IsNullOrWhiteSpace(networkName) &&
                    !string.Equals(network.Name, networkName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (ObjectId id in network.GetPipeIds()) yield return id;
            }
        }

        public static void RejectReference(Autodesk.Civil.DatabaseServices.Entity entity)
        {
            if (entity.IsReferenceObject)
                throw new InvalidOperationException("Data Shortcut/Vault 引用对象为只读；YangCAD 不允许直接修改 reference object。");
        }
    }

    public sealed class CivilStructureListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.structure.list",
            Description = "列出 gravity pipe network Structure/检查井及井顶、井底、自动贴合状态。",
            Owner = "Civil3D"
        };

        public object Execute(CadToolContext context)
        {
            var network = ToolJson.OptionalNonBlankString(context.Args, "network");
            var limit = Math.Max(1, Math.Min(5000, ToolJson.Int(context.Args, "limit") ?? 1000));
            var civil = CivilPipeStructureHelpers.ActiveCivilDocument();
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var rows = new List<object>();
                foreach (var id in CivilPipeStructureHelpers.AllStructureIds(civil, tr, network))
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is Structure structure)
                        rows.Add(CivilPipeStructureHelpers.StructureSnapshot(structure));
                }
                return new { count = rows.Count, structures = rows };
            });
        }
    }

    public sealed class CivilStructureGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.structure.get",
            Description = "按 Handle 读取一个 gravity Structure/检查井完整关键状态。",
            Owner = "Civil3D"
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            return CadTransactionRunner.Read(context.Document, tr =>
                CivilPipeStructureHelpers.StructureSnapshot(
                    CivilPipeStructureHelpers.StructureFromHandle(context, tr, handle, OpenMode.ForRead)));
        }
    }

    public sealed class CivilStructureUpdateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.structure.update",
            Description = "修改 gravity Structure 井顶/井底/地面自动贴合；默认 dry_run。",
            Owner = "Civil3D",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var rim = ToolJson.Double(context.Args, "rim_elevation");
            var sump = ToolJson.Double(context.Args, "sump_elevation");
            var automatic = ToolJson.Bool(context.Args, "automatic_rim_surface_adjustment");
            var adjustment = ToolJson.Double(context.Args, "surface_adjustment_value");
            var description = ToolJson.String(context.Args, "description");
            var descriptionProvided = ToolJson.Has(context.Args, "description") && context.Args["description"] != null;
            var applyRules = ToolJson.Bool(context.Args, "apply_rules") ?? false;

            if (!rim.HasValue && !sump.HasValue && !automatic.HasValue && !adjustment.HasValue &&
                !descriptionProvided && !applyRules)
                throw new ArgumentException("没有可修改字段。");

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var structure = CivilPipeStructureHelpers.StructureFromHandle(context, tr, handle, OpenMode.ForWrite);
                CivilPipeStructureHelpers.RejectReference(structure);
                var before = CivilPipeStructureHelpers.StructureSnapshot(structure);

                var targetAutomatic = automatic ?? structure.AutomaticRimSurfaceAdjustment;
                if (rim.HasValue && targetAutomatic)
                    throw new InvalidOperationException(
                        "设置 rim_elevation 时，最终 automatic_rim_surface_adjustment 必须为 false；" +
                        "请在同一次调用中显式传 automatic_rim_surface_adjustment=false，避免 Civil 3D 又按 Surface 覆盖井顶。");

                if (automatic.HasValue) structure.AutomaticRimSurfaceAdjustment = automatic.Value;
                if (adjustment.HasValue) structure.SurfaceAdjustmentValue = adjustment.Value;
                if (rim.HasValue)
                {
                    structure.Position = new Point3d(structure.Position.X, structure.Position.Y, rim.Value);
                }
                if (sump.HasValue)
                {
                    structure.ControlSumpBy = StructureControlSumpType.ByElevation;
                    structure.SumpElevation = sump.Value;
                }
                if (descriptionProvided) structure.Description = description ?? "";

                bool? rulesApplied = null;
                if (applyRules) rulesApplied = structure.ApplyRules();
                var after = CivilPipeStructureHelpers.StructureSnapshot(structure);
                return new
                {
                    handle,
                    dry_run = context.DryRun,
                    before,
                    after,
                    apply_rules = applyRules,
                    rules_applied = rulesApplied
                };
            });
        }
    }

    public sealed class CivilPipeListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.pipe.list",
            Description = "列出 gravity pipe network Pipe，含端点中心/管内底高程和坡度。",
            Owner = "Civil3D"
        };

        public object Execute(CadToolContext context)
        {
            var network = ToolJson.OptionalNonBlankString(context.Args, "network");
            var limit = Math.Max(1, Math.Min(5000, ToolJson.Int(context.Args, "limit") ?? 1000));
            var civil = CivilPipeStructureHelpers.ActiveCivilDocument();
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var rows = new List<object>();
                foreach (var id in CivilPipeStructureHelpers.AllPipeIds(civil, tr, network))
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is Pipe pipe)
                        rows.Add(CivilPipeStructureHelpers.PipeSnapshot(pipe));
                }
                return new { count = rows.Count, pipes = rows };
            });
        }
    }

    public sealed class CivilPipeGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.pipe.get",
            Description = "按 Handle 读取一个 gravity Pipe 的端点、管内底、尺寸、坡度及连接 Structure。",
            Owner = "Civil3D"
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            return CadTransactionRunner.Read(context.Document, tr =>
                CivilPipeStructureHelpers.PipeSnapshot(
                    CivilPipeStructureHelpers.PipeFromHandle(context, tr, handle, OpenMode.ForRead)));
        }
    }

    public sealed class CivilPipeUpdateTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "civil.pipe.update",
            Description = "修改 gravity Pipe 起终点中心/管内底高程；保持 XY 不变，默认 dry_run。",
            Owner = "Civil3D",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var startCenter = ToolJson.Double(context.Args, "start_center_elevation");
            var endCenter = ToolJson.Double(context.Args, "end_center_elevation");
            var startInvert = ToolJson.Double(context.Args, "start_invert_elevation");
            var endInvert = ToolJson.Double(context.Args, "end_invert_elevation");
            var description = ToolJson.String(context.Args, "description");
            var descriptionProvided = ToolJson.Has(context.Args, "description") && context.Args["description"] != null;
            var applyRules = ToolJson.Bool(context.Args, "apply_rules") ?? false;

            if (startCenter.HasValue && startInvert.HasValue)
                throw new ArgumentException("start_center_elevation 与 start_invert_elevation 不能同时指定。");
            if (endCenter.HasValue && endInvert.HasValue)
                throw new ArgumentException("end_center_elevation 与 end_invert_elevation 不能同时指定。");
            if (!startCenter.HasValue && !endCenter.HasValue && !startInvert.HasValue && !endInvert.HasValue &&
                !descriptionProvided && !applyRules)
                throw new ArgumentException("没有可修改字段。");

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var pipe = CivilPipeStructureHelpers.PipeFromHandle(context, tr, handle, OpenMode.ForWrite);
                CivilPipeStructureHelpers.RejectReference(pipe);
                var before = CivilPipeStructureHelpers.PipeSnapshot(pipe);
                var innerHeight = pipe.InnerHeight;
                if ((startInvert.HasValue || endInvert.HasValue) && innerHeight <= 0)
                    throw new InvalidOperationException("Pipe InnerHeight 无效，无法可靠换算管内底高程。");

                var start = pipe.StartPoint;
                var end = pipe.EndPoint;
                if (startInvert.HasValue) start = new Point3d(start.X, start.Y, startInvert.Value + innerHeight * 0.5);
                else if (startCenter.HasValue) start = new Point3d(start.X, start.Y, startCenter.Value);
                if (endInvert.HasValue) end = new Point3d(end.X, end.Y, endInvert.Value + innerHeight * 0.5);
                else if (endCenter.HasValue) end = new Point3d(end.X, end.Y, endCenter.Value);

                if (startCenter.HasValue || startInvert.HasValue) pipe.StartPoint = start;
                if (endCenter.HasValue || endInvert.HasValue) pipe.EndPoint = end;
                if (descriptionProvided) pipe.Description = description ?? "";

                bool? rulesApplied = null;
                if (applyRules) rulesApplied = pipe.ApplyRules();
                var after = CivilPipeStructureHelpers.PipeSnapshot(pipe);
                return new
                {
                    handle,
                    dry_run = context.DryRun,
                    before,
                    after,
                    apply_rules = applyRules,
                    rules_applied = rulesApplied
                };
            });
        }
    }
}
