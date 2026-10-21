using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.Core.Tools;
using CivilFeatureLine = Autodesk.Civil.DatabaseServices.FeatureLine;

namespace YangCAD.Civil3DHost.Tools
{
    internal static class CivilProductionReadAssistHelpers
    {
        private sealed class RegionRange
        {
            public string Name { get; set; } = "";
            public double Start { get; set; }
            public double End { get; set; }
        }

        public static CivilDocument CivilDocument()
            => CivilApplication.ActiveDocument ?? throw new InvalidOperationException("Civil 3D 当前没有活动 CivilDocument。");

        public static int Limit(IReadOnlyDictionary<string, object?> args, string key, int defaultValue, int maxValue)
        {
            var value = ToolJson.Int(args, key) ?? defaultValue;
            if (value < 1 || value > maxValue) throw new ArgumentException($"{key} 必须在 1..{maxValue} 范围内。");
            return value;
        }

        public static double PositiveTolerance(IReadOnlyDictionary<string, object?> args, string key, double defaultValue)
        {
            var value = ToolJson.Double(args, key) ?? defaultValue;
            if (value <= 0.0) throw new ArgumentException($"{key} 必须大于 0。");
            return value;
        }

        public static CivilFeatureLine FeatureLineFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as CivilFeatureLine
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D FeatureLine。");

