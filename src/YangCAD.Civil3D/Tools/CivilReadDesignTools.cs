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

namespace YangCAD.Civil3DHost.Tools
{
    internal static class CivilReadDesignHelpers
    {
        public static CivilDocument CivilDocument()
            => CivilApplication.ActiveDocument ?? throw new InvalidOperationException("Civil 3D 当前没有活动 CivilDocument。");

        public static Autodesk.Civil.DatabaseServices.Surface SurfaceFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D Surface。");

        public static Alignment AlignmentFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as Alignment
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D Alignment。");

        public static Profile ProfileFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as Profile
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D Profile。");

        public static CogoPoint CogoPointFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as CogoPoint
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D COGO Point。");

        public static PressurePipeNetwork PressureNetworkFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as PressurePipeNetwork
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D Pressure Pipe Network。");

        public static int Limit(IReadOnlyDictionary<string, object?> args, string key, int defaultValue, int maxValue)
        {
            var value = ToolJson.Int(args, key) ?? defaultValue;
            if (value < 1 || value > maxValue) throw new ArgumentException($"{key} 必须在 1..{maxValue} 范围内。");
            return value;
        }

        // RawStation is not available in every installed Civil 3D 2022/2023 API.
        // Prefer it when present; older Station getters return raw station semantics.
        public static double ProfilePviRawStation(ProfilePVI pvi)
        {
            foreach (var propertyName in new[] { "RawStation", "Station" })
            {
                try
                {
                    var property = pvi.GetType().GetProperty(propertyName);
                    if (property == null || !property.CanRead) continue;
                    var value = property.GetValue(pvi, null);
                    if (value is double station) return station;
                }
                catch { }
            }
            throw new InvalidOperationException("当前 Civil 3D API 无法读取 Profile PVI raw station。");
        }

        public static object SurfaceSnapshot(Autodesk.Civil.DatabaseServices.Surface surface)
        {
            int boundaryOperationCount;
            try { boundaryOperationCount = surface.BoundariesDefinition.Count; }
            catch { boundaryOperationCount = -1; }
            return new
            {
                handle = surface.ObjectId.Handle.ToString(),
                name = surface.Name,
                type = surface.GetType().Name,
                description = surface.Description,
                style = surface.StyleName,
                auto_rebuild = surface.AutoRebuild,
                boundary_operation_count = boundaryOperationCount,
                is_reference = surface.IsReferenceObject
            };
        }

        public static object ProfileSnapshot(Profile profile, Transaction tr)
        {
            string? alignmentName = null;
            string? alignmentHandle = null;
            if (!profile.AlignmentId.IsNull)
            {
                alignmentHandle = profile.AlignmentId.Handle.ToString();
                try { alignmentName = (tr.GetObject(profile.AlignmentId, OpenMode.ForRead) as Alignment)?.Name; } catch { }
            }
            return new
            {
                handle = profile.ObjectId.Handle.ToString(),
                name = profile.Name,
                profile_type = profile.ProfileType.ToString(),
                update_mode = profile.UpdateMode.ToString(),
                starting_station = profile.StartingStation,
                ending_station = profile.EndingStation,
                length = profile.Length,
                elevation_min = profile.ElevationMin,
                elevation_max = profile.ElevationMax,
                offset = profile.Offset,
                pvi_count = profile.PVIs.Count,
                alignment_handle = alignmentHandle,
                alignment_name = alignmentName,
                style = profile.StyleName,
                is_reference = profile.IsReferenceObject
            };
        }

        public static object CogoPointSnapshot(CogoPoint point, Transaction tr)
        {
            string? groupName = null;
            if (!point.PrimaryPointGroupId.IsNull)
            {
                try { groupName = (tr.GetObject(point.PrimaryPointGroupId, OpenMode.ForRead) as PointGroup)?.Name; } catch { }
            }
            string? styleName = null;
            if (!point.StyleId.IsNull)
            {
                try { styleName = (tr.GetObject(point.StyleId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PointStyle)?.Name; } catch { }
            }
            return new
            {
                handle = point.ObjectId.Handle.ToString(),
                point_number = point.PointNumber,
                point_name = point.PointName,
                easting = point.Easting,
                northing = point.Northing,
                elevation = point.Elevation,
                location = EntityHelpers.Pt(point.Location),
                raw_description = point.RawDescription,
                full_description = point.FullDescription,
                primary_point_group = groupName,
                style = styleName,
                is_locked = point.IsLocked,
                is_survey_point = point.IsSurveyPoint,
                is_project_point = point.IsProjectPoint
            };
        }

