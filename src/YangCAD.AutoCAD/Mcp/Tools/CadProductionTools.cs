using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Tools
{
    internal static class CadProductionHelpers
    {
        public static Polyline PolylineFromHandle(CadToolContext context, Transaction tr, string handle, OpenMode mode)
            => tr.GetObject(EntityHelpers.FromHandle(context.Database, handle), mode) as Polyline
               ?? throw new InvalidOperationException("Handle 不是 2D Lightweight Polyline。");

        public static double Tolerance(IReadOnlyDictionary<string, object?> args)
        {
            var tolerance = ToolJson.Double(args, "tolerance") ?? 1e-6;
            if (tolerance <= 0 || tolerance > 1.0)
                throw new ArgumentOutOfRangeException("tolerance", "tolerance 必须 > 0 且 <= 1.0。");
            return tolerance;
        }

        public static bool Near(Point2d a, Point2d b, double tolerance)
            => a.GetDistanceTo(b) <= tolerance;

        public static bool CollinearAndBetween(Point2d a, Point2d b, Point2d c, double tolerance)
        {
            var ab = b - a;
            var ac = c - a;
            var acLength = ac.Length;
            if (acLength <= tolerance) return false;

            // `tolerance` is a drawing-distance tolerance.  Use the perpendicular distance from B
            // to line AC instead of a scale-relative cross-product threshold; otherwise a very long
            // segment can accept a visibly bent middle point as "collinear".
            var cross = Math.Abs(ab.X * ac.Y - ab.Y * ac.X);
            var perpendicularDistance = cross / acLength;
            if (perpendicularDistance > tolerance) return false;

            // Only remove B when it lies between A and C (allowing at most `tolerance` overshoot),
            // never when the path doubles back.
            var dot = (b.X - a.X) * (b.X - c.X) + (b.Y - a.Y) * (b.Y - c.Y);
            return dot <= tolerance * Math.Max(1.0, acLength);
        }

        public static bool HasNonZeroSegmentWidths(Polyline p)
        {
            // HasWidth is the AutoCAD semantic flag for width values.  ConstantWidth is checked
            // separately so every non-zero width representation fails closed.
            if (p.HasWidth) return true;
            try { if (p.ConstantWidth != 0.0) return true; } catch { }
            for (var i = 0; i < p.NumberOfVertices; i++)
            {
                if (p.GetStartWidthAt(i) != 0.0 || p.GetEndWidthAt(i) != 0.0) return true;
            }
            return false;
        }

        public static object InspectPolyline(Polyline p, double tolerance)
        {
            var duplicates = new List<int>();
            var zeroSegments = new List<int>();
            var collinear = new List<int>();
            // Bulge/width are semantic data, not geometric noise: any stored non-zero value must
            // fail closed regardless of the positional cleanup tolerance.
            var hasBulges = p.HasBulges;
            var hasVariableWidths = HasNonZeroSegmentWidths(p);
            var n = p.NumberOfVertices;

            var segmentCount = p.Closed ? n : Math.Max(0, n - 1);
            for (var i = 0; i < segmentCount; i++)
            {
                var j = (i + 1) % n;
                var a = p.GetPoint2dAt(i);
                var b = p.GetPoint2dAt(j);
                if (Near(a, b, tolerance))
                {
                    zeroSegments.Add(i);
                    // Report the vertex that would be removed; for a closing duplicate, remove last.
                    duplicates.Add(j == 0 ? i : j);
                }
            }

            if (!hasBulges && !hasVariableWidths && n >= (p.Closed ? 3 : 3))
            {
                var start = p.Closed ? 0 : 1;
                var end = p.Closed ? n : n - 1;
                for (var i = start; i < end; i++)
                {
                    var prev = (i - 1 + n) % n;
                    var next = (i + 1) % n;
                    if (Near(p.GetPoint2dAt(prev), p.GetPoint2dAt(i), tolerance) ||
                        Near(p.GetPoint2dAt(i), p.GetPoint2dAt(next), tolerance))
                        continue;
                    if (CollinearAndBetween(p.GetPoint2dAt(prev), p.GetPoint2dAt(i), p.GetPoint2dAt(next), tolerance))
                        collinear.Add(i);
                }
            }

            double? length = null;
            try { length = p.Length; } catch { }
            double? area = null;
            if (p.Closed)
            {
                try { area = p.Area; } catch { }
            }

            double? constantWidth = null;
            try { constantWidth = p.ConstantWidth; } catch { }

            return new
            {
                handle = p.ObjectId.IsNull ? null : p.Handle.ToString(),
                layer = p.Layer,
                closed = p.Closed,
                vertex_count = n,
                segment_count = segmentCount,
                length,
                area,
                elevation = p.Elevation,
                constant_width = constantWidth,
                has_bulges = hasBulges,
                has_nonzero_segment_widths = hasVariableWidths,
                clean_supported = !hasBulges && !hasVariableWidths && n >= (p.Closed ? 3 : 2),
                duplicate_vertex_indices = duplicates.Distinct().OrderBy(i => i).ToArray(),
                zero_length_segment_indices = zeroSegments.ToArray(),
                collinear_vertex_indices = collinear.ToArray(),
                tolerance
            };
        }

        public static string[] StringArray(IReadOnlyDictionary<string, object?> args, string key)
        {
            if (!args.TryGetValue(key, out var raw) || raw == null) return Array.Empty<string>();
            if (raw is string || !(raw is IEnumerable source))
                throw new ArgumentException($"参数 {key} 必须是字符串数组。");
            var result = new List<string>();
            foreach (var item in source)
            {
                if (!(item is string text) || string.IsNullOrWhiteSpace(text))
                    throw new ArgumentException($"参数 {key} 必须全部是非空字符串。");
                result.Add(text.Trim());
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static BlockTableRecord ModelSpace(Transaction tr, Database db)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        }
    }

    public sealed class CadPolylineInspectTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.polyline.inspect",
            Description = "检查 2D Lightweight Polyline 的重复点、零长段、共线冗余点及弧段/变宽限制。"
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var tolerance = CadProductionHelpers.Tolerance(context.Args);
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                return PolylineInspectionService.Inspect(context.Database, tr, handle, tolerance);
            });
        }
    }

    public sealed class CadPolylineCleanTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.polyline.clean",
            Description = "安全清理直线型 2D Polyline 的连续重复点/零长段与可选共线冗余点。默认 dry_run。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var tolerance = CadProductionHelpers.Tolerance(context.Args);
            var removeDuplicates = ToolJson.Bool(context.Args, "remove_duplicates") ?? true;
            var removeCollinear = ToolJson.Bool(context.Args, "remove_collinear") ?? false;

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                return PolylineCleanupService.Clean(context.Database, tr, handle, tolerance, removeDuplicates, removeCollinear, context.DryRun);
            });
        }
    }

    public sealed class CadLayerAuditTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.layer.audit",
            Description = "审计图层状态、模型空间实体数量与 AutoCAD Purge 可清理性。"
        };

        public object Execute(CadToolContext context)
        {
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                return LayerAuditService.Audit(context.Database, tr);
            });
        }
    }

    public sealed class CadLayerCleanupTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.layer.cleanup",
            Description = "使用 AutoCAD Database.Purge 仅删除安全可清理的未使用图层。默认 dry_run，且受删除权限保护。",
            IsWrite = true,
            SupportsDryRun = true
        };

        public object Execute(CadToolContext context)
        {
            var requestedNames = CadProductionHelpers.StringArray(context.Args, "names");
            var includeDefpoints = ToolJson.Bool(context.Args, "include_defpoints") ?? false;

            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                return LayerCleanupService.Cleanup(context.Database, tr, requestedNames, includeDefpoints, context.DryRun);
            });
        }
    }

    public sealed class CadDrawingAuditTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor
        {
            Name = "cad.drawing.audit",
            Description = "只读扫描模型空间，汇总实体/图层/块及常见几何与文字质量问题。"
        };

        public object Execute(CadToolContext context)
        {
            var entityLimit = ToolJson.Int(context.Args, "entity_limit") ?? 100000;
            var issueLimit = ToolJson.Int(context.Args, "issue_limit") ?? 1000;
            var tolerance = CadProductionHelpers.Tolerance(context.Args);
            if (entityLimit < 1 || entityLimit > 1000000) throw new ArgumentOutOfRangeException("entity_limit", "entity_limit 必须在 1..1000000。");
            if (issueLimit < 1 || issueLimit > 10000) throw new ArgumentOutOfRangeException("issue_limit", "issue_limit 必须在 1..10000。");

            return CadTransactionRunner.Read(context.Document, tr =>
            {
                return DrawingAuditService.Audit(context.Database, tr, context.Document.Name, entityLimit, issueLimit, tolerance);
            });
        }
    }
}