        public static Corridor CorridorFromHandle(CadToolContext context, Transaction tr, string handle)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), OpenMode.ForRead) as Corridor
               ?? throw new InvalidOperationException("Handle 不是 Civil 3D Corridor。");

        public static List<ObjectId> FeatureLineIds(CivilDocument civil, Transaction tr)
        {
            var ids = new List<ObjectId>();
            var seen = new HashSet<ObjectId>();

            foreach (ObjectId id in civil.GetSitelessFeatureLineIds())
            {
                if (!id.IsNull && seen.Add(id)) ids.Add(id);
            }

            foreach (ObjectId siteId in civil.GetSiteIds())
            {
                var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
                if (site == null) continue;
                foreach (ObjectId id in site.GetFeatureLineIds())
                {
                    if (!id.IsNull && seen.Add(id)) ids.Add(id);
                }
            }
            return ids;
        }

        private static string? ObjectName(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return null;
            try
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (obj is Autodesk.Civil.DatabaseServices.Entity civilEntity) return civilEntity.Name;
                if (obj is SymbolTableRecord symbol) return symbol.Name;
            }
            catch { }
            return null;
        }

        private static double? SafeDouble(Func<double> reader)
        {
            try
            {
                var value = reader();
                return double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
            }
            catch { return null; }
        }

        // Civil 3D API compatibility: some installed 2022 builds do not expose
        // Entity.IsReferencedSourceExisting at compile time. Treat that capability
        // as optional instead of substituting IsReferenceValid with different semantics.
        private static bool? ReferenceSourceExists(Autodesk.Civil.DatabaseServices.Entity entity)
        {
            if (!entity.IsReferenceObject) return true;
            try
            {
                var property = entity.GetType().GetProperty("IsReferencedSourceExisting");
                if (property == null || !property.CanRead || property.PropertyType != typeof(bool)) return null;
                var value = property.GetValue(entity, null);
                return value is bool flag ? flag : (bool?)null;
            }
            catch { return null; }
        }

        public static object FeatureLineSnapshot(CivilFeatureLine featureLine, Transaction tr)
        {
            ObjectId siteId = ObjectId.Null;
            ObjectId surfaceId = ObjectId.Null;
            try { siteId = featureLine.SiteId; } catch { }
            try { surfaceId = featureLine.RelativeSurfaceId; } catch { }

            return new
            {
                handle = featureLine.ObjectId.Handle.ToString(),
                name = featureLine.Name,
                type = featureLine.GetType().Name,
                style = featureLine.StyleName,
                site_handle = siteId.IsNull ? null : siteId.Handle.ToString(),
                site_name = ObjectName(tr, siteId),
                relative_surface_handle = surfaceId.IsNull ? null : surfaceId.Handle.ToString(),
                relative_surface_name = ObjectName(tr, surfaceId),
                point_count = featureLine.PointsCount,
                pi_point_count = featureLine.PIPointsCount,
                elevation_point_count = featureLine.ElevationPointsCount,
                length_2d = SafeDouble(() => featureLine.Length2D),
                length_3d = SafeDouble(() => featureLine.Length3D),
                elevation_min = SafeDouble(() => featureLine.MinElevation),
                elevation_max = SafeDouble(() => featureLine.MaxElevation),
                grade_min = SafeDouble(() => featureLine.MinGrade),
                grade_max = SafeDouble(() => featureLine.MaxGrade),
                is_reference = featureLine.IsReferenceObject,
                is_reference_stale = featureLine.IsReferenceObject ? featureLine.IsReferenceStale : false,
                reference_source_exists = ReferenceSourceExists(featureLine)
            };
        }

        public static FeatureLinePointType PointType(string? value)
        {
            var normalized = (value ?? "all").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "all": return FeatureLinePointType.AllPoints;
                case "pi": return FeatureLinePointType.PIPoint;
                case "elevation": return FeatureLinePointType.ElevationPoint;
                default: throw new ArgumentException("point_type 必须为 all / pi / elevation。");
            }
        }

        public static string PointTypeName(FeatureLinePointType type)
        {
            switch (type)
            {
                case FeatureLinePointType.PIPoint: return "pi";
                case FeatureLinePointType.ElevationPoint: return "elevation";
                default: return "all";
            }
        }

        public static object FeatureLinePoints(CivilFeatureLine featureLine, FeatureLinePointType type, int limit)
        {
            var points = featureLine.GetPoints(type);
            var rows = new List<object>();
            double distance2d = 0.0;
            double distance3d = 0.0;
            Point3d? previous = null;

            for (var i = 0; i < points.Count && rows.Count < limit; i++)
            {
                var point = points[i];
                if (previous.HasValue)
                {
                    var dx = point.X - previous.Value.X;
                    var dy = point.Y - previous.Value.Y;
                    var dz = point.Z - previous.Value.Z;
                    distance2d += Math.Sqrt(dx * dx + dy * dy);
                    distance3d += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                rows.Add(new
                {
                    index = i,
                    x = point.X,
                    y = point.Y,
                    z = point.Z,
                    cumulative_chord_2d = distance2d,
                    cumulative_chord_3d = distance3d
                });
                previous = point;
            }

            return new
            {
                handle = featureLine.ObjectId.Handle.ToString(),
                name = featureLine.Name,
                point_type = PointTypeName(type),
                total = points.Count,
                count = rows.Count,
                truncated = points.Count > rows.Count,
                limit,
                distance_semantics = "cumulative straight-line chord across the returned point sequence; not Civil station or exact path length",
                points = rows.ToArray()
            };
        }

        public static object CorridorSummary(Corridor corridor)
        {
            var regionCount = 0;
            foreach (Baseline baseline in corridor.Baselines)
                regionCount += baseline.BaselineRegions.Count;

            var surfaceCount = 0;
            try { surfaceCount = corridor.CorridorSurfaces.Count; } catch { }

            return new
            {
                handle = corridor.ObjectId.Handle.ToString(),
                name = corridor.Name,
                description = corridor.Description,
                code_set_style = corridor.CodeSetStyleName,
                baseline_count = corridor.Baselines.Count,
                region_count = regionCount,
                corridor_surface_count = surfaceCount,
                is_out_of_date = corridor.IsOutOfDate,
                rebuild_automatic = corridor.RebuildAutomatic,
                region_lock_mode = corridor.RegionLockMode.ToString(),
                maximum_triangle_side_length = corridor.MaximumTriangleSideLength,
                is_reference = corridor.IsReferenceObject,
                is_reference_stale = corridor.IsReferenceObject ? corridor.IsReferenceStale : false,
                reference_source_exists = ReferenceSourceExists(corridor)
            };
        }

        public static object BaselineSnapshot(Baseline baseline, Transaction tr, int regionLimit)
        {
            var regions = new List<object>();
            var totalRegions = baseline.BaselineRegions.Count;
            var emitted = 0;
            foreach (BaselineRegion region in baseline.BaselineRegions)
            {
                if (emitted >= regionLimit) break;
                string? assemblyName = null;
                if (!region.AssemblyId.IsNull)
                {
                    try { assemblyName = (tr.GetObject(region.AssemblyId, OpenMode.ForRead) as Assembly)?.Name; } catch { }
                }
                var appliedAssemblyCount = 0;
                try { foreach (AppliedAssembly ignored in region.AppliedAssemblies) appliedAssemblyCount++; } catch { }

                regions.Add(new
                {
                    name = region.Name,
                    start_station = region.StartStation,
                    end_station = region.EndStation,
                    length = region.EndStation - region.StartStation,
                    assembly_handle = region.AssemblyId.IsNull ? null : region.AssemblyId.Handle.ToString(),
                    assembly_name = assemblyName,
                    needs_processing = region.NeedsProcessing,
                    applied_assembly_count = appliedAssemblyCount
                });
                emitted++;
            }

            var featureLineBased = false;
            try { featureLineBased = baseline.IsFeatureLineBased(); } catch { }
            var featureLineId = ObjectId.Null;
            var alignmentId = ObjectId.Null;
            var profileId = ObjectId.Null;
            try { featureLineId = baseline.FeatureLineId; } catch { }
            try { alignmentId = baseline.AlignmentId; } catch { }
            try { profileId = baseline.ProfileId; } catch { }

            return new
            {
                name = baseline.Name,
                start_station = baseline.StartStation,
                end_station = baseline.EndStation,
                length = baseline.EndStation - baseline.StartStation,
                feature_line_based = featureLineBased,
                feature_line_handle = featureLineId.IsNull ? null : featureLineId.Handle.ToString(),
                feature_line_name = ObjectName(tr, featureLineId),
                alignment_handle = alignmentId.IsNull ? null : alignmentId.Handle.ToString(),
                alignment_name = ObjectName(tr, alignmentId),
                profile_handle = profileId.IsNull ? null : profileId.Handle.ToString(),
                profile_name = ObjectName(tr, profileId),
                needs_processing = baseline.NeedsProcessing,
                region_total = totalRegions,
                region_count = regions.Count,
                regions_truncated = totalRegions > regions.Count,
                regions = regions.ToArray()
            };
        }

        public static string[] CorridorCodes(Corridor corridor, string kind, int limit, out int total, out bool truncated)
        {
            IEnumerable<string> source;
            switch (kind)
            {
                case "point": source = corridor.GetPointCodes(); break;
                case "link": source = corridor.GetLinkCodes(); break;
                case "shape": source = corridor.GetShapeCodes(); break;
                default: throw new ArgumentException("未知 corridor code kind。");
            }
            var all = new List<string>();
            foreach (var code in source) all.Add(code);
            total = all.Count;
            truncated = total > limit;
            if (!truncated) return all.ToArray();
            return all.GetRange(0, limit).ToArray();
        }

        public static object CorridorDiagnostics(Corridor corridor, Transaction tr, double stationTolerance, int issueLimit)
        {
            var issues = new List<object>();
            var issueTotal = 0;
            var baselineCount = 0;
            var regionCount = 0;
            var disabledBaselineCount = 0;
            var disabledRegionCount = 0;
            var missingAssemblyCount = 0;
            var gapCount = 0;
            var overlapCount = 0;
            var invalidRangeCount = 0;

            Action<string, string, string, object?> addIssue = (severity, code, message, detail) =>
            {
                issueTotal++;
                if (issues.Count >= issueLimit) return;
                issues.Add(new { severity, code, message, detail });
            };

            if (corridor.IsOutOfDate)
                addIssue("warning", "corridor_out_of_date", "Corridor 当前为 out-of-date，读取结果可能不是最新 rebuild 状态。", null);
            if (corridor.IsReferenceObject && corridor.IsReferenceStale)
                addIssue("warning", "reference_stale", "Corridor 数据引用已 stale。", null);
            var referenceSourceExists = ReferenceSourceExists(corridor);
            if (corridor.IsReferenceObject && referenceSourceExists == false)
                addIssue("error", "reference_source_missing", "Corridor 引用源不存在或不可用。", null);
            else if (corridor.IsReferenceObject && !referenceSourceExists.HasValue)
                addIssue("info", "reference_source_existence_unavailable", "当前 Civil 3D API 未公开引用源存在性；该项返回 unknown，不推断为源缺失。", null);

            foreach (Baseline baseline in corridor.Baselines)
            {
                baselineCount++;
                if (!baseline.NeedsProcessing)
                {
                    disabledBaselineCount++;
                    addIssue("info", "baseline_processing_disabled", "Baseline 被设置为 rebuild 时不处理。", new { baseline = baseline.Name });
                }

                var ranges = new List<RegionRange>();
                foreach (BaselineRegion region in baseline.BaselineRegions)
                {
                    regionCount++;
                    ranges.Add(new RegionRange { Name = region.Name, Start = region.StartStation, End = region.EndStation });
                    if (!region.NeedsProcessing)
                    {
                        disabledRegionCount++;
                        addIssue("info", "region_processing_disabled", "Region 被设置为 rebuild 时不处理。", new { baseline = baseline.Name, region = region.Name });
                    }
                    if (region.AssemblyId.IsNull)
                    {
                        missingAssemblyCount++;
                        addIssue("warning", "region_missing_assembly", "Region 没有关联 Assembly。", new { baseline = baseline.Name, region = region.Name });
                    }
                    else
                    {
                        try
                        {
                            if (!(tr.GetObject(region.AssemblyId, OpenMode.ForRead) is Assembly))
                            {
                                missingAssemblyCount++;
                                addIssue("warning", "region_invalid_assembly", "Region 的 AssemblyId 无法解析为 Assembly。", new { baseline = baseline.Name, region = region.Name });
                            }
                        }
                        catch
                        {
                            missingAssemblyCount++;
                            addIssue("warning", "region_invalid_assembly", "Region 的 AssemblyId 无法打开。", new { baseline = baseline.Name, region = region.Name });
                        }
                    }
                    if (region.EndStation <= region.StartStation)
                    {
                        invalidRangeCount++;
                        addIssue("error", "region_invalid_range", "Region 终桩号不大于起桩号。", new { baseline = baseline.Name, region = region.Name, start_station = region.StartStation, end_station = region.EndStation });
                    }
                    else if (region.EndStation - region.StartStation <= stationTolerance)
                    {
                        addIssue("warning", "region_near_zero_range", "Region 长度不大于 station_tolerance；请结合项目精度复核。", new { baseline = baseline.Name, region = region.Name, start_station = region.StartStation, end_station = region.EndStation, station_tolerance = stationTolerance });
                    }
                }

                ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
                for (var i = 1; i < ranges.Count; i++)
                {
                    var previous = ranges[i - 1];
                    var current = ranges[i];
                    var delta = current.Start - previous.End;
                    if (delta > stationTolerance)
                    {
                        gapCount++;
                        addIssue("warning", "region_gap", "同一 Baseline 的相邻 Region 存在桩号空档。", new { baseline = baseline.Name, previous_region = previous.Name, next_region = current.Name, gap = delta });
                    }
                    else if (delta < -stationTolerance)
                    {
                        overlapCount++;
                        addIssue("error", "region_overlap", "同一 Baseline 的相邻 Region 存在桩号重叠。", new { baseline = baseline.Name, previous_region = previous.Name, next_region = current.Name, overlap = -delta });
                    }
                }
            }

            return new
            {
                handle = corridor.ObjectId.Handle.ToString(),
                name = corridor.Name,
                is_out_of_date = corridor.IsOutOfDate,
                station_tolerance = stationTolerance,
                baseline_count = baselineCount,
                region_count = regionCount,
                processing_disabled_baseline_count = disabledBaselineCount,
                processing_disabled_region_count = disabledRegionCount,
                missing_or_invalid_assembly_count = missingAssemblyCount,
                region_gap_count = gapCount,
                region_overlap_count = overlapCount,
                invalid_region_range_count = invalidRangeCount,
                issue_total = issueTotal,
                issue_count = issues.Count,
                issue_limit = issueLimit,
                issues_truncated = issueTotal > issues.Count,
                issues = issues.ToArray()
            };
        }
    }

    public sealed class CivilFeatureLineListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.feature_line.list", Description = "列出 Civil 3D FeatureLine，包含 Site/长度/高程/坡度摘要。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var limit = CivilProductionReadAssistHelpers.Limit(c.Args, "limit", 1000, 5000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var civil = CivilProductionReadAssistHelpers.CivilDocument();
                var ids = CivilProductionReadAssistHelpers.FeatureLineIds(civil, tr);
                var rows = new List<object>();
                foreach (var id in ids)
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is CivilFeatureLine featureLine)
                        rows.Add(CivilProductionReadAssistHelpers.FeatureLineSnapshot(featureLine, tr));
                }
                return new { total = ids.Count, count = rows.Count, truncated = ids.Count > rows.Count, limit, feature_lines = rows.ToArray() };
            });
        }
    }

    public sealed class CivilFeatureLineGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.feature_line.get", Description = "按 Handle 深读一个 Civil 3D FeatureLine。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            return CadTransactionRunner.Read(c.Document, tr => CivilProductionReadAssistHelpers.FeatureLineSnapshot(CivilProductionReadAssistHelpers.FeatureLineFromHandle(c, tr, handle), tr));
        }
    }

    public sealed class CivilFeatureLinePointsTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.feature_line.points", Description = "读取 FeatureLine 的 PI/Elevation/全部点，带响应上限。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var type = CivilProductionReadAssistHelpers.PointType(ToolJson.String(c.Args, "point_type"));
            var limit = CivilProductionReadAssistHelpers.Limit(c.Args, "limit", 5000, 50000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var featureLine = CivilProductionReadAssistHelpers.FeatureLineFromHandle(c, tr, handle);
                return CivilProductionReadAssistHelpers.FeatureLinePoints(featureLine, type, limit);
            });
        }
    }

    public sealed class CivilFeatureLineDiagnoseTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.feature_line.diagnose", Description = "只读诊断 FeatureLine 的零长/近垂直段、点数与可选坡度阈值。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var tolerance = CivilProductionReadAssistHelpers.PositiveTolerance(c.Args, "tolerance", 1e-6);
            var issueLimit = CivilProductionReadAssistHelpers.Limit(c.Args, "issue_limit", 200, 5000);
            var maxAbsGrade = ToolJson.Double(c.Args, "max_abs_grade");
            if (maxAbsGrade.HasValue && maxAbsGrade.Value < 0.0) throw new ArgumentException("max_abs_grade 必须大于等于 0。");

            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var featureLine = CivilProductionReadAssistHelpers.FeatureLineFromHandle(c, tr, handle);
                var points = featureLine.GetPoints(FeatureLinePointType.AllPoints);
                var issues = new List<object>();
                var issueTotal = 0;
                Action<object> addIssue = issue =>
                {
                    issueTotal++;
                    if (issues.Count < issueLimit) issues.Add(issue);
                };
                var zero2d = 0;
                var zero3d = 0;
                var vertical = 0;
                var nonFinite = 0;
                for (var i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    if (double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.Y) || double.IsNaN(p.Z) || double.IsInfinity(p.Z))
                    {
                        nonFinite++;
                        addIssue(new { severity = "error", code = "non_finite_point", index = i, point = EntityHelpers.Pt(p) });
                    }
                    if (i == 0) continue;
                    var q = points[i - 1];
                    var dx = p.X - q.X;
                    var dy = p.Y - q.Y;
                    var dz = p.Z - q.Z;
                    var d2 = Math.Sqrt(dx * dx + dy * dy);
                    var d3 = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d2 <= tolerance)
                    {
                        zero2d++;
                        if (d3 <= tolerance)
                        {
                            zero3d++;
                            addIssue(new { severity = "warning", code = "near_zero_segment", start_index = i - 1, end_index = i, distance_2d = d2, distance_3d = d3 });
                        }
                        else
                        {
                            vertical++;
                            addIssue(new { severity = "info", code = "near_vertical_segment", start_index = i - 1, end_index = i, distance_2d = d2, delta_z = dz });
                        }
                    }
                }

                var gradeMin = (double?)null;
                var gradeMax = (double?)null;
                try { gradeMin = featureLine.MinGrade; } catch { }
                try { gradeMax = featureLine.MaxGrade; } catch { }
                if (maxAbsGrade.HasValue && ((gradeMin.HasValue && Math.Abs(gradeMin.Value) > maxAbsGrade.Value) || (gradeMax.HasValue && Math.Abs(gradeMax.Value) > maxAbsGrade.Value)))
                {
                    addIssue(new { severity = "warning", code = "grade_threshold_exceeded", max_abs_grade = maxAbsGrade.Value, grade_min = gradeMin, grade_max = gradeMax });
                }
                if (points.Count != featureLine.PointsCount)
                    addIssue(new { severity = "warning", code = "point_count_mismatch", reported_point_count = featureLine.PointsCount, enumerated_point_count = points.Count });

                return new
                {
                    handle = featureLine.ObjectId.Handle.ToString(),
                    name = featureLine.Name,
                    tolerance,
                    max_abs_grade = maxAbsGrade,
                    point_count = points.Count,
                    pi_point_count = featureLine.PIPointsCount,
                    zero_or_near_zero_2d_segment_count = zero2d,
                    zero_or_near_zero_3d_segment_count = zero3d,
                    near_vertical_segment_count = vertical,
                    non_finite_point_count = nonFinite,
                    grade_min = gradeMin,
                    grade_max = gradeMax,
                    issue_total = issueTotal,
                    issue_count = issues.Count,
                    issue_limit = issueLimit,
                    issues_truncated = issueTotal > issues.Count,
                    issues = issues.ToArray(),
                    read_only = true
                };
            });
        }
    }

    public sealed class CivilCorridorListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.corridor.list", Description = "列出 Corridor，含 baseline/region/out-of-date 摘要。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var limit = CivilProductionReadAssistHelpers.Limit(c.Args, "limit", 200, 1000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var civil = CivilProductionReadAssistHelpers.CivilDocument();
                var rows = new List<object>();
                foreach (ObjectId id in civil.CorridorCollection)
                {
                    if (rows.Count >= limit) break;
                    if (tr.GetObject(id, OpenMode.ForRead) is Corridor corridor)
                        rows.Add(CivilProductionReadAssistHelpers.CorridorSummary(corridor));
                }
                return new { total = civil.CorridorCollection.Count, count = rows.Count, truncated = civil.CorridorCollection.Count > rows.Count, limit, corridors = rows.ToArray() };
            });
        }
    }

    public sealed class CivilCorridorGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.corridor.get", Description = "按 Handle 深读 Corridor，并可返回 point/link/shape code。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var includeCodes = ToolJson.Bool(c.Args, "include_codes") ?? true;
            var codeLimit = CivilProductionReadAssistHelpers.Limit(c.Args, "code_limit", 500, 5000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var corridor = CivilProductionReadAssistHelpers.CorridorFromHandle(c, tr, handle);
                var summary = CivilProductionReadAssistHelpers.CorridorSummary(corridor);
                string[] pointCodes = Array.Empty<string>(), linkCodes = Array.Empty<string>(), shapeCodes = Array.Empty<string>();
                var pointTotal = 0; var linkTotal = 0; var shapeTotal = 0;
                var pointTruncated = false; var linkTruncated = false; var shapeTruncated = false;
                if (includeCodes)
                {
                    pointCodes = CivilProductionReadAssistHelpers.CorridorCodes(corridor, "point", codeLimit, out pointTotal, out pointTruncated);
                    linkCodes = CivilProductionReadAssistHelpers.CorridorCodes(corridor, "link", codeLimit, out linkTotal, out linkTruncated);
                    shapeCodes = CivilProductionReadAssistHelpers.CorridorCodes(corridor, "shape", codeLimit, out shapeTotal, out shapeTruncated);
                }
                return new
                {
                    summary,
                    codes_included = includeCodes,
                    code_limit = codeLimit,
                    point_code_total = pointTotal,
                    point_codes_truncated = pointTruncated,
                    point_codes = pointCodes,
                    link_code_total = linkTotal,
                    link_codes_truncated = linkTruncated,
                    link_codes = linkCodes,
                    shape_code_total = shapeTotal,
                    shape_codes_truncated = shapeTruncated,
                    shape_codes = shapeCodes
                };
            });
        }
    }

    public sealed class CivilCorridorBaselinesTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.corridor.baselines", Description = "读取 Corridor Baseline/Region/Assembly 结构，带双层 limit。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var baselineLimit = CivilProductionReadAssistHelpers.Limit(c.Args, "baseline_limit", 100, 1000);
            var regionLimit = CivilProductionReadAssistHelpers.Limit(c.Args, "region_limit_per_baseline", 1000, 10000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var corridor = CivilProductionReadAssistHelpers.CorridorFromHandle(c, tr, handle);
                var rows = new List<object>();
                foreach (Baseline baseline in corridor.Baselines)
                {
                    if (rows.Count >= baselineLimit) break;
                    rows.Add(CivilProductionReadAssistHelpers.BaselineSnapshot(baseline, tr, regionLimit));
                }
                return new
                {
                    handle = corridor.ObjectId.Handle.ToString(),
                    name = corridor.Name,
                    baseline_total = corridor.Baselines.Count,
                    baseline_count = rows.Count,
                    baselines_truncated = corridor.Baselines.Count > rows.Count,
                    baseline_limit = baselineLimit,
                    region_limit_per_baseline = regionLimit,
                    baselines = rows.ToArray()
                };
            });
        }
    }

    public sealed class CivilCorridorDiagnoseTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "civil.corridor.diagnose", Description = "只读诊断 Corridor out-of-date、引用状态、Region gap/overlap、Assembly 与 processing 状态。", Owner = "Civil3D" };
        public object Execute(CadToolContext c)
        {
            var handle = ToolJson.RequiredString(c.Args, "handle");
            var tolerance = CivilProductionReadAssistHelpers.PositiveTolerance(c.Args, "station_tolerance", 1e-6);
            var issueLimit = CivilProductionReadAssistHelpers.Limit(c.Args, "issue_limit", 500, 5000);
            return CadTransactionRunner.Read(c.Document, tr =>
            {
                var corridor = CivilProductionReadAssistHelpers.CorridorFromHandle(c, tr, handle);
                return CivilProductionReadAssistHelpers.CorridorDiagnostics(corridor, tr, tolerance, issueLimit);
            });
        }
    }
}