        public static object PressureNetworkSnapshot(PressurePipeNetwork network, bool includeRuns)
        {
            var runs = new List<object>();
            if (includeRuns)
            {
                try
                {
                    foreach (PressurePipeRun run in network.PipeRuns)
                    {
                        runs.Add(new
                        {
                            name = run.Name,
                            alignment_handle = run.AlignmentId.IsNull ? null : run.AlignmentId.Handle.ToString(),
                            profile_handle = run.ProfileId.IsNull ? null : run.ProfileId.Handle.ToString()
                        });
                    }
                }
                catch { }
            }
            int pipes = 0, fittings = 0, appurtenances = 0;
            try { pipes = network.GetPipeIds().Count; } catch { }
            try { fittings = network.GetFittingIds().Count; } catch { }
            try { appurtenances = network.GetAppurtenanceIds().Count; } catch { }
            return new
            {
                handle = network.ObjectId.Handle.ToString(),
                name = network.Name,
                description = network.Description,
                style = network.StyleName,
                is_empty = network.IsEmpty,
                is_reference = network.IsReferenceObject,
                reference_alignment = network.ReferenceAlignmentName,
                reference_surface = network.ReferenceSurfaceName,
                pipe_count = pipes,
                fitting_count = fittings,
                appurtenance_count = appurtenances,
                pipe_runs = runs.ToArray()
            };
        }
    }

