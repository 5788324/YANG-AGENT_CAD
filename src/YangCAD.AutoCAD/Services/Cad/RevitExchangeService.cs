using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// YangCAD RVT Exchange v1.
    /// The source document is read-only. Export clones the in-memory database with Database.Wblock(),
    /// rewrites only the detached clone, and writes *_RVT.dwg + *_RVT_MAP.json + *_RVT_REPORT.json.
    /// </summary>
    public static class RevitExchangeService
    {
        public const string SchemaVersion = "yangcad-rvt-exchange/1.0";
        private const int LayerNameLimit = 180;

        public sealed class Request
        {
            public string OutputFolder { get; set; } = "";
            public string OutputSuffix { get; set; } = "_RVT";
            public bool ScanBlockDefinitions { get; set; } = true;
            public bool IncludePaperSpace { get; set; } = false;
            // Separate from NormalizeGeometryOverrides (which only means ByLayer appearance).
            // "local" preserves entity WCS data and writes a nonzero MAP origin for YangTools.
            public string CoordinateOriginMode { get; set; } = "local";
            public bool NormalizeGeometryOverrides { get; set; } = true;
        }

        /// <summary>
        /// Stable product result shared by GUI and MCP adapters.  Property names intentionally match
        /// the external product contract so both surfaces serialize the same business payload.
        /// </summary>
        public sealed class Result
        {
            public bool success { get; set; } = true;
            public bool dry_run { get; set; }
            public string schema_version { get; set; } = RevitExchangeService.SchemaVersion;
            public string units { get; set; } = "mm";
            public object[] origin { get; set; } = new object[] { 0.0, 0.0, 0.0 };
            public string source_insunits { get; set; } = "";
            public string coordinate_origin_mode { get; set; } = "local";
            public int model_space_entities { get; set; }
            public int paper_space_entities { get; set; }
            public bool mixed_spaces { get; set; }
            public string source_document { get; set; } = "";
            public string output_dwg { get; set; } = "";
            public string map_path { get; set; } = "";
            public string report_path { get; set; } = "";
            public int scanned_entities { get; set; }
            public int classified_entities { get; set; }
            public int unsupported_count { get; set; }
            public int generated_layers { get; set; }
            public Dictionary<string, int> category_counts { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> dxf_type_counts { get; set; } = new Dictionary<string, int>();
            public string[] handles { get; set; } = Array.Empty<string>();
            public UnsupportedItem[] unsupported_items { get; set; } = Array.Empty<UnsupportedItem>();
            public string[] warnings { get; set; } = Array.Empty<string>();
            public string[] changed_object_ids { get; set; } = Array.Empty<string>();
            public string undo_semantics { get; set; } = "source_document_read_only_no_undo_required";
        }

        public sealed class UnsupportedItem
        {
            public string Handle { get; set; } = "";
            public string DxfType { get; set; } = "";
            public string reason { get; set; } = "";
        }

        private sealed class Analysis
        {
            public readonly Dictionary<string, LayerDefinition> Layers = new Dictionary<string, LayerDefinition>(StringComparer.OrdinalIgnoreCase);
            public readonly List<EntityRecord> Entities = new List<EntityRecord>();
            public readonly List<string> Warnings = new List<string>();
            public readonly Dictionary<string, int> CategoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> UnresolvedByBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> UnsupportedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<UnsupportedItem> UnsupportedItems = new List<UnsupportedItem>();
            public int ScannedEntities;
            public int ClassifiedEntities;
            public int MixedMText;
            public int GradientHatches;
            public int UnsupportedEntities;
            public int NormalBlockDefinitions;
            public int SkippedBlockDefinitionEntities;
            public int SkippedAnonymousBlocks;
            public int SkippedXrefBlocks;
            public int DynamicBlockReferences;
            public int ModelSpaceEntities;
            public int PaperSpaceEntities;
            public bool HasExtents;
            public double MinX = double.PositiveInfinity;
            public double MinY = double.PositiveInfinity;
            public double MaxX = double.NegativeInfinity;
            public double MaxY = double.NegativeInfinity;
            public object[] Origin = new object[] { 0.0, 0.0, 0.0 };
            public string OriginMode = "source";
        }

        private sealed class LayerDefinition
        {
            public string LayerName { get; set; } = "";
            public string Category { get; set; } = "";
            public string Signature { get; set; } = "";
            public Dictionary<string, object?> Traits { get; set; } = new Dictionary<string, object?>();
        }

        private sealed class EntityRecord
        {
            public string Handle { get; set; } = "";
            // DxfType is the official RVT Exchange v1 entity discriminator.
            // YangTools may keep legacy aliases for old files, but new YangCAD exports always emit DxfType.
            public string DxfType { get; set; } = "";
            public string Category { get; set; } = "";
            public string OriginalLayer { get; set; } = "";
            public string ExchangeLayer { get; set; } = "";
            public string Owner { get; set; } = "";
            public Dictionary<string, object?> Traits { get; set; } = new Dictionary<string, object?>();
            public Dictionary<string, object?> Data { get; set; } = new Dictionary<string, object?>();
        }

        private sealed class EffectiveAppearance
        {
            public LineWeight LineWeight { get; set; } = LineWeight.ByLineWeightDefault;
            public string LineTypeName { get; set; } = "Continuous";
            public ObjectId LineTypeId { get; set; } = ObjectId.Null;
            public Color Color { get; set; } = Color.FromColorIndex(ColorMethod.ByAci, 7);
            public string ColorName { get; set; } = "White";
            public bool HasByBlockFallback { get; set; }
        }

        private sealed class TextTraits
        {
            public double Height { get; set; }
            public string StyleName { get; set; } = "";
            public string FontName { get; set; } = "";
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public bool Underline { get; set; }
            public bool Overline { get; set; }
            public bool StrikeThrough { get; set; }
            public bool Superscript { get; set; }
            public bool Subscript { get; set; }
            public bool Stacked { get; set; }
            public bool MixedFormat { get; set; }
            public double ObliqueDegrees { get; set; }
            public double WidthFactor { get; set; } = 1.0;
            public string PlainText { get; set; } = "";
            public string RawText { get; set; } = "";
            public string[] InlineFonts { get; set; } = Array.Empty<string>();
        }

        public static Result Preview(Database db, Transaction tr, string documentName, Request request)
        {
            ValidateRequest(request);
            var analysis = Analyze(db, tr, request, applyLayers: false);
            var units = ExchangeUnits(db.Insunits, analysis); // Preview must fail closed on unsupported explicit units too.
            var paths = ResolveOutputPaths(db, documentName, request, reserveUniquePath: true);
            return BuildResult(true, documentName, paths, analysis, units, db.Insunits.ToString());
        }

        public static Result Export(Database sourceDb, Transaction sourceTr, string documentName, Request request)
        {
            ValidateRequest(request);
            // Analyze the source first so Preview and Real report the same semantic counts.
            var sourceAnalysis = Analyze(sourceDb, sourceTr, request, applyLayers: false);
            var sourceUnits = ExchangeUnits(sourceDb.Insunits, sourceAnalysis); // Fail before creating any output directory/files.
            var paths = ResolveOutputPaths(sourceDb, documentName, request, reserveUniquePath: true);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Dwg)!);

            Analysis outputAnalysis;
            var tempStem = Path.Combine(Path.GetDirectoryName(paths.Dwg)!, ".yangcad-rvt-" + Guid.NewGuid().ToString("N"));
            var tempDwg = tempStem + ".dwg";
            var tempMap = tempStem + "_MAP.json";
            var tempReport = tempStem + "_REPORT.json";
            var movedDwg = false;
            var movedMap = false;
            var movedReport = false;
            try
            {
                using (var outputDb = sourceDb.Wblock())
                {
                    using (var outTr = outputDb.TransactionManager.StartTransaction())
                    {
                        outputAnalysis = Analyze(outputDb, outTr, request, applyLayers: true);
                        outTr.Commit();
                    }
                    if (sourceAnalysis.ClassifiedEntities != outputAnalysis.ClassifiedEntities)
                    {
                        const string drift = "source-clone-classification-count-drift";
                        sourceAnalysis.Warnings.Add(drift);
                        outputAnalysis.Warnings.Add(drift);
                    }
                    outputDb.SaveAs(tempDwg, DwgVersion.Current);
                    WriteMap(tempMap, documentName, paths.Dwg, outputDb, outputAnalysis);
                    WriteReport(tempReport, documentName, paths.Dwg, outputDb, outputAnalysis);
                }

                File.Move(tempDwg, paths.Dwg);
                movedDwg = true;
                File.Move(tempMap, paths.Map);
                movedMap = true;
                File.Move(tempReport, paths.Report);
                movedReport = true;
            }
            catch
            {
                DeleteIfExists(tempDwg); DeleteIfExists(tempMap); DeleteIfExists(tempReport);
                if (movedDwg) DeleteIfExists(paths.Dwg);
                if (movedMap) DeleteIfExists(paths.Map);
                if (movedReport) DeleteIfExists(paths.Report);
                throw;
            }

            return BuildResult(false, documentName, paths, outputAnalysis, sourceUnits, sourceDb.Insunits.ToString());
        }

        private static void ValidateRequest(Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.OutputSuffix)) request.OutputSuffix = "_RVT";
            request.OutputSuffix = SanitizeFilePart(request.OutputSuffix.Trim());
            if (string.IsNullOrWhiteSpace(request.OutputSuffix)) request.OutputSuffix = "_RVT";
            request.CoordinateOriginMode = (request.CoordinateOriginMode ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(request.CoordinateOriginMode)) request.CoordinateOriginMode = "local";
            if (request.CoordinateOriginMode != "local" && request.CoordinateOriginMode != "source")
                throw new ArgumentException("coordinate_origin_mode 只能为 local 或 source。", nameof(request));
        }

        private static Result BuildResult(bool dryRun, string documentName, (string Dwg, string Map, string Report) paths, Analysis analysis, string units, string sourceInsunits)
        {
            return new Result
            {
                success = true,
                dry_run = dryRun,
                units = units,
                origin = analysis.Origin.ToArray(),
                source_insunits = sourceInsunits,
                coordinate_origin_mode = analysis.OriginMode,
                model_space_entities = analysis.ModelSpaceEntities,
                paper_space_entities = analysis.PaperSpaceEntities,
                mixed_spaces = analysis.ModelSpaceEntities > 0 && analysis.PaperSpaceEntities > 0,
                source_document = documentName,
                output_dwg = paths.Dwg,
                map_path = paths.Map,
                report_path = paths.Report,
                scanned_entities = analysis.ScannedEntities,
                classified_entities = analysis.ClassifiedEntities,
                unsupported_count = analysis.UnsupportedEntities,
                generated_layers = analysis.Layers.Count,
                category_counts = new Dictionary<string, int>(analysis.CategoryCounts, StringComparer.OrdinalIgnoreCase),
                dxf_type_counts = analysis.Entities.GroupBy(x => x.DxfType, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
                handles = analysis.Entities.Select(x => x.Handle).ToArray(),
                unsupported_items = analysis.UnsupportedItems.ToArray(),
                warnings = analysis.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        private static (string Dwg, string Map, string Report) ResolveOutputPaths(Database db, string documentName, Request request, bool reserveUniquePath)
        {
            var sourcePath = db.Filename;
            var baseName = Path.GetFileNameWithoutExtension(!string.IsNullOrWhiteSpace(sourcePath) ? sourcePath : documentName);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "YangCAD_Export";

            string folder;
            if (!string.IsNullOrWhiteSpace(request.OutputFolder)) folder = Environment.ExpandEnvironmentVariables(request.OutputFolder.Trim());
            else if (!string.IsNullOrWhiteSpace(sourcePath) && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(sourcePath))) folder = Path.GetDirectoryName(sourcePath)!;
            else folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "YangCADExports");

            var stem = SanitizeFilePart(baseName + request.OutputSuffix);
            if (string.IsNullOrWhiteSpace(stem)) stem = "YangCAD_RVT";
            var dwg = Path.Combine(folder, stem + ".dwg");
            if (reserveUniquePath) dwg = GetAvailablePath(dwg);
            var outputStem = Path.Combine(Path.GetDirectoryName(dwg)!, Path.GetFileNameWithoutExtension(dwg));
            return (dwg, outputStem + "_MAP.json", outputStem + "_REPORT.json");
        }

        private static string GetAvailablePath(string requested)
        {
            bool Available(string dwg)
            {
                var stemPath = Path.Combine(Path.GetDirectoryName(dwg)!, Path.GetFileNameWithoutExtension(dwg));
                return !File.Exists(dwg) && !File.Exists(stemPath + "_MAP.json") && !File.Exists(stemPath + "_REPORT.json");
            }

            if (Available(requested)) return requested;
            var dir = Path.GetDirectoryName(requested)!;
            var stem = Path.GetFileNameWithoutExtension(requested);
            var ext = Path.GetExtension(requested);
            for (var i = 1; i <= 9999; i++)
            {
                var candidate = Path.Combine(dir, stem + "_" + i.ToString(CultureInfo.InvariantCulture) + ext);
                if (Available(candidate)) return candidate;
            }
            throw new IOException("无法生成唯一输出文件名。请更换输出目录。");
        }

        private static void DeleteIfExists(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static Analysis Analyze(Database db, Transaction tr, Request request, bool applyLayers)
        {
            var analysis = new Analysis();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (btr.IsFromExternalReference || btr.IsFromOverlayReference)
                {
                    analysis.SkippedXrefBlocks++;
                    continue;
                }
                if (btr.IsAnonymous && !btr.IsLayout)
                {
                    analysis.SkippedAnonymousBlocks++;
                    continue;
                }
                if (!btr.IsLayout)
                {
                    analysis.NormalBlockDefinitions++;
                    if (request.ScanBlockDefinitions)
                    {
                        // RVT Exchange v1 has no BlockReference instance transform contract.
                        // Publishing definition children would place them at definition-space coordinates in YangTools.
                        // Keep normal block definitions diagnostic-only and fail closed at the BlockReference entity.
                        foreach (ObjectId ignored in btr) analysis.SkippedBlockDefinitionEntities++;
                    }
                    continue;
                }
                if (!request.IncludePaperSpace && !string.Equals(btr.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (ObjectId id in btr)
                {
                    analysis.ScannedEntities++;
                    var mode = applyLayers ? OpenMode.ForWrite : OpenMode.ForRead;
                    if (!(tr.GetObject(id, mode, false) is Entity entity)) continue;
                    try
                    {
                        ClassifyEntity(db, tr, btr, entity, request, applyLayers, analysis);
                    }
                    catch (Exception ex)
                    {
                        RecordUnsupported(analysis, entity, "classification_failed:" + ex.GetType().Name);
                        analysis.Warnings.Add("entity-failed:" + entity.GetRXClass().DxfName + ":" + ex.GetType().Name);
                    }
                }
            }

            // Local origin is a placement hint for native YangTools reconstruction; entity Data remains WCS.
            // A single origin cannot reconcile ModelSpace and PaperSpace with unrelated coordinates.
            analysis.OriginMode = request.CoordinateOriginMode;
            if (analysis.ModelSpaceEntities > 0 && analysis.PaperSpaceEntities > 0)
            {
                analysis.OriginMode = "source";
                analysis.Warnings.Add("危险：Exchange 同时包含模型空间和布局空间；坐标系不同，已禁用局部原点转换。必须分开输出才能用于 Revit 原生重建。");
            }
            else if (request.CoordinateOriginMode == "local")
            {
                if (analysis.HasExtents)
                    analysis.Origin = new object[] { Round(analysis.MinX, 8), Round(analysis.MinY, 8), 0.0 };
                else if (analysis.ClassifiedEntities > 0)
                    analysis.Warnings.Add("无法计算当前选择空间的几何范围；保留原始 origin=[0,0,0]。");
            }
            if (analysis.HasExtents && (analysis.MaxX - analysis.MinX > 10000 || analysis.MaxY - analysis.MinY > 10000))
                analysis.Warnings.Add("选中对象的坐标跨度超过 10000 个图纸单位；局部原点只平移不缩放，请在 Revit 端核对范围。");
            if (!request.IncludePaperSpace)
                analysis.Warnings.Add("MAP仅包含模型空间；_RVT.dwg为整个源DWG的独立克隆，仍保留原有布局实体，直接导入DWG与按MAP重建的范围不相同。");

            if (analysis.UnresolvedByBlock.Count > 0)
                analysis.Warnings.Add("ByBlock 外观无法在块定义层面完全解析，已使用来源图层作为 fallback；数量=" + analysis.UnresolvedByBlock.Count.ToString(CultureInfo.InvariantCulture));
            if (analysis.MixedMText > 0)
                analysis.Warnings.Add("存在混合格式 MText，已标记 MIXED；数量=" + analysis.MixedMText.ToString(CultureInfo.InvariantCulture));
            if (analysis.GradientHatches > 0)
                analysis.Warnings.Add("存在 Gradient Hatch，Revit FilledRegion 无直接等价，已单独分类；数量=" + analysis.GradientHatches.ToString(CultureInfo.InvariantCulture));
            if (analysis.NormalBlockDefinitions > 0 && request.ScanBlockDefinitions)
                analysis.Warnings.Add("普通块定义仅做诊断，不发布其子实体：RVT Exchange v1 未定义 BlockReference 实例变换；块定义=" + analysis.NormalBlockDefinitions.ToString(CultureInfo.InvariantCulture) + "，跳过子实体=" + analysis.SkippedBlockDefinitionEntities.ToString(CultureInfo.InvariantCulture));
            if (analysis.SkippedAnonymousBlocks > 0)
                analysis.Warnings.Add("跳过匿名/系统块定义（含尺寸匿名块等）；数量=" + analysis.SkippedAnonymousBlocks.ToString(CultureInfo.InvariantCulture));
            if (analysis.DynamicBlockReferences > 0)
                analysis.Warnings.Add("检测到动态块引用；动态块当前显示状态可能依赖 AutoCAD 匿名表示，RVT Exchange v1 不直接修改匿名定义，请在 Revit 导入前重点复核；数量=" + analysis.DynamicBlockReferences.ToString(CultureInfo.InvariantCulture));
            if (analysis.SkippedXrefBlocks > 0)
                analysis.Warnings.Add("跳过 Xref/Overlay 块定义；数量=" + analysis.SkippedXrefBlocks.ToString(CultureInfo.InvariantCulture));
            return analysis;
        }

        private static void RecordUnsupported(Analysis analysis, Entity entity, string reason)
        {
            analysis.UnsupportedEntities++;
            var dxfType = SafeString(() => entity.GetRXClass().DxfName);
            var key = string.IsNullOrWhiteSpace(dxfType) ? "UNKNOWN" : dxfType.Trim().ToUpperInvariant();
            analysis.UnsupportedCounts.TryGetValue(key, out var count);
            analysis.UnsupportedCounts[key] = count + 1;
            analysis.UnsupportedItems.Add(new UnsupportedItem
            {
                Handle = SafeString(() => entity.Handle.ToString()),
                DxfType = key,
                reason = string.IsNullOrWhiteSpace(reason) ? "unsupported_by_yangcad-rvt-exchange/1.0" : reason
            });
        }

        private static void ClassifyEntity(Database db, Transaction tr, BlockTableRecord owner, Entity entity, Request request, bool applyLayers, Analysis analysis)
        {
            string category;
            string layerName;
            string signature;
            var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var originalLayer = entity.Layer;
            var appearance = ResolveAppearance(db, tr, entity, analysis);

            if (entity is Line line)
            {
                category = "geometry";
                signature = GeometrySignature(appearance);
                layerName = GeometryLayerName(appearance);
                data["geometry_kind"] = "line";
                data["start"] = Point(line.StartPoint);
                data["end"] = Point(line.EndPoint);
            }
            else if (entity is Polyline polyline)
            {
                category = "geometry";
                signature = GeometrySignature(appearance);
                layerName = GeometryLayerName(appearance);
                data["geometry_kind"] = "polyline";
                data["closed"] = polyline.Closed;
                var vertices = new List<object>();
                for (var i = 0; i < polyline.NumberOfVertices; i++)
                    vertices.Add(new { point = Point2(polyline.GetPoint2dAt(i)), bulge = Round(polyline.GetBulgeAt(i), 8) });
                data["vertices"] = vertices.ToArray();
            }
            else if (entity is Arc arc)
            {
                category = "geometry";
                signature = GeometrySignature(appearance);
                layerName = GeometryLayerName(appearance);
                data["geometry_kind"] = "arc";
                data["center"] = Point(arc.Center);
                data["radius"] = Round(arc.Radius, 8);
                data["start_angle_deg"] = Round(ToDegrees(arc.StartAngle), 6);
                data["end_angle_deg"] = Round(ToDegrees(arc.EndAngle), 6);
            }
            else if (entity is Circle circle)
            {
                category = "geometry";
                signature = GeometrySignature(appearance);
                layerName = GeometryLayerName(appearance);
                data["geometry_kind"] = "circle";
                data["center"] = Point(circle.Center);
                data["radius"] = Round(circle.Radius, 8);
            }
            else if (entity is Ellipse || entity is Spline)
            {
                // V1 official DxfType set is intentionally closed.  Ellipse/Spline were previously
                // classified with extents-only payloads, which made them look rebuildable even though
                // YangTools has no V1 native semantic contract for them.  Fail closed instead.
                RecordUnsupported(analysis, entity, "DxfType is outside yangcad-rvt-exchange/1.0 official V1 type set");
                return;
            }
            else if (entity is DBText dbText)
            {
                category = "text";
                var traits = ExtractDbTextTraits(tr, dbText);
                signature = TextSignature("T", traits);
                layerName = TextLayerName("T", traits);
                AddTextData(data, traits);
                data["position"] = Point(dbText.Position);
                data["rotation_deg"] = Round(ToDegrees(dbText.Rotation), 6);
                data["width"] = 0.0;
            }
            else if (entity is MText mText)
            {
                category = "text";
                var traits = ExtractMTextTraits(tr, mText);
                if (traits.MixedFormat) analysis.MixedMText++;
                signature = TextSignature("T", traits);
                layerName = TextLayerName("T", traits);
                AddTextData(data, traits);
                data["position"] = Point(mText.Location);
                data["rotation_deg"] = Round(ToDegrees(mText.Rotation), 6);
                data["width"] = Round(mText.Width, 8);
            }
            else if (entity is MLeader mLeader)
            {
                var traits = ExtractMLeaderTextTraits(tr, mLeader);
                var leaderLines = GetMLeaderLines(mLeader);
                if (string.IsNullOrWhiteSpace(traits.PlainText))
                {
                    RecordUnsupported(analysis, entity, "MULTILEADER has no reconstructable text content");
                    return;
                }
                if (leaderLines.Length == 0)
                {
                    RecordUnsupported(analysis, entity, "MULTILEADER has no reconstructable leader line");
                    return;
                }
                category = "leader";
                if (traits.MixedFormat) analysis.MixedMText++;
                signature = TextSignature("ML", traits);
                layerName = TextLayerName("ML", traits);
                AddTextData(data, traits);
                data["leader_kind"] = "mleader";
                data["leader_lines"] = leaderLines;
                data["leader_vertex_order"] = "arrow_to_text";
                data["content_type"] = SafeString(() => mLeader.ContentType.ToString());
                data["arrow_size"] = Round(SafeDouble(() => mLeader.ArrowSize), 8);
                data["dogleg_length"] = Round(SafeDouble(() => mLeader.DoglegLength), 8);
                var mLeaderTextLocation = SafePoint(() => mLeader.TextLocation);
                data["position"] = mLeaderTextLocation;
                data["text_location"] = mLeaderTextLocation;
            }
            else if (entity is Leader leader)
            {
                var traits = ExtractLeaderTextTraits(tr, leader);
                var leaderVertices = Enumerable.Range(0, leader.NumVertices).Select(i => (object)Point(leader.VertexAt(i))).ToArray();
                if (string.IsNullOrWhiteSpace(traits.PlainText))
                {
                    RecordUnsupported(analysis, entity, "LEADER has no reconstructable text annotation");
                    return;
                }
                if (leaderVertices.Length < 2)
                {
                    RecordUnsupported(analysis, entity, "LEADER has fewer than two vertices");
                    return;
                }
                category = "leader";
                signature = TextSignature("L", traits);
                layerName = TextLayerName("L", traits);
                AddTextData(data, traits);
                data["leader_kind"] = "leader";
                data["vertices"] = leaderVertices;
                data["leader_lines"] = new object[] { new { points = leaderVertices, vertex_order = "arrow_to_text" } };
                data["leader_vertex_order"] = "arrow_to_text";
                var leaderTextLocation = LeaderAnnotationPosition(tr, leader);
                if (leaderTextLocation.Length > 0) data["position"] = leaderTextLocation;
                data["has_arrow_head"] = leader.HasArrowHead;
                data["is_splined"] = leader.IsSplined;
                data["dimension_style"] = SafeStyleName(tr, leader.DimensionStyle);
            }
            else if (entity is Dimension dimension)
            {
                var kind = DimensionKind(dimension);
                if (!string.Equals(kind, "LINEAR", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(kind, "ALIGNED", StringComparison.OrdinalIgnoreCase))
                {
                    RecordUnsupported(analysis, entity, "RVT Exchange v1 only guarantees linear/aligned dimension semantics");
                    return;
                }
                category = "dimension";
                var style = SafeString(() => dimension.DimensionStyleName);
                var textHeight = SafeDouble(() => dimension.Dimtxt);
                signature = "D|" + kind + "|" + Round(textHeight, 4).ToString("0.####", CultureInfo.InvariantCulture) + "|" + style;
                layerName = SafeLayerName("RVT-D " + kind + " H" + FormatHeight(textHeight) + " " + SanitizeToken(style));
                data["dimension_kind"] = kind.ToLowerInvariant();
                data["dimension_style"] = style;
                data["measurement"] = SafeDouble(() => dimension.Measurement);
                var dimensionText = SafeString(() => dimension.DimensionText);
                data["dimension_text"] = dimensionText;
                data["display_text"] = dimensionText;
                data["revit_rebuild_supported"] = string.Equals(kind, "LINEAR", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "ALIGNED", StringComparison.OrdinalIgnoreCase);
                data["text_height"] = Round(textHeight, 6);
                data["text_position"] = SafePoint(() => dimension.TextPosition);
                data["text_rotation_deg"] = Round(ToDegrees(SafeDouble(() => dimension.TextRotation)), 6);
                data["dim_scale"] = Round(SafeDouble(() => dimension.Dimscale), 6);
                data["arrow_size"] = Round(SafeDouble(() => dimension.Dimasz), 6);
                data["definition_points"] = DimensionDefinitionPoints(dimension);
            }
            else if (entity is Hatch hatch)
            {
                category = "hatch";
                var isGradient = SafeBool(() => hatch.IsGradient);
                if (isGradient) analysis.GradientHatches++;
                var pattern = isGradient ? "GRADIENT" : SafeString(() => hatch.PatternName);
                var scale = isGradient ? 1.0 : SafeDouble(() => hatch.PatternScale, 1.0);
                var angleDeg = isGradient ? ToDegrees(SafeDouble(() => hatch.GradientAngle)) : ToDegrees(SafeDouble(() => hatch.PatternAngle));
                signature = "H|" + pattern + "|" + Round(scale, 4).ToString(CultureInfo.InvariantCulture) + "|" + Round(angleDeg, 1).ToString(CultureInfo.InvariantCulture) + "|" + appearance.ColorName;
                layerName = SafeLayerName("RVT-H " + SanitizeToken(pattern) + " S" + FormatNumber(scale, 4) + " A" + FormatNumber(NormalizeDegrees(angleDeg), 1) + " " + appearance.ColorName);
                data["pattern"] = pattern;
                data["pattern_type"] = isGradient ? "Gradient" : SafeString(() => hatch.PatternType.ToString());
                data["scale"] = Round(scale, 8);
                data["angle_deg"] = Round(NormalizeDegrees(angleDeg), 6);
                data["loop_count"] = SafeInt(() => hatch.NumberOfLoops);
                data["is_gradient"] = isGradient;
                data["boundary_loops"] = GetHatchLoops(hatch);
                AddExtents(entity, data);
            }
            else if (entity is BlockReference blockReference)
            {
                if (SafeBool(() => blockReference.IsDynamicBlock)) analysis.DynamicBlockReferences++;
                // BlockReference instance transforms are not part of yangcad-rvt-exchange/1.0.
                // Definition children are diagnostic-only, so references fail closed instead of reconstructing at wrong coordinates.
                RecordUnsupported(analysis, entity, SafeBool(() => blockReference.IsDynamicBlock)
                    ? "dynamic BlockReference is outside Exchange v1; anonymous display state is not rewritten"
                    : "BlockReference is outside Exchange v1; block definition scanning is diagnostic only");
                return;
            }
            else
            {
                RecordUnsupported(analysis, entity, "DxfType is not supported by yangcad-rvt-exchange/1.0");
                return;
            }

            analysis.ClassifiedEntities++;
            var exchangeCategory = ExchangeCategory(category);
            analysis.CategoryCounts.TryGetValue(exchangeCategory, out var count);
            analysis.CategoryCounts[exchangeCategory] = count + 1;

            layerName = EnsureUniqueLayerName(analysis, layerName, signature);
            var entityTraits = BuildLayerTraits(category, entity, appearance, data);
            if (!analysis.Layers.ContainsKey(layerName))
            {
                analysis.Layers[layerName] = new LayerDefinition
                {
                    LayerName = layerName,
                    Category = category,
                    Signature = signature,
                    Traits = new Dictionary<string, object?>(entityTraits, StringComparer.OrdinalIgnoreCase)
                };
            }

            if (applyLayers)
            {
                var layerId = EnsureLayer(db, tr, analysis.Layers[layerName], appearance);
                entity.LayerId = layerId;
                if (request.NormalizeGeometryOverrides && category == "geometry") NormalizeGeometry(entity);
            }

            // Capture only successfully classified entities. Extents are for a placement origin,
            // never mutate source geometry or rewrite Data coordinates.
            if (string.Equals(owner.Name, BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase))
                analysis.ModelSpaceEntities++;
            else
                analysis.PaperSpaceEntities++;
            try
            {
                var ext = entity.GeometricExtents;
                double minX = ext.MinPoint.X, minY = ext.MinPoint.Y;
                double maxX = ext.MaxPoint.X, maxY = ext.MaxPoint.Y;
                if (!double.IsNaN(minX) && !double.IsNaN(minY) && !double.IsNaN(maxX) && !double.IsNaN(maxY)
                    && !double.IsInfinity(minX) && !double.IsInfinity(minY)
                    && !double.IsInfinity(maxX) && !double.IsInfinity(maxY)
                    && minX <= maxX && minY <= maxY)
                {
                    analysis.HasExtents = true;
                    analysis.MinX = Math.Min(analysis.MinX, minX);
                    analysis.MinY = Math.Min(analysis.MinY, minY);
                    analysis.MaxX = Math.Max(analysis.MaxX, maxX);
                    analysis.MaxY = Math.Max(analysis.MaxY, maxY);
                }
            }
            catch { /* Not every entity provides reliable GeometricExtents. */ }

            analysis.Entities.Add(new EntityRecord
            {
                Handle = entity.Handle.ToString(),
                DxfType = entity.GetRXClass().DxfName,
                Category = ExchangeCategory(category),
                OriginalLayer = originalLayer,
                ExchangeLayer = layerName,
                Owner = owner.IsLayout ? "layout:" + owner.Name : "block:" + owner.Name,
                Traits = entityTraits,
                Data = data
            });
        }

        private static Dictionary<string, object?> BuildLayerTraits(string category, Entity entity, EffectiveAppearance appearance, Dictionary<string, object?> data)
        {
            var traits = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["category"] = ExchangeCategory(category),
                ["Color"] = appearance.ColorName,
                ["source_dxf"] = entity.GetRXClass().DxfName
            };
            if (category == "geometry")
            {
                var lineWeightMm = LineWeightMillimeters(appearance.LineWeight);
                traits["LineWeightMm"] = lineWeightMm;
                traits["LineType"] = appearance.LineTypeName;
                // Compatibility aliases retained for existing readers/reports.
                traits["lineweight_mm"] = lineWeightMm;
                traits["linetype"] = appearance.LineTypeName;
            }
            if (category == "text" || category == "leader")
            {
                foreach (var key in new[] { "text_height", "text_style", "font", "bold", "italic", "underline", "overline", "strike_through", "superscript", "subscript", "stacked", "mixed_format", "oblique_deg", "width_factor" })
                    if (data.TryGetValue(key, out var value)) traits[key] = value;
                if (data.TryGetValue("text_style", out var textStyle)) traits["TextStyle"] = textStyle;
                if (data.TryGetValue("font", out var font)) traits["Font"] = font;
                if (data.TryGetValue("text_height", out var height)) traits["Height"] = height;
            }
            if (category == "dimension")
            {
                foreach (var key in new[] { "dimension_kind", "dimension_style", "text_height" })
                    if (data.TryGetValue(key, out var value)) traits[key] = value;
            }
            if (category == "hatch")
            {
                foreach (var key in new[] { "pattern", "pattern_type", "scale", "angle_deg", "is_gradient" })
                    if (data.TryGetValue(key, out var value)) traits[key] = value;
            }
            return traits;
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, LayerDefinition definition, EffectiveAppearance appearance)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            LayerTableRecord layer;
            ObjectId id;
            if (table.Has(definition.LayerName))
            {
                id = table[definition.LayerName];
                layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                table.UpgradeOpen();
                layer = new LayerTableRecord { Name = definition.LayerName };
                id = table.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
            }

            // The output clone owns exchange-layer semantics. If a source drawing already contains
            // a layer with the generated name, make its properties match that name instead of
            // silently emitting a contradictory RVT exchange layer.
            // Generated exchange layers must be usable even when a source drawing already contains
            // a same-named layer in an off/frozen/locked/non-plot state. This only changes the detached clone.
            layer.IsOff = false;
            layer.IsFrozen = false;
            layer.IsLocked = false;
            layer.IsPlottable = true;
            if (definition.Category == "geometry")
            {
                if (!appearance.LineTypeId.IsNull) layer.LinetypeObjectId = appearance.LineTypeId;
                if ((int)appearance.LineWeight >= 0) layer.LineWeight = appearance.LineWeight;
                layer.Color = CloneColor(appearance.Color);
            }
            else
            {
                layer.Color = CloneColor(appearance.Color);
            }
            return id;
        }

        private static Color CloneColor(Color color)
        {
            try
            {
                if (color.ColorMethod == ColorMethod.ByColor)
                {
                    var v = color.ColorValue;
                    return Color.FromRgb(v.R, v.G, v.B);
                }
                if (color.ColorMethod == ColorMethod.ByAci) return Color.FromColorIndex(ColorMethod.ByAci, color.ColorIndex);
            }
            catch { }
            return Color.FromColorIndex(ColorMethod.ByAci, 7);
        }

        private static void NormalizeGeometry(Entity entity)
        {
            entity.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
            entity.Linetype = "ByLayer";
            entity.LineWeight = LineWeight.ByLayer;
        }

        private static EffectiveAppearance ResolveAppearance(Database db, Transaction tr, Entity entity, Analysis analysis)
        {
            var layer = (LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead);
            var result = new EffectiveAppearance();

            var lw = entity.LineWeight;
            if (lw == LineWeight.ByLayer) lw = layer.LineWeight;
            if (lw == LineWeight.ByBlock)
            {
                result.HasByBlockFallback = true;
                analysis.UnresolvedByBlock.Add(entity.Handle.ToString());
                lw = layer.LineWeight;
            }
            if (lw == LineWeight.ByLayer || lw == LineWeight.ByBlock || lw == LineWeight.ByLineWeightDefault) lw = LineWeight.LineWeight025;
            result.LineWeight = lw;

            var ltName = entity.Linetype;
            ObjectId ltId = entity.LinetypeId;
            if (string.Equals(ltName, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                ltId = layer.LinetypeObjectId;
                var rec = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);
                ltName = rec.Name;
            }
            else if (string.Equals(ltName, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                result.HasByBlockFallback = true;
                analysis.UnresolvedByBlock.Add(entity.Handle.ToString());
                ltId = layer.LinetypeObjectId;
                var rec = (LinetypeTableRecord)tr.GetObject(ltId, OpenMode.ForRead);
                ltName = rec.Name;
            }
            result.LineTypeName = string.IsNullOrWhiteSpace(ltName) ? "Continuous" : ltName;
            result.LineTypeId = ltId;

            var color = entity.Color;
            if (color.ColorMethod == ColorMethod.ByLayer)
                color = layer.Color;
            else if (color.ColorMethod == ColorMethod.ByBlock)
            {
                result.HasByBlockFallback = true;
                analysis.UnresolvedByBlock.Add(entity.Handle.ToString());
                color = layer.Color;
            }
            result.Color = CloneColor(color);
            result.ColorName = ColorName(color);
            return result;
        }

        private static string GeometrySignature(EffectiveAppearance a)
            => "G|" + FormatLineWeight(a.LineWeight) + "|" + a.LineTypeName + "|" + a.ColorName;

        private static string GeometryLayerName(EffectiveAppearance a)
            => SafeLayerName("#" + FormatLineWeight(a.LineWeight) + " " + SanitizeToken(a.LineTypeName) + " " + a.ColorName);

        private static string TextSignature(string prefix, TextTraits t)
            => prefix + "|" + Round(t.Height, 4).ToString(CultureInfo.InvariantCulture) + "|" + t.FontName + "|" + TextFlags(t);

        private static string TextLayerName(string prefix, TextTraits t)
            => SafeLayerName("RVT-" + prefix + " H" + FormatHeight(t.Height) + " " + SanitizeToken(t.FontName) + TextFlagsSuffix(t));

        private static string TextFlags(TextTraits t)
            => string.Join(",", new[] {
                t.Bold ? "B" : "", t.Italic ? "I" : "", t.Underline ? "U" : "", t.Overline ? "O" : "",
                t.StrikeThrough ? "K" : "", t.Superscript ? "SUP" : "", t.Subscript ? "SUB" : "", t.Stacked ? "STACK" : "", t.MixedFormat ? "MIXED" : ""
            }.Where(x => x.Length > 0));

        private static string TextFlagsSuffix(TextTraits t)
        {
            var flags = TextFlags(t);
            return flags.Length == 0 ? "" : " " + flags.Replace(',', '-');
        }

        private static TextTraits ExtractDbTextTraits(Transaction tr, DBText text)
        {
            var traits = ExtractTextStyle(tr, text.TextStyleId);
            traits.Height = text.Height;
            traits.ObliqueDegrees = ToDegrees(text.Oblique);
            traits.WidthFactor = text.WidthFactor;
            traits.PlainText = text.TextString ?? "";
            traits.RawText = text.TextString ?? "";
            traits.Underline = ContainsIgnoreCase(text.TextString, "%%U");
            traits.Superscript = HasSuperscript(text.TextString);
            traits.Subscript = HasSubscript(text.TextString);
            return traits;
        }

        private static TextTraits ExtractMTextTraits(Transaction tr, MText text)
        {
            var traits = ExtractTextStyle(tr, text.TextStyleId);
            traits.Height = text.TextHeight;
            traits.PlainText = SafeString(() => text.Text);
            traits.RawText = text.Contents ?? "";
            ApplyMTextInlineFlags(traits, traits.RawText);
            return traits;
        }

        private static TextTraits ExtractMLeaderTextTraits(Transaction tr, MLeader leader)
        {
            try
            {
                var mText = leader.MText;
                if (mText != null)
                {
                    var traits = ExtractMTextTraits(tr, mText);
                    if (leader.TextHeight > 0) traits.Height = leader.TextHeight;
                    return traits;
                }
            }
            catch { }
            var fallback = ExtractTextStyle(tr, leader.TextStyleId);
            fallback.Height = SafeDouble(() => leader.TextHeight);
            return fallback;
        }

        private static TextTraits ExtractLeaderTextTraits(Transaction tr, Leader leader)
        {
            try
            {
                if (!leader.Annotation.IsNull)
                {
                    var obj = tr.GetObject(leader.Annotation, OpenMode.ForRead, false);
                    if (obj is MText mt) return ExtractMTextTraits(tr, mt);
                    if (obj is DBText dt) return ExtractDbTextTraits(tr, dt);
                }
            }
            catch { }
            return new TextTraits { Height = SafeDouble(() => leader.Dimtxt), FontName = "UNKNOWN", StyleName = SafeStyleName(tr, leader.DimensionStyle) };
        }

        private static TextTraits ExtractTextStyle(Transaction tr, ObjectId styleId)
        {
            var traits = new TextTraits { FontName = "UNKNOWN" };
            try
            {
                var style = (TextStyleTableRecord)tr.GetObject(styleId, OpenMode.ForRead);
                traits.StyleName = style.Name;
                traits.ObliqueDegrees = ToDegrees(style.ObliquingAngle);
                traits.WidthFactor = style.XScale;
                try
                {
                    var font = style.Font;
                    traits.FontName = string.IsNullOrWhiteSpace(font.TypeFace) ? (string.IsNullOrWhiteSpace(style.FileName) ? style.Name : style.FileName) : font.TypeFace;
                    traits.Bold = font.Bold;
                    traits.Italic = font.Italic;
                }
                catch
                {
                    traits.FontName = string.IsNullOrWhiteSpace(style.FileName) ? style.Name : style.FileName;
                }
            }
            catch { }
            return traits;
        }

        private static void ApplyMTextInlineFlags(TextTraits traits, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            traits.Underline |= Regex.IsMatch(raw, @"\\L", RegexOptions.IgnoreCase);
            traits.Overline |= Regex.IsMatch(raw, @"\\O", RegexOptions.IgnoreCase);
            traits.StrikeThrough |= Regex.IsMatch(raw, @"\\K", RegexOptions.IgnoreCase);
            traits.Bold |= Regex.IsMatch(raw, @"\|b1", RegexOptions.IgnoreCase);
            traits.Italic |= Regex.IsMatch(raw, @"\|i1", RegexOptions.IgnoreCase);
            traits.Stacked |= Regex.IsMatch(raw, @"\\S[^;]+;", RegexOptions.IgnoreCase);
            traits.Superscript |= HasSuperscript(raw) || Regex.IsMatch(raw, @"\\S[^;]*\^[^;]*;", RegexOptions.IgnoreCase);
            traits.Subscript |= HasSubscript(raw) || Regex.IsMatch(raw, @"\\S[^;]*[/#][^;]*;", RegexOptions.IgnoreCase);
            var inlineFonts = Regex.Matches(raw, @"\\[fF]([^;|]+)(?:\|[^;]*)?;", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            traits.InlineFonts = inlineFonts;
            if (inlineFonts.Any(x => !string.Equals(x, traits.FontName, StringComparison.OrdinalIgnoreCase)))
            {
                traits.MixedFormat = true;
                traits.FontName = "MIXEDFONT";
            }
            traits.MixedFormat |= Regex.IsMatch(raw, @"\\[fFHCcWwQqTtLlOoKkSs]|[{}]");
        }

        private static void AddTextData(Dictionary<string, object?> data, TextTraits t)
        {
            data["text"] = t.PlainText;
            data["raw_text"] = t.RawText;
            data["inline_fonts"] = t.InlineFonts;
            data["text_height"] = Round(t.Height, 8);
            data["text_style"] = t.StyleName;
            data["font"] = t.FontName;
            data["bold"] = t.Bold;
            data["italic"] = t.Italic;
            data["underline"] = t.Underline;
            data["overline"] = t.Overline;
            data["strike_through"] = t.StrikeThrough;
            data["superscript"] = t.Superscript;
            data["subscript"] = t.Subscript;
            data["stacked"] = t.Stacked;
            data["mixed_format"] = t.MixedFormat;
            data["oblique_deg"] = Round(t.ObliqueDegrees, 6);
            data["width_factor"] = Round(t.WidthFactor, 6);
        }

        private static object[] LeaderAnnotationPosition(Transaction tr, Leader leader)
        {
            try
            {
                if (leader.Annotation.IsNull) return Array.Empty<object>();
                var obj = tr.GetObject(leader.Annotation, OpenMode.ForRead, false);
                if (obj is MText mt) return Point(mt.Location);
                if (obj is DBText dt) return Point(dt.Position);
            }
            catch { }
            return Array.Empty<object>();
        }

        private static object[] GetMLeaderLines(MLeader leader)
        {
            var lines = new List<object>();
            try
            {
                foreach (var leaderIndexObj in leader.GetLeaderIndexes())
                {
                    var leaderIndex = Convert.ToInt32(leaderIndexObj, CultureInfo.InvariantCulture);
                    foreach (var lineIndexObj in leader.GetLeaderLineIndexes(leaderIndex))
                    {
                        var lineIndex = Convert.ToInt32(lineIndexObj, CultureInfo.InvariantCulture);
                        var vertices = new List<object>();
                        var count = leader.VerticesCount(lineIndex);
                        for (var i = 0; i < count; i++) vertices.Add(Point(leader.GetVertex(lineIndex, i)));
                        // AutoCAD defines the first MLeader vertex as the leader head (arrow) and the last as the tail (text/landing).
                        lines.Add(new { leader_index = leaderIndex, line_index = lineIndex, vertex_order = "arrow_to_text", points = vertices.ToArray() });
                    }
                }
            }
            catch { }
            return lines.ToArray();
        }

        private static object[] DimensionDefinitionPoints(Dimension dimension)
        {
            // YangTools V1 rebuilds linear/aligned dimensions from these three stable semantic roles.
            // Other dimension kinds retain the previous additive reflection payload for diagnostics/future support.
            if (dimension is RotatedDimension rotated)
            {
                return new object[]
                {
                    new { role = "DimLinePoint", point = Point(rotated.DimLinePoint) },
                    new { role = "XLine1Point", point = Point(rotated.XLine1Point) },
                    new { role = "XLine2Point", point = Point(rotated.XLine2Point) }
                };
            }
            if (dimension is AlignedDimension aligned)
            {
                return new object[]
                {
                    new { role = "DimLinePoint", point = Point(aligned.DimLinePoint) },
                    new { role = "XLine1Point", point = Point(aligned.XLine1Point) },
                    new { role = "XLine2Point", point = Point(aligned.XLine2Point) }
                };
            }

            var result = new List<object>();
            foreach (var property in dimension.GetType().GetProperties())
            {
                try
                {
                    if (!property.CanRead || property.PropertyType != typeof(Point3d)) continue;
                    if (string.Equals(property.Name, "TextPosition", StringComparison.OrdinalIgnoreCase)) continue;
                    var value = (Point3d)property.GetValue(dimension, null)!;
                    result.Add(new { role = property.Name, point = Point(value) });
                }
                catch { }
            }
            return result.OrderBy(x => Convert.ToString(x.GetType().GetProperty("role")?.GetValue(x, null), CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string DimensionKind(Dimension dimension)
        {
            var n = dimension.GetType().Name;
            if (n.IndexOf("Aligned", StringComparison.OrdinalIgnoreCase) >= 0) return "ALIGNED";
            if (n.IndexOf("Rotated", StringComparison.OrdinalIgnoreCase) >= 0) return "LINEAR";
            if (n.IndexOf("Angular", StringComparison.OrdinalIgnoreCase) >= 0) return "ANGULAR";
            if (n.IndexOf("Diametric", StringComparison.OrdinalIgnoreCase) >= 0) return "DIAMETER";
            if (n.IndexOf("Radial", StringComparison.OrdinalIgnoreCase) >= 0) return "RADIUS";
            if (n.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) >= 0) return "ARC";
            if (n.IndexOf("Ordinate", StringComparison.OrdinalIgnoreCase) >= 0) return "ORDINATE";
            return n.ToUpperInvariant();
        }

        private static object[] GetHatchLoops(Hatch hatch)
        {
            var loops = new List<object>();
            var count = SafeInt(() => hatch.NumberOfLoops);
            for (var i = 0; i < count; i++)
            {
                try
                {
                    var loop = hatch.GetLoopAt(i);
                    var isOuter = (loop.LoopType & HatchLoopTypes.External) == HatchLoopTypes.External ||
                                  (loop.LoopType & HatchLoopTypes.Outermost) == HatchLoopTypes.Outermost;
                    if (loop.IsPolyline)
                    {
                        var vertices = new List<object>();
                        foreach (BulgeVertex vertex in loop.Polyline)
                            vertices.Add(new { point = Point2(vertex.Vertex), bulge = Round(vertex.Bulge, 8) });
                        loops.Add(new { index = i, loop_type = loop.LoopType.ToString(), is_outer = isOuter, is_hole = !isOuter, kind = "polyline", vertices = vertices.ToArray() });
                    }
                    else
                    {
                        var curves = new List<object>();
                        foreach (Curve2d curve in loop.Curves) curves.Add(SerializeCurve2d(curve));
                        loops.Add(new { index = i, loop_type = loop.LoopType.ToString(), is_outer = isOuter, is_hole = !isOuter, kind = "curves", curves = curves.ToArray() });
                    }
                }
                catch (Exception ex)
                {
                    loops.Add(new { index = i, kind = "unreadable", error = ex.GetType().Name });
                }
            }
            return loops.ToArray();
        }

        private static object SerializeCurve2d(Curve2d curve)
        {
            if (curve is LineSegment2d line)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Type"] = "LINE",
                    ["StartPoint"] = Point2(line.StartPoint),
                    ["EndPoint"] = Point2(line.EndPoint)
                };
            }
            if (curve is CircularArc2d arc)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Type"] = "ARC",
                    ["StartPoint"] = Point2(arc.StartPoint),
                    ["EndPoint"] = Point2(arc.EndPoint),
                    ["Center"] = Point2(arc.Center),
                    ["Radius"] = Round(arc.Radius, 8),
                    ["StartAngleDeg"] = Round(ToDegrees(arc.StartAngle), 6),
                    ["EndAngleDeg"] = Round(ToDegrees(arc.EndAngle), 6),
                    ["Clockwise"] = arc.IsClockWise
                };
            }

            // Keep a generic additive fallback for less common Curve2d types.
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Type"] = curve.GetType().Name.ToUpperInvariant(),
                ["curve_type"] = curve.GetType().Name
            };
            foreach (var property in curve.GetType().GetProperties())
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                try
                {
                    var value = property.GetValue(curve, null);
                    if (value is Point2d p2) values[property.Name] = Point2(p2);
                    else if (value is Vector2d v2) values[property.Name] = new object[] { Round(v2.X, 8), Round(v2.Y, 8) };
                    else if (value is double d) values[property.Name] = Round(d, 8);
                    else if (value is int || value is bool || value is string || value is Enum) values[property.Name] = value?.ToString();
                }
                catch { }
            }
            return values;
        }

        private static void AddExtents(Entity entity, Dictionary<string, object?> data)
        {
            try
            {
                var ext = entity.GeometricExtents;
                data["extents_min"] = Point(ext.MinPoint);
                data["extents_max"] = Point(ext.MaxPoint);
            }
            catch { }
        }

        private static string EnsureUniqueLayerName(Analysis analysis, string requested, string signature)
        {
            requested = SafeLayerName(requested);
            if (!analysis.Layers.TryGetValue(requested, out var existing) || string.Equals(existing.Signature, signature, StringComparison.Ordinal)) return requested;
            var hash = ShortHash(signature);
            var maxBase = Math.Max(1, LayerNameLimit - hash.Length - 1);
            var prefix = requested.Length <= maxBase ? requested : requested.Substring(0, maxBase);
            return prefix + "-" + hash;
        }

        private static string SafeLayerName(string value)
        {
            var text = SanitizeToken(value, preserveSpaces: true);
            if (text.Length > LayerNameLimit) text = text.Substring(0, LayerNameLimit - 9) + "-" + ShortHash(value);
            return string.IsNullOrWhiteSpace(text) ? "RVT-UNCLASSIFIED" : text;
        }

        private static string SanitizeToken(string value, bool preserveSpaces = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
            var invalid = new HashSet<char>(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=', ',' });
            var sb = new StringBuilder(value.Length);
            foreach (var c in value.Trim())
            {
                if (invalid.Contains(c) || char.IsControl(c)) sb.Append('-');
                else if (!preserveSpaces && char.IsWhiteSpace(c)) sb.Append('_');
                else sb.Append(c);
            }
            var result = Regex.Replace(sb.ToString(), preserveSpaces ? @"\s+" : @"_+", preserveSpaces ? " " : "_");
            return result.Trim(' ', '-', '_');
        }

        private static string SanitizeFilePart(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            return new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        }

        private static string ShortHash(string value)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                return BitConverter.ToString(bytes, 0, 4).Replace("-", "");
            }
        }

        private static string FormatLineWeight(LineWeight lineWeight)
        {
            var raw = (int)lineWeight;
            if (raw < 0) raw = 25;
            return (raw / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static double LineWeightMillimeters(LineWeight lineWeight)
        {
            var raw = (int)lineWeight;
            if (raw < 0) raw = 25;
            return raw / 100.0;
        }

        private static string FormatHeight(double value) => FormatNumber(value, 4);
        private static string FormatNumber(double value, int decimals)
        {
            var format = "0." + new string('#', Math.Max(0, decimals));
            return Round(value, decimals).ToString(format, CultureInfo.InvariantCulture);
        }

        private static string ColorName(Color color)
        {
            try
            {
                if (color.ColorMethod == ColorMethod.ByAci)
                {
                    switch (color.ColorIndex)
                    {
                        case 1: return "Red";
                        case 2: return "Yellow";
                        case 3: return "Green";
                        case 4: return "Cyan";
                        case 5: return "Blue";
                        case 6: return "Magenta";
                        case 7: return "White";
                        default: return "ACI" + color.ColorIndex.ToString(CultureInfo.InvariantCulture);
                    }
                }
                var v = color.ColorValue;
                return "RGB-" + v.R + "-" + v.G + "-" + v.B;
            }
            catch { return "White"; }
        }

        private static string ExchangeCategory(string internalCategory)
        {
            switch (internalCategory)
            {
                case "geometry": return "Curve";
                case "text":
                case "leader": return "Text";
                case "hatch": return "Fill";
                case "dimension": return "Dimension";
                default: return internalCategory;
            }
        }

        private static object[] Point(Point3d p) => new object[] { Round(p.X, 8), Round(p.Y, 8), Round(p.Z, 8) };
        private static object[] Point2(Point2d p) => new object[] { Round(p.X, 8), Round(p.Y, 8), 0.0 };
        private static double Round(double value, int digits) => double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : Math.Round(value, digits, MidpointRounding.AwayFromZero);
        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
        private static double NormalizeDegrees(double degrees)
        {
            degrees %= 360.0;
            if (degrees < 0) degrees += 360.0;
            return degrees;
        }

        private static bool ContainsIgnoreCase(string? source, string value)
            => !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool HasSuperscript(string? text)
            => !string.IsNullOrEmpty(text) && text.Any(c => "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿᴬᴮᴰᴱᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾᴿᵀᵁⱽᵂ".IndexOf(c) >= 0);

        private static bool HasSubscript(string? text)
            => !string.IsNullOrEmpty(text) && text.Any(c => "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓ".IndexOf(c) >= 0);

        private static object[] SafePoint(Func<Point3d> getter)
        {
            try { return Point(getter()); }
            catch { return Array.Empty<object>(); }
        }

        private static string SafeStyleName(Transaction tr, ObjectId styleId)
        {
            try
            {
                if (styleId.IsNull) return "";
                var record = tr.GetObject(styleId, OpenMode.ForRead, false) as SymbolTableRecord;
                return record?.Name ?? "";
            }
            catch { return ""; }
        }

        private static string SafeString(Func<string> getter)
        {
            try { return getter() ?? ""; }
            catch { return ""; }
        }
        private static double SafeDouble(Func<double> getter, double fallback = 0)
        {
            try { return getter(); }
            catch { return fallback; }
        }
        private static int SafeInt(Func<int> getter, int fallback = 0)
        {
            try { return getter(); }
            catch { return fallback; }
        }
        private static bool SafeBool(Func<bool> getter, bool fallback = false)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static string ExchangeUnits(UnitsValue units, Analysis analysis)
        {
            switch (units)
            {
                case UnitsValue.Millimeters: return "mm";
                case UnitsValue.Centimeters: return "cm";
                case UnitsValue.Meters: return "m";
                case UnitsValue.Inches: return "in";
                case UnitsValue.Feet: return "ft";
                case UnitsValue.Undefined:
                    analysis.Warnings.Add("INSUNITS 未指定；RVT Exchange v1 按常用中国施工图约定回退为 mm。请在导出摘要中复核单位。");
                    return "mm";
                default:
                    throw new NotSupportedException("当前 CAD INSUNITS=" + units + " 不在 YangTools 3.1.36 V1 支持范围。请先将 INSUNITS 设为 mm/cm/m/in/ft 后重新导出，禁止静默按 mm 转换。");
            }
        }

        private static void WriteMap(string path, string documentName, string outputDwg, Database db, Analysis analysis)
        {
            var units = ExchangeUnits(db.Insunits, analysis);
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.Write("{");
                writer.Write("\"schema_version\":"); writer.Write(JsonCodec.Serialize(SchemaVersion));
                writer.Write(",\"units\":"); writer.Write(JsonCodec.Serialize(units));
                writer.Write(",\"origin\":"); writer.Write(JsonCodec.Serialize(analysis.Origin));
                writer.Write(",\"coordinate_origin_mode\":"); writer.Write(JsonCodec.Serialize(analysis.OriginMode));
                writer.Write(",\"model_space_entities\":"); writer.Write(analysis.ModelSpaceEntities.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"paper_space_entities\":"); writer.Write(analysis.PaperSpaceEntities.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"mixed_spaces\":"); writer.Write(analysis.ModelSpaceEntities > 0 && analysis.PaperSpaceEntities > 0 ? "true" : "false");
                writer.Write(",\"source_insunits\":"); writer.Write(JsonCodec.Serialize(db.Insunits.ToString()));
                writer.Write(",\"generated_utc\":"); writer.Write(JsonCodec.Serialize(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                writer.Write(",\"source_document\":"); writer.Write(JsonCodec.Serialize(documentName));
                writer.Write(",\"output_dwg\":"); writer.Write(JsonCodec.Serialize(outputDwg));
                writer.Write(",\"layers\":[");
                var first = true;
                foreach (var layer in analysis.Layers.Values.OrderBy(x => x.LayerName, StringComparer.OrdinalIgnoreCase))
                {
                    if (!first) writer.Write(','); first = false;
                    writer.Write(JsonCodec.Serialize(layer));
                }
                writer.Write("],\"entities\":[");
                first = true;
                foreach (var entity in analysis.Entities)
                {
                    if (!first) writer.Write(','); first = false;
                    writer.Write(JsonCodec.Serialize(entity));
                }
                writer.Write("],\"counts\":"); writer.Write(JsonCodec.Serialize(analysis.Entities.GroupBy(x => x.DxfType, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase)));
                writer.Write(",\"unsupported_count\":"); writer.Write(analysis.UnsupportedEntities.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"unsupported\":"); writer.Write(JsonCodec.Serialize(analysis.UnsupportedCounts));
                writer.Write(",\"unsupported_items\":"); writer.Write(JsonCodec.Serialize(analysis.UnsupportedItems));
                writer.Write(",\"warnings\":"); writer.Write(JsonCodec.Serialize(analysis.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
                writer.Write("}");
            }
        }

        private static void WriteReport(string path, string documentName, string outputDwg, Database db, Analysis analysis)
        {
            var report = new
            {
                schema_version = SchemaVersion,
                units = ExchangeUnits(db.Insunits, analysis),
                origin = analysis.Origin,
                coordinate_origin_mode = analysis.OriginMode,
                model_space_entities = analysis.ModelSpaceEntities,
                paper_space_entities = analysis.PaperSpaceEntities,
                mixed_spaces = analysis.ModelSpaceEntities > 0 && analysis.PaperSpaceEntities > 0,
                source_insunits = db.Insunits.ToString(),
                generated_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                source_document = documentName,
                output_dwg = outputDwg,
                scanned_entities = analysis.ScannedEntities,
                classified_entities = analysis.ClassifiedEntities,
                unsupported_entities = analysis.UnsupportedEntities,
                unsupported_counts = analysis.UnsupportedCounts,
                unsupported_items = analysis.UnsupportedItems,
                generated_layers = analysis.Layers.Count,
                category_counts = analysis.CategoryCounts,
                dxf_type_counts = analysis.Entities.GroupBy(x => x.DxfType, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
                mixed_mtext = analysis.MixedMText,
                gradient_hatches = analysis.GradientHatches,
                normal_block_definitions_seen = analysis.NormalBlockDefinitions,
                block_definition_entities_skipped = analysis.SkippedBlockDefinitionEntities,
                anonymous_blocks_skipped = analysis.SkippedAnonymousBlocks,
                dynamic_block_references_detected = analysis.DynamicBlockReferences,
                xref_blocks_skipped = analysis.SkippedXrefBlocks,
                unresolved_byblock = analysis.UnresolvedByBlock.Count,
                warnings = analysis.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            File.WriteAllText(path, JsonCodec.Serialize(report, indented: true), new UTF8Encoding(false));
        }
    }
}
