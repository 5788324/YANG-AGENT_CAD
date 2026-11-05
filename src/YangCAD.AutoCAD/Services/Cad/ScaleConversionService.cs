using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class ScaleConversionService
    {
        public enum DrawingMode
        {
            Standard1To1,
            ScaledDrawing
        }

        public sealed class Request
        {
            public string[] Handles { get; set; } = Array.Empty<string>();
            public DrawingMode Mode { get; set; } = DrawingMode.Standard1To1;
            public double OldScale { get; set; } = 5.0;
            public double NewScale { get; set; } = 10.0;
            public Point3d BasePoint { get; set; } = Point3d.Origin;
            public bool PreserveTextPaperSize { get; set; } = true;
            public bool PreserveDimensionPaperSize { get; set; } = true;
            public bool PreserveLeaderPaperSize { get; set; } = true;
            public bool PreserveHatchPaperScale { get; set; } = true;
            public bool PreserveLinetypePaperScale { get; set; } = true;
        }

        public sealed class ItemIssue
        {
            public string handle { get; set; } = "";
            public string dxf_type { get; set; } = "";
            public string reason { get; set; } = "";
            public string severity { get; set; } = "skip";
        }

        public sealed class Result
        {
            public bool success { get; set; } = true;
            public bool partial_success { get; set; }
            public bool dry_run { get; set; }
            public string source_document { get; set; } = "";
            public string mode { get; set; } = "standard_1to1";
            public double old_scale { get; set; }
            public double new_scale { get; set; }
            public double geometry_factor { get; set; } = 1.0;
            public double annotation_factor { get; set; } = 1.0;
            public double[] base_point { get; set; } = new[] { 0.0, 0.0, 0.0 };
            public int requested { get; set; }
            public int processed { get; set; }
            public int skipped { get; set; }
            public int failed { get; set; }
            public Dictionary<string, int> processed_by_type { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> skipped_by_reason { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public string[] processed_handles { get; set; } = Array.Empty<string>();
            public string[] changed_handles { get; set; } = Array.Empty<string>();
            public ItemIssue[] issues { get; set; } = Array.Empty<ItemIssue>();
            public string[] warnings { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "single_transaction_one_command_target_one_undo";
        }

        private sealed class MutableResult
        {
            public readonly List<string> ProcessedHandles = new List<string>();
            public readonly List<string> ChangedHandles = new List<string>();
            public readonly List<ItemIssue> Issues = new List<ItemIssue>();
            public readonly List<string> Warnings = new List<string>();
            public readonly Dictionary<string, int> ProcessedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> SkippedByReason = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public int Failed;
        }

        // These are effective object-level values. Setting them creates/updates only the selected object's
        // override and intentionally does not mutate global DimStyle/MLeaderStyle/TextStyle records.
        private static readonly string[] DimensionPaperProperties =
        {
            "Dimtxt", "Dimasz", "Dimgap", "Dimexe", "Dimexo", "Dimcen", "Dimtsz"
        };

        private static readonly string[] LeaderPaperProperties =
        {
            "Dimasz", "Dimtxt", "Dimgap"
        };

        private static readonly string[] MLeaderPaperProperties =
        {
            "TextHeight", "ArrowSize", "LandingGap", "DoglegLength"
        };

        private static readonly HashSet<string> GeometryDxfNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LINE", "LWPOLYLINE", "POLYLINE", "ARC", "CIRCLE", "ELLIPSE", "SPLINE", "REGION", "SOLID", "3DSOLID"
        };

        private static readonly HashSet<string> UnderlayDxfNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IMAGE", "PDFUNDERLAY", "DGNUNDERLAY", "DWFUNDERLAY"
        };

        public static DrawingMode ParseMode(string? value)
        {
            var text = (value ?? "").Trim().ToLowerInvariant();
            if (text == "standard" || text == "standard_1to1" || text == "1to1" || text == "1:1")
                return DrawingMode.Standard1To1;
            if (text == "scaled" || text == "scaled_drawing" || text == "legacy" || text == "non_1to1")
                return DrawingMode.ScaledDrawing;
            throw new ArgumentException("mode 必须是 standard_1to1 或 scaled_drawing。");
        }

        public static Result Convert(CadExecutionContext context, Request request, bool dryRun)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (request == null) throw new ArgumentNullException(nameof(request));
            Validate(request);

            var geometryFactor = request.Mode == DrawingMode.ScaledDrawing ? request.OldScale / request.NewScale : 1.0;
            var annotationFactor = request.Mode == DrawingMode.Standard1To1 ? request.NewScale / request.OldScale : 1.0;
            var mutable = new MutableResult();
            var handles = request.Handles
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var handle in handles)
            {
                ProcessOne(context, request, dryRun, geometryFactor, annotationFactor, handle, mutable);
            }

            var skipped = mutable.Issues.Count(x => string.Equals(x.severity, "skip", StringComparison.OrdinalIgnoreCase));
            var failed = mutable.Issues.Count(x => string.Equals(x.severity, "fail", StringComparison.OrdinalIgnoreCase));
            var processed = mutable.ProcessedHandles.Count;
            return new Result
            {
                success = failed == 0 && processed > 0,
                partial_success = processed > 0 && (skipped > 0 || failed > 0),
                dry_run = dryRun,
                source_document = context.Document.Name ?? "",
                mode = request.Mode == DrawingMode.Standard1To1 ? "standard_1to1" : "scaled_drawing",
                old_scale = request.OldScale,
                new_scale = request.NewScale,
                geometry_factor = geometryFactor,
                annotation_factor = annotationFactor,
                base_point = new[] { request.BasePoint.X, request.BasePoint.Y, request.BasePoint.Z },
                requested = handles.Length,
                processed = processed,
                skipped = skipped,
                failed = failed,
                processed_by_type = mutable.ProcessedByType,
                skipped_by_reason = mutable.SkippedByReason,
                processed_handles = mutable.ProcessedHandles.ToArray(),
                changed_handles = dryRun ? Array.Empty<string>() : mutable.ChangedHandles.ToArray(),
                issues = mutable.Issues.ToArray(),
                warnings = mutable.Warnings.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static void Validate(Request request)
        {
            if (request.Handles == null || request.Handles.Length == 0)
                throw new ArgumentException("至少需要一个对象 Handle。", nameof(request.Handles));
            if (!IsFinitePositive(request.OldScale) || !IsFinitePositive(request.NewScale))
                throw new ArgumentOutOfRangeException("old_scale/new_scale", "比例分母必须是大于 0 的有限数值。");
            if (Math.Abs(request.OldScale - request.NewScale) < 1e-12)
                throw new ArgumentException("当前比例与目标比例相同，不需要转换。");
            var geometryFactor = request.OldScale / request.NewScale;
            var annotationFactor = request.NewScale / request.OldScale;
            if (!IsFinitePositive(geometryFactor) || !IsFinitePositive(annotationFactor))
                throw new ArgumentOutOfRangeException("scale", "比例换算结果无效。");
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void ProcessOne(
            CadExecutionContext context,
            Request request,
            bool dryRun,
            double geometryFactor,
            double annotationFactor,
            string handle,
            MutableResult result)
        {
            var dxf = "UNKNOWN";
            // Per-object nested transaction: one bad entity rolls back only itself. The runner-owned
            // outer transaction still groups the whole YGSCALE/MCP operation into one user undo target.
            // Dry-run also uses a nested read transaction so one bad Handle never aborts the full preview.
            using (var itemTransaction = context.Database.TransactionManager.StartTransaction())
            {
                var processedBefore = result.ProcessedHandles.Count;
                var changedBefore = result.ChangedHandles.Count;
                var typeSnapshot = new Dictionary<string, int>(result.ProcessedByType, StringComparer.OrdinalIgnoreCase);
                var issueCountBefore = result.Issues.Count;
                var skipSnapshot = new Dictionary<string, int>(result.SkippedByReason, StringComparer.OrdinalIgnoreCase);
                try
                {
                    ProcessOneInsideTransaction(context, itemTransaction, request, dryRun, geometryFactor, annotationFactor, handle, ref dxf, result);
                    itemTransaction.Commit();
                }
                catch (Exception ex)
                {
                    try { itemTransaction.Abort(); } catch { }
                    while (result.ProcessedHandles.Count > processedBefore) result.ProcessedHandles.RemoveAt(result.ProcessedHandles.Count - 1);
                    while (result.ChangedHandles.Count > changedBefore) result.ChangedHandles.RemoveAt(result.ChangedHandles.Count - 1);
                    while (result.Issues.Count > issueCountBefore) result.Issues.RemoveAt(result.Issues.Count - 1);
                    result.ProcessedByType.Clear();
                    foreach (var pair in typeSnapshot) result.ProcessedByType[pair.Key] = pair.Value;
                    result.SkippedByReason.Clear();
                    foreach (var pair in skipSnapshot) result.SkippedByReason[pair.Key] = pair.Value;
                    result.Failed++;
                    result.Issues.Add(new ItemIssue
                    {
                        handle = handle,
                        dxf_type = dxf,
                        severity = "fail",
                        reason = ex.Message
                    });
                }
            }
        }

        private static void ProcessOneInsideTransaction(
            CadExecutionContext context,
            Transaction transaction,
            Request request,
            bool dryRun,
            double geometryFactor,
            double annotationFactor,
            string handle,
            ref string dxf,
            MutableResult result)
        {
            var id = EntityHelpers.FromHandle(context.Database, handle);
            var entity = transaction.GetObject(id, dryRun ? OpenMode.ForRead : OpenMode.ForWrite, false) as Entity;
            if (entity == null)
            {
                Skip(result, handle, "UNKNOWN", "not_entity", "Handle 不是可转换 Entity。");
                return;
            }
            dxf = SafeDxf(entity);

            var reason = UnsupportedReason(transaction, entity, request.Mode);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Skip(result, handle, dxf, reason!, ExplainReason(reason!));
                return;
            }

            if (!dryRun)
            {
                if (request.Mode == DrawingMode.Standard1To1)
                    ApplyStandardMode(transaction, entity, annotationFactor, request, result);
                else
                    ApplyScaledDrawingMode(transaction, entity, geometryFactor, request, result);
            }

            MarkProcessed(result, handle, dxf, changed: !dryRun);
        }

        private static string? UnsupportedReason(Transaction tr, Entity entity, DrawingMode mode)
        {
            var dxf = SafeDxf(entity);
            var typeName = entity.GetType().FullName ?? entity.GetType().Name;
            var assemblyName = entity.GetType().Assembly.GetName().Name ?? "";

            if (IsOutsideModelSpace(tr, entity)) return "non_model_space";
            if (IsAnnotative(entity)) return "annotative_object";
            if (entity is BlockReference annotativeBlock && IsAnnotativeBlockDefinition(tr, annotativeBlock)) return "annotative_object";
            if (UnderlayDxfNames.Contains(dxf)) return "underlay_or_raster";
            if (dxf.IndexOf("PROXY", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0)
                return "proxy_or_third_party";
            if (dxf.StartsWith("AECC", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("Autodesk.Civil.", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.IndexOf("Aecc", StringComparison.OrdinalIgnoreCase) >= 0)
                return "civil3d_object";
            if (dxf.StartsWith("AEC", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("Autodesk.Aec", StringComparison.OrdinalIgnoreCase))
                return "aec_object";

            if (entity is BlockReference block)
            {
                if (IsXref(tr, block)) return "xref";
                // Dynamic block visibility/action state can change under a geometry transform;
                // until an AutoCAD runtime regression exists, do not mutate it.
                if (block.IsDynamicBlock) return "dynamic_block_not_verified";
                // In 1:1 drawings a BlockReference may be a real-size component OR an annotation symbol.
                // Scaling the whole block would be unsafe, while scaling only nested geometry would mutate the
                // shared definition. V1 therefore leaves standard-mode blocks untouched and reports them.
                if (mode == DrawingMode.Standard1To1) return "standard_mode_block_ambiguous";
                return null;
            }

            if (entity is DBText || entity is MText || entity is Dimension || entity is Leader || entity is MLeader || entity is Hatch || entity is AttributeReference)
                return null;
            if (GeometryDxfNames.Contains(dxf)) return null;
            return "unsupported_type";
        }

        private static void ApplyStandardMode(Transaction tr, Entity entity, double annotationFactor, Request request, MutableResult result)
        {
            // Geometry remains true 1:1. Only model-space annotation/display sizes change so the paper
            // appearance remains approximately stable at the target viewport/plot scale.
            if (request.PreserveLinetypePaperScale)
                ScaleEntityLinetype(entity, annotationFactor, result);

            if (entity is DBText dbText)
            {
                if (request.PreserveTextPaperSize) dbText.Height *= annotationFactor;
                return;
            }

            if (entity is MText mText)
            {
                if (request.PreserveTextPaperSize)
                {
                    mText.TextHeight *= annotationFactor;
                    if (mText.Width > 0) mText.Width *= annotationFactor;
                }
                return;
            }

            if (entity is AttributeReference attribute)
            {
                if (request.PreserveTextPaperSize) attribute.Height *= annotationFactor;
                return;
            }

            if (entity is Dimension dimension)
            {
                if (request.PreserveDimensionPaperSize)
                    ScaleDoubleProperties(dimension, DimensionPaperProperties, annotationFactor, result);
                // Standard 1:1 mode must keep Dimlfac unchanged because measured geometry is still true-size.
                return;
            }

            if (entity is MLeader mLeader)
            {
                if (request.PreserveLeaderPaperSize)
                    ScaleDoubleProperties(mLeader, MLeaderPaperProperties, annotationFactor, result);
                return;
            }

            if (entity is Leader leader)
            {
                if (request.PreserveLeaderPaperSize)
                    ScaleDoubleProperties(leader, LeaderPaperProperties, annotationFactor, result);
                return;
            }

            if (entity is Hatch hatch)
            {
                if (request.PreserveHatchPaperScale && !hatch.IsGradient)
                {
                    hatch.PatternScale *= annotationFactor;
                    hatch.SetHatchPattern(hatch.PatternType, hatch.PatternName);
                    hatch.EvaluateHatch(true);
                }
                return;
            }

            // Ordinary true-size geometry does not move or resize in standard mode.
        }

        private static void ApplyScaledDrawingMode(Transaction tr, Entity entity, double geometryFactor, Request request, MutableResult result)
        {
            var transform = Matrix3d.Scaling(geometryFactor, request.BasePoint);
            var oldLinetypeScale = entity.LinetypeScale;

            if (entity is DBText dbText)
            {
                var oldHeight = dbText.Height;
                entity.TransformBy(transform);
                if (request.PreserveTextPaperSize) dbText.Height = oldHeight;
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is MText mText)
            {
                var oldHeight = mText.TextHeight;
                var oldWidth = mText.Width;
                entity.TransformBy(transform);
                if (request.PreserveTextPaperSize)
                {
                    mText.TextHeight = oldHeight;
                    mText.Width = oldWidth;
                }
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is AttributeReference attribute)
            {
                var oldHeight = attribute.Height;
                entity.TransformBy(transform);
                if (request.PreserveTextPaperSize) attribute.Height = oldHeight;
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is Dimension dimension)
            {
                var visual = SnapshotDoubleProperties(dimension, DimensionPaperProperties);
                if (!TryGetDoubleProperty(dimension, "Dimlfac", out var oldDimlfac))
                    throw new InvalidOperationException("无法读取对象级 DIMLFAC；已回滚该标注，防止尺寸读数失真。");
                entity.TransformBy(transform);
                if (request.PreserveDimensionPaperSize)
                    RestoreDoubleProperties(dimension, visual, result);
                if (!TrySetDoubleProperty(dimension, "Dimlfac", oldDimlfac / geometryFactor, result))
                    throw new InvalidOperationException("无法设置对象级 DIMLFAC；已回滚该标注，防止尺寸读数失真。");
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is MLeader mLeader)
            {
                var visual = SnapshotDoubleProperties(mLeader, MLeaderPaperProperties);
                if (request.PreserveLeaderPaperSize && visual.Count == 0)
                    throw new InvalidOperationException("当前 MLeader 无可补偿的纸面尺寸属性；安全跳过而非误报成功。");
                entity.TransformBy(transform);
                if (request.PreserveLeaderPaperSize)
                    RestoreDoubleProperties(mLeader, visual, result);
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is Leader leader)
            {
                var visual = SnapshotDoubleProperties(leader, LeaderPaperProperties);
                if (request.PreserveLeaderPaperSize && visual.Count == 0)
                    throw new InvalidOperationException("当前 Leader 无可补偿的纸面尺寸属性；安全跳过而非误报成功。");
                entity.TransformBy(transform);
                if (request.PreserveLeaderPaperSize)
                    RestoreDoubleProperties(leader, visual, result);
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is Hatch hatch)
            {
                var preservePattern = request.PreserveHatchPaperScale && !hatch.IsGradient;
                var oldPatternScale = preservePattern ? hatch.PatternScale : 0.0;
                entity.TransformBy(transform);
                if (preservePattern)
                {
                    hatch.PatternScale = oldPatternScale;
                    hatch.SetHatchPattern(hatch.PatternType, hatch.PatternName);
                    hatch.EvaluateHatch(true);
                }
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            if (entity is BlockReference block)
            {
                var attributes = SnapshotBlockAttributes(tr, block);
                entity.TransformBy(transform);
                if (request.PreserveTextPaperSize)
                    RestoreBlockAttributes(tr, attributes, result);
                RestoreLinetype(entity, oldLinetypeScale, request, result);
                return;
            }

            entity.TransformBy(transform);
            RestoreLinetype(entity, oldLinetypeScale, request, result);
        }

        private sealed class AttributeSnapshot
        {
            public ObjectId Id { get; set; } = ObjectId.Null;
            public double Height { get; set; }
        }

        private static List<AttributeSnapshot> SnapshotBlockAttributes(Transaction tr, BlockReference block)
        {
            var result = new List<AttributeSnapshot>();
            foreach (ObjectId id in block.AttributeCollection)
            {
                if (id.IsNull || id.IsErased) continue;
                try
                {
                    if (tr.GetObject(id, OpenMode.ForRead, false) is AttributeReference attribute)
                        result.Add(new AttributeSnapshot { Id = id, Height = attribute.Height });
                }
                catch { }
            }
            return result;
        }

        private static void RestoreBlockAttributes(Transaction tr, IEnumerable<AttributeSnapshot> snapshots, MutableResult result)
        {
            foreach (var snapshot in snapshots)
            {
                if (tr.GetObject(snapshot.Id, OpenMode.ForWrite, false) is AttributeReference attribute)
                    attribute.Height = snapshot.Height;
            }
        }

        private static void ScaleEntityLinetype(Entity entity, double factor, MutableResult result)
        {
            if (entity.LinetypeScale > 0) entity.LinetypeScale *= factor;
        }

        private static void RestoreLinetype(Entity entity, double oldValue, Request request, MutableResult result)
        {
            if (!request.PreserveLinetypePaperScale) return;
            entity.LinetypeScale = oldValue;
        }

        private static Dictionary<string, double> SnapshotDoubleProperties(object target, IEnumerable<string> names)
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (TryGetDoubleProperty(target, name, out var value)) values[name] = value;
            }
            return values;
        }

        private static void ScaleDoubleProperties(object target, IEnumerable<string> names, double factor, MutableResult result)
        {
            foreach (var name in names)
            {
                if (TryGetDoubleProperty(target, name, out var value))
                    TrySetDoubleProperty(target, name, value * factor, result);
            }
        }

        private static void RestoreDoubleProperties(object target, IReadOnlyDictionary<string, double> values, MutableResult result)
        {
            foreach (var pair in values) TrySetDoubleProperty(target, pair.Key, pair.Value, result);
        }

        private static void TryScaleDoubleProperty(object target, string name, double factor, MutableResult result)
        {
            if (TryGetDoubleProperty(target, name, out var value))
                TrySetDoubleProperty(target, name, value * factor, result);
        }

        private static bool TryGetDoubleProperty(object target, string name, out double value)
        {
            value = 0;
            try
            {
                var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null || !property.CanRead) return false;
                var raw = property.GetValue(target, null);
                if (raw == null) return false;
                value = System.Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch { return false; }
        }

        private static bool TrySetDoubleProperty(object target, string name, double value, MutableResult result)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null || !property.CanWrite) return false;
            try
            {
                object converted;
                if (property.PropertyType == typeof(double)) converted = value;
                else if (property.PropertyType == typeof(float)) converted = (float)value;
                else converted = System.Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
                property.SetValue(target, converted, null);
                return true;
            }
            catch (System.Exception ex)
            {
                throw new InvalidOperationException($"{target.GetType().Name}.{name} 调整失败；为避免部分转换，本对象已回滚：{ex.Message}", ex);
            }
        }

        private static bool IsOutsideModelSpace(Transaction tr, Entity entity)
        {
            try
            {
                if (entity.OwnerId.IsNull) return false;
                if (tr.GetObject(entity.OwnerId, OpenMode.ForRead, false) is BlockTableRecord owner)
                    return !string.Equals(owner.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            return false;
        }

        private static bool IsAnnotative(Entity entity)
        {
            return IsTrueLikeProperty(entity, "Annotative") || IsTrueLikeProperty(entity, "EnableAnnotationScale");
        }

        private static bool IsAnnotativeBlockDefinition(Transaction tr, BlockReference block)
        {
            try
            {
                if (tr.GetObject(block.BlockTableRecord, OpenMode.ForRead, false) is BlockTableRecord definition)
                    return IsTrueLikeProperty(definition, "Annotative");
            }
            catch { }
            return false;
        }

        private static bool IsTrueLikeProperty(object target, string name)
        {
            try
            {
                var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null || !property.CanRead) return false;
                var raw = property.GetValue(target, null);
                if (raw is bool boolean) return boolean;
                var text = System.Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                return text.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Annotative", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsXref(Transaction tr, BlockReference block)
        {
            try
            {
                if (tr.GetObject(block.BlockTableRecord, OpenMode.ForRead, false) is BlockTableRecord definition)
                    return definition.IsFromExternalReference;
            }
            catch { }
            return false;
        }

        private static string SafeDxf(Entity entity)
        {
            try { return entity.GetRXClass().DxfName ?? entity.GetType().Name; }
            catch { return entity.GetType().Name; }
        }

        private static void MarkProcessed(MutableResult result, string handle, string dxf, bool changed)
        {
            result.ProcessedHandles.Add(handle);
            if (changed) result.ChangedHandles.Add(handle);
            if (!result.ProcessedByType.ContainsKey(dxf)) result.ProcessedByType[dxf] = 0;
            result.ProcessedByType[dxf]++;
        }

        private static void Skip(MutableResult result, string handle, string dxf, string reason, string message)
        {
            result.Issues.Add(new ItemIssue { handle = handle, dxf_type = dxf, reason = message, severity = "skip" });
            if (!result.SkippedByReason.ContainsKey(reason)) result.SkippedByReason[reason] = 0;
            result.SkippedByReason[reason]++;
        }

        private static string ExplainReason(string reason)
        {
            switch (reason)
            {
                case "non_model_space": return "第一版只处理模型空间顶层对象。";
                case "annotative_object": return "注释性对象第一版安全跳过，避免重复应用注释比例。";
                case "underlay_or_raster": return "图片/PDF/DGN/DWF Underlay 第一版不处理。";
                case "proxy_or_third_party": return "Proxy/第三方对象第一版不处理。";
                case "civil3d_object": return "Civil 3D 专业对象禁止直接比例缩放。";
                case "aec_object": return "AEC 智能对象第一版不处理。";
                case "xref": return "Xref 第一版不处理，不修改外部参照定义。";
                case "dynamic_block_not_verified": return "动态块比例缩放未经实机验证，当前安全跳过以保护块参数和可见性状态。";
                case "standard_mode_block_ambiguous": return "1:1 模式无法安全判断块是实尺寸几何还是注释符号，第一版保持普通块不变。";
                default: return "当前对象类型第一版不支持。";
            }
        }
    }
}