    public sealed class CivilSurfaceGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.surface.get", Description = "按 Handle 读取 Surface 基本属性。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            return CadTransactionRunner.Read(c.Document, tr => CivilReadDesignHelpers.SurfaceSnapshot(CivilReadDesignHelpers.SurfaceFromHandle(c, tr, handle)));
        }
    }

    public sealed class CivilSurfaceElevationTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.surface.elevation", Description = "查询 Surface 指定 XY 的高程。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var x = ToolJson.Double(c.Args, "x") ?? throw new ArgumentException("缺少参数：x");
            var y = ToolJson.Double(c.Args, "y") ?? throw new ArgumentException("缺少参数：y");
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var surface = CivilReadDesignHelpers.SurfaceFromHandle(c, tr, handle);
                try { return (object)new { handle, surface = surface.Name, x, y, on_surface = true, elevation = (double?)surface.FindElevationAtXY(x, y) }; }
                catch (Autodesk.Civil.PointNotOnEntityException) { return new { handle, surface = surface.Name, x, y, on_surface = false, elevation = (double?)null }; }
            });
        }
    }

    public sealed class CivilSurfaceBoundariesTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.surface.boundaries", Description = "读取 Surface 边界定义及离散顶点。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var vertexLimit = CivilReadDesignHelpers.Limit(c.Args, "vertex_limit", 5000, 50000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var surface = CivilReadDesignHelpers.SurfaceFromHandle(c, tr, handle);
                var operations = new List<object>();
                var emitted = 0;
                var truncated = false;
                for (int i = 0; i < surface.BoundariesDefinition.Count; i++)
                {
                    var operation = surface.BoundariesDefinition[i];
                    var boundaries = new List<object>();
                    for (int j = 0; j < operation.Count; j++)
                    {
                        var boundary = operation[j];
                        var vertices = new List<object>();
                        foreach (Point3d point in boundary.Vertices)
                        {
                            if (emitted >= vertexLimit) { truncated = true; break; }
                            vertices.Add(EntityHelpers.Pt(point));
                            emitted++;
                        }
                        boundaries.Add(new { boundary_type = boundary.BoundaryType.ToString(), vertex_count = boundary.Vertices.Count, vertices = vertices.ToArray() });
                        if (truncated) break;
                    }
                    operations.Add(new
                    {
                        name = operation.Name,
                        enabled = operation.Enabled,
                        boundary_type = operation.BoundaryType.ToString(),
                        mid_ordinate_distance = operation.MidOrdinateDistance,
                        boundary_count = operation.Count,
                        boundaries = boundaries.ToArray()
                    });
                    if (truncated) break;
                }
                return new { handle, surface = surface.Name, operation_count = surface.BoundariesDefinition.Count, emitted_vertices = emitted, truncated, vertex_limit = vertexLimit, operations = operations.ToArray() };
            });
        }
    }

    public sealed class CivilAlignmentPointLocationTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.alignment.point_location", Description = "Alignment 桩号/偏距 → XY。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var station = ToolJson.Double(c.Args, "station") ?? throw new ArgumentException("缺少参数：station");
            var offset = ToolJson.Double(c.Args, "offset") ?? 0.0;
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var alignment = CivilReadDesignHelpers.AlignmentFromHandle(c, tr, handle);
                if (station < alignment.StartingStation || station > alignment.EndingStation)
                    throw new ArgumentOutOfRangeException(nameof(station), $"桩号 {station} 超出 Alignment 范围 [{alignment.StartingStation}, {alignment.EndingStation}]。");
                double easting = 0, northing = 0;
                alignment.PointLocation(station, offset, ref easting, ref northing);
                return new { handle, alignment = alignment.Name, station, offset, x = easting, y = northing };
            });
        }
    }

    public sealed class CivilProfileListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.profile.list", Description = "列出 Profile，可按 Alignment Handle 过滤。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var civil = CivilReadDesignHelpers.CivilDocument();
            var alignmentHandle = ToolJson.String(c.Args, "alignment_handle");
            var limit = CivilReadDesignHelpers.Limit(c.Args, "limit", 1000, 10000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var rows = new List<object>();
                var alignmentIds = new List<ObjectId>();
                if (!string.IsNullOrWhiteSpace(alignmentHandle))
                    alignmentIds.Add(CivilReadDesignHelpers.AlignmentFromHandle(c, tr, alignmentHandle!).ObjectId);
                else
                    foreach (ObjectId id in civil.GetAlignmentIds()) alignmentIds.Add(id);
                foreach (ObjectId alignmentId in alignmentIds)
                {
                    var alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                    if (alignment == null) continue;
                    foreach (ObjectId profileId in alignment.GetProfileIds())
                    {
                        if (rows.Count >= limit) break;
                        if (tr.GetObject(profileId, OpenMode.ForRead) is Profile profile)
                            rows.Add(CivilReadDesignHelpers.ProfileSnapshot(profile, tr));
                    }
                    if (rows.Count >= limit) break;
                }
                return new { count = rows.Count, profiles = rows.ToArray(), limit };
            });
        }
    }

    public sealed class CivilProfileGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.profile.get", Description = "按 Handle 读取 Profile。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            return CadTransactionRunner.Read(c.Document, tr => CivilReadDesignHelpers.ProfileSnapshot(CivilReadDesignHelpers.ProfileFromHandle(c, tr, handle), tr));
        }
    }

    public sealed class CivilProfileElevationTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.profile.elevation", Description = "查询 Profile 指定桩号的高程与瞬时坡度。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var station = ToolJson.Double(c.Args, "station") ?? throw new ArgumentException("缺少参数：station");
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var profile = CivilReadDesignHelpers.ProfileFromHandle(c, tr, handle);
                if (station < profile.StartingStation || station > profile.EndingStation)
                    throw new ArgumentOutOfRangeException(nameof(station), $"桩号 {station} 超出 Profile 范围 [{profile.StartingStation}, {profile.EndingStation}]。");
                return new { handle, profile = profile.Name, station, elevation = profile.ElevationAt(station), grade = profile.GradeAt(station) };
            });
        }
    }

    public sealed class CivilProfilePviListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.profile.pvi.list", Description = "列出 Profile PVI 的桩号、高程及进出坡度。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var limit = CivilReadDesignHelpers.Limit(c.Args, "limit", 1000, 10000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var profile = CivilReadDesignHelpers.ProfileFromHandle(c, tr, handle);
                var rows = new List<object>();
                foreach (ProfilePVI pvi in profile.PVIs)
                {
                    if (rows.Count >= limit) break;
                    double? gradeIn = null;
                    try { gradeIn = pvi.GradeIn; } catch { }
                    double? gradeOut = null;
                    try { gradeOut = pvi.GradeOut; } catch { }
                    rows.Add(new { station = CivilReadDesignHelpers.ProfilePviRawStation(pvi), elevation = pvi.Elevation, grade_in = gradeIn, grade_out = gradeOut, pvi_type = pvi.PVIType.ToString() });
                }
                return new { handle, profile = profile.Name, count = rows.Count, total = profile.PVIs.Count, pvis = rows.ToArray(), limit };
            });
        }
    }

    public sealed class CivilCogoPointListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.cogo_point.list", Description = "列出 COGO Point。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var civil = CivilReadDesignHelpers.CivilDocument();
            var limit = CivilReadDesignHelpers.Limit(c.Args, "limit", 1000, 10000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var rows = new List<object>();
                foreach (ObjectId id in civil.CogoPoints)
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is CogoPoint point)
                        rows.Add(CivilReadDesignHelpers.CogoPointSnapshot(point, tr));
                }
                return new { count = rows.Count, total = civil.CogoPoints.Count, points = rows.ToArray(), limit };
            });
        }
    }

    public sealed class CivilCogoPointGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.cogo_point.get", Description = "按 Handle 读取 COGO Point。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            return CadTransactionRunner.Read(c.Document, tr => CivilReadDesignHelpers.CogoPointSnapshot(CivilReadDesignHelpers.CogoPointFromHandle(c, tr, handle), tr));
        }
    }

    public sealed class CivilPressureNetworkListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.pressure_network.list", Description = "只读列出 Pressure Pipe Network。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var civil = CivilReadDesignHelpers.CivilDocument();
            var limit = CivilReadDesignHelpers.Limit(c.Args, "limit", 100, 1000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var rows = new List<object>();
                foreach (ObjectId id in CivilDocumentPressurePipesExtension.GetPressurePipeNetworkIds(civil))
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is PressurePipeNetwork network)
                        rows.Add(CivilReadDesignHelpers.PressureNetworkSnapshot(network, false));
                }
                return new { count = rows.Count, networks = rows.ToArray(), limit, read_only = true };
            });
        }
    }

    public sealed class CivilPressureNetworkGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.pressure_network.get", Description = "只读读取 Pressure Pipe Network 与 PipeRun 摘要。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            return CadTransactionRunner.Read(c.Document, tr => CivilReadDesignHelpers.PressureNetworkSnapshot(CivilReadDesignHelpers.PressureNetworkFromHandle(c, tr, handle), true));
        }
    }
}
