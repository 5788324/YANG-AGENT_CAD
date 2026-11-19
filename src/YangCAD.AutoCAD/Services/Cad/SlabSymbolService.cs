using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class SlabSymbolService
    {
        private const string SymbolLayerName = "SLB_SYMBOL";
        private static readonly string[] KnownSymbolBlocks =
            { "SLB_ONE", "SLB_TWO", "YB5_ARM_X", "YB5_ARM_Y", "YB5_ARM_B", "YB4_ARM" };

        private const string DirectionalBlockPrefix = "SLB_DIR_";
        private const double SideTolerance = 1.0;
        private static readonly ConditionalWeakTable<Database, SlabLayers> DrawingLayers =
            new ConditionalWeakTable<Database, SlabLayers>();

        private sealed class SlabLayers
        {
            public List<string> Beams = new List<string>();
            public string? Outline;
        }
        private static string _lastWhy = "未找到封闭板格";

        private readonly struct Seg2D
        {
            public readonly double X1, Y1, X2, Y2, MinX, MinY, MaxX, MaxY, Len;
            public Seg2D(double x1, double y1, double x2, double y2, double len)
            {
                X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
                MinX = Math.Min(x1, x2); MaxX = Math.Max(x1, x2);
                MinY = Math.Min(y1, y2); MaxY = Math.Max(y1, y2);
                Len = len;
            }
        }

        private sealed class SlabResult
        {
            public Point2d Center;
            public List<Point2d> Polygon = new List<Point2d>();
            public string Type = "TWO";
            public double Rotation;
            public double Size;
            public double Ratio;
            public double Span1;
            public double Span2;
            public int DirectionMask;
            public double? Elevation;
            public bool HasNoSupport => Elevation.HasValue && DirectionMask == 0;
            public string BlockName => DirectionMask != 0 ? DirectionalBlockPrefix + DirectionMask.ToString("D2")
                : Type == "ONE" ? "SLB_ONE" : "SLB_TWO";

            public string Describe()
            {
                var label = Elevation.HasValue
                    ? $"识别到 {Enumerable.Range(0, 4).Count(i => (DirectionMask & (1 << i)) != 0)} 侧支承"
                    : Type == "ONE" ? "单向板" : "双向板";
                return $"{label} 长短比={Ratio:0.00} 板格={Span1:0}x{Span2:0} 旋转={Rotation * 180.0 / Math.PI:0.0}°";
            }
        }

        public static void RunSlabLayerSelect(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,ARC") });
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n选择梁线样本以指定支承图层 [回车：使用旧板格识别]："
            };
            var sel = ed.GetSelection(options, filter);
            if (sel.Status == PromptStatus.Cancel) return;
            if (sel.Status != PromptStatus.OK || sel.Value == null || sel.Value.Count == 0)
            {
                DrawingLayers.Remove(db);
                ed.WriteMessage("\n[SLBLAYER] 将使用所有非符号图层的线识别封闭板格。");
                return;
            }

            var layers = new SlabLayers();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in sel.Value.GetObjectIds())
                {
                    var layer = ((Entity)tr.GetObject(id, OpenMode.ForRead)).Layer;
                    if (!layers.Beams.Contains(layer, StringComparer.OrdinalIgnoreCase)) layers.Beams.Add(layer);
                }
            }

            var outlineOptions = new PromptEntityOptions("\n选择闭合矩形板轮廓样本 [回车：仅用梁线识别封闭板格]：")
            {
                AllowNone = true
            };
            outlineOptions.SetRejectMessage("\n请选择闭合矩形多段线。");
            outlineOptions.AddAllowedClass(typeof(Polyline), true);
            var outline = ed.GetEntity(outlineOptions);
            if (outline.Status == PromptStatus.Cancel) return;
            if (outline.Status == PromptStatus.OK)
            {
                using var tr = db.TransactionManager.StartTransaction();
                var pl = (Polyline)tr.GetObject(outline.ObjectId, OpenMode.ForRead);
                if (GetRectangle(pl) == null || layers.Beams.Contains(pl.Layer, StringComparer.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n[SLBLAYER] 板轮廓必须是闭合矩形，且位于独立于梁线的图层。");
                    return;
                }
                layers.Outline = pl.Layer;
            }

            DrawingLayers.Remove(db);
            DrawingLayers.Add(db, layers);
            ed.WriteMessage($"\n[SLBLAYER] 梁线图层：{string.Join(", ", layers.Beams)}；板轮廓：{layers.Outline ?? "仅用梁线识别封闭板格"}。");
        }

        public static void RunSlabSettings(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            settings.SlabExtend = AskDouble(ed, "线端延伸距离(闭合梁线小缺口)", settings.SlabExtend, 0, 5000);
            settings.SlabOneWayRatio = AskDouble(ed, "单向板阈值 长/短 >=", settings.SlabOneWayRatio, 1.0, 20.0);
            settings.SlabFactor = AskDouble(ed, "符号长度系数(短跨×)", settings.SlabFactor, 0.05, 1.0);
            settings.SlabMinSize = AskDouble(ed, "符号最小长度", settings.SlabMinSize, 10, 50000);
            settings.SlabMaxSize = AskDouble(ed, "符号最大长度", settings.SlabMaxSize, settings.SlabMinSize, 50000);
            settings.SlabMinSpan = AskDouble(ed, "有效板格最小边长", settings.SlabMinSpan, 10, 50000);
            settings.SlabMaxSpan = AskDouble(ed, "有效板格最大边长", settings.SlabMaxSpan, settings.SlabMinSpan, 100000);
            settings.Save();
            ed.WriteMessage("\n[SLBSET] 板符号参数已保存。");
        }

        public static void RunSlabPlace(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            int n = 0, unsupported = 0, bad = 0;

            ed.WriteMessage($"\n[SLB] 点击板格内部放置受力符号 | 延伸={settings.SlabExtend:0} 单向板比>={settings.SlabOneWayRatio:0.#} 系数={settings.SlabFactor:0.##}");

            while (true)
            {
                var ppo = new PromptPointOptions("\n点击板格内部 [选梁线图层(L)/改参数(S)/仅预览(P)] <退出>：")
                {
                    AllowNone = true
                };
                ppo.Keywords.Add("L");
                ppo.Keywords.Add("S");
                ppo.Keywords.Add("P");

                var res = ed.GetPoint(ppo);
                if (res.Status == PromptStatus.Cancel || res.Status == PromptStatus.None) break;
                if (res.Status == PromptStatus.Keyword)
                {
                    var key = res.StringResult.ToUpperInvariant();
                    if (key == "L") RunSlabLayerSelect(doc);
                    else if (key == "S")
                    {
                        RunSlabSettings(doc);
                        settings = UserSettings.Load();
                    }
                    else if (key == "P") RunSlabPreview(doc);
                    continue;
                }

                if (res.Status != PromptStatus.OK) continue;
                var wcs = res.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                var analysis = AnalyzePoint(doc.Database, new Point2d(wcs.X, wcs.Y), settings, null, wcs.Z);
                if (analysis == null)
                {
                    bad++;
                    ed.WriteMessage($"\n识别失败: {_lastWhy}");
                    continue;
                }

                if (analysis.HasNoSupport)
                {
                    unsupported++;
                    ed.WriteMessage("\n该板四侧均无符合条件的梁线，未插入受力符号。");
                    continue;
                }

                var action = PlaceOrUpdateSymbol(doc.Database, analysis);
                if (action != null)
                {
                    n++;
                    ed.WriteMessage($"\n{action}: {analysis.Describe()}");
                }
                else
                {
                    bad++;
                    ed.WriteMessage($"\n未布置：{_lastWhy}");
                }
            }

            ed.WriteMessage($"\n[SLB] 完成: 处理 {n} 个, 无支承 {unsupported} 个, 失败 {bad} 个。");
        }

        public static void RunSlabPreview(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            var pr = ed.GetPoint("\n点击板格内部预览识别结果(绿框=板格, 红线=符号)：");
            if (pr.Status != PromptStatus.OK) return;

            var wcs = pr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            var analysis = AnalyzePoint(doc.Database, new Point2d(wcs.X, wcs.Y), settings, null, wcs.Z);
            if (analysis == null)
            {
                ed.WriteMessage($"\n预览失败: {_lastWhy}");
                return;
            }

            var wcsToUcs = ed.CurrentUserCoordinateSystem.Inverse();
            var pts = analysis.Polygon;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                ed.DrawVector(
                    new Point3d(a.X, a.Y, analysis.Elevation ?? 0).TransformBy(wcsToUcs),
                    new Point3d(b.X, b.Y, analysis.Elevation ?? 0).TransformBy(wcsToUcs),
                    3, false);
            }

            var z = analysis.Elevation ?? 0.0;
            var cos = Math.Cos(analysis.Rotation);
            var sin = Math.Sin(analysis.Rotation);
            Point3d ToUcs(double x, double y) => new Point3d(
                analysis.Center.X + analysis.Size * (x * cos - y * sin),
                analysis.Center.Y + analysis.Size * (x * sin + y * cos), z).TransformBy(wcsToUcs);

            foreach (var (x1, y1, x2, y2) in SymbolLines(analysis))
                ed.DrawVector(ToUcs(x1, y1), ToUcs(x2, y2), 1, false);

            ed.WriteMessage($"\n预览: {analysis.Describe()}");
            ed.GetString("\n检查后按回车清除预览：");
            ed.Regen();
        }

        public static void RunSlabFix(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "INSERT") });
            var sel = ed.SelectImplied();
            ObjectId[] ids;
            if (sel.Status == PromptStatus.OK && sel.Value != null && sel.Value.Count > 0)
            {
                ids = sel.Value.GetObjectIds();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
            }
            else
            {
                var opts = new PromptSelectionOptions { MessageForAdding = "\n选择需要修正的已放置板受力符号(块引用)：" };
                var sr = ed.GetSelection(opts, filter);
                if (sr.Status != PromptStatus.OK || sr.Value == null) return;
                ids = sr.Value.GetObjectIds();
            }

            var pko = new PromptKeywordOptions("\n保持符号原有的单向/双向类型? [是(Y)/否(N,按长短比重判)] <是>：", "Y N")
            {
                AllowNone = true
            };
            pko.Keywords.Default = "Y";
            var kr = ed.GetKeywords(pko);
            if (kr.Status == PromptStatus.Cancel) return;
            var keepOld = !string.Equals(kr.StringResult, "N", StringComparison.OrdinalIgnoreCase);

            int fixedCount = 0, removedUnsupported = 0, dup = 0, skipped = 0, bad = 0;
            var doneCenters = new List<Point3d>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is BlockReference br) || br.OwnerId != db.CurrentSpaceId)
                    {
                        skipped++;
                        continue;
                    }

                    var blockName = GetBlockName(tr, br);
                    if (!IsSymbolBlock(blockName))
                    {
                        skipped++;
                        continue;
                    }

                    var pt = new Point2d(br.Position.X, br.Position.Y);
                    var forcedType = keepOld ? InferOldType(blockName) : null;
                    var analysis = AnalyzePoint(db, pt, settings, forcedType, br.Position.Z);
                    if (analysis == null)
                    {
                        bad++;
                        ed.WriteMessage($"\n未能修正({pt.X:0.0},{pt.Y:0.0}): {_lastWhy}");
                        continue;
                    }

                    var directional = blockName.StartsWith(DirectionalBlockPrefix, StringComparison.OrdinalIgnoreCase);
                    if (analysis.HasNoSupport)
                    {
                        if (directional)
                        {
                            br.UpgradeOpen();
                            br.Erase();
                            removedUnsupported++;
                        }
                        else
                        {
                            bad++;
                            ed.WriteMessage("\n板格无支承，原有单向或双向符号保留。");
                        }
                        continue;
                    }

                    if (directional && !analysis.Elevation.HasValue)
                    {
                        bad++;
                        ed.WriteMessage("\n方向板符号需要已配置的板轮廓和梁线图层；原符号保留。");
                        continue;
                    }

                    var center = new Point3d(analysis.Center.X, analysis.Center.Y, analysis.Elevation ?? br.Position.Z);
                    if (doneCenters.Any(c => c.DistanceTo(center) < SideTolerance))
                    {
                        br.UpgradeOpen();
                        br.Erase();
                        dup++;
                        continue;
                    }

                    EnsureSymbolBlock(db, tr, analysis);
                    br.UpgradeOpen();
                    UpdateBlockReference(db, tr, br, analysis);
                    doneCenters.Add(center);
                    fixedCount++;
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[SLBFIX] 完成: 修正 {fixedCount} 个, 移除无支承方向符号 {removedUnsupported} 个, 删除同板格重复 {dup} 个, 跳过非板符号 {skipped} 个, 失败 {bad} 个。");
        }

        private static string? PlaceOrUpdateSymbol(Database db, SlabResult res)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                var existing = new List<BlockReference>();
                foreach (ObjectId id in space)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is BlockReference br)) continue;
                    var bname = GetBlockName(tr, br);
                    if (!IsSymbolBlock(bname)) continue;
                    if (res.Elevation.HasValue && Math.Abs(br.Position.Z - res.Elevation.Value) > SideTolerance) continue;
                    if (!IsInsidePolygon(br.Position.X, br.Position.Y, res.Polygon)) continue;

                    if (res.DirectionMask == 0 && bname.StartsWith(DirectionalBlockPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastWhy = "板格已有方向符号，请先恢复板轮廓和梁线图层设置。";
                        return null;
                    }
                    existing.Add(br);
                }

                EnsureSymbolBlock(db, tr, res);
                if (existing.Count > 0)
                {
                    var keep = existing[0];
                    keep.UpgradeOpen();
                    UpdateBlockReference(db, tr, keep, res);
                    for (var i = 1; i < existing.Count; i++)
                    {
                        existing[i].UpgradeOpen();
                        existing[i].Erase();
                    }
                    tr.Commit();
                    return "更新";
                }

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                space.UpgradeOpen();
                var newBr = new BlockReference(new Point3d(res.Center.X, res.Center.Y, res.Elevation ?? 0.0), bt[res.BlockName])
                {
                    Layer = SymbolLayerName,
                    ScaleFactors = new Scale3d(res.Size, res.Size, res.Size),
                    Rotation = res.Rotation
                };
                space.AppendEntity(newBr);
                tr.AddNewlyCreatedDBObject(newBr, true);
                tr.Commit();
                return "新建";
            }
        }

        private static void UpdateBlockReference(Database db, Transaction tr, BlockReference br, SlabResult res)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            br.BlockTableRecord = bt[res.BlockName];
            br.Position = new Point3d(res.Center.X, res.Center.Y, res.Elevation ?? br.Position.Z);
            br.ScaleFactors = new Scale3d(res.Size, res.Size, res.Size);
            br.Rotation = res.Rotation;
        }

        private static string GetBlockName(Transaction tr, BlockReference br)
        {
            var id = br.DynamicBlockTableRecord.IsNull ? br.BlockTableRecord : br.DynamicBlockTableRecord;
            return tr.GetObject(id, OpenMode.ForRead) is BlockTableRecord btr ? btr.Name : "";
        }

        private static bool IsSymbolBlock(string name)
        {
            if (KnownSymbolBlocks.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
            return name.Length == DirectionalBlockPrefix.Length + 2 &&
                name.StartsWith(DirectionalBlockPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring(DirectionalBlockPrefix.Length), out var mask) &&
                mask >= 1 && mask <= 15 && name.EndsWith(mask.ToString("D2"), StringComparison.Ordinal);
        }

        private static string? InferOldType(string blockName)
        {
            var n = (blockName ?? "").ToUpperInvariant();
            if (n == "SLB_ONE" || n == "YB5_ARM_X" || n == "YB5_ARM_Y") return "ONE";
            if (n == "SLB_TWO" || n == "YB5_ARM_B") return "TWO";
            return null;
        }

        private static void EnsureSymbolBlock(Database db, Transaction tr, SlabResult result)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(SymbolLayerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = SymbolLayerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2)
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(result.BlockName)) return;
            bt.UpgradeOpen();
            var btr = new BlockTableRecord { Name = result.BlockName };
            bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            foreach (var (x1, y1, x2, y2) in SymbolLines(result))
                AddBlockLine(btr, tr, x1, y1, x2, y2);
        }

        private static IEnumerable<(double X1, double Y1, double X2, double Y2)> SymbolLines(SlabResult result)
        {
            if (result.HasNoSupport) yield break;

            var mask = result.DirectionMask;
            if (mask == 0)
            {
                yield return (-0.5, 0, 0.5, 0);
                if (result.Type == "TWO") yield return (0, -0.5, 0, 0.5);
                mask = result.Type == "ONE" ? 5 : 15;
            }

            var axes = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) };
            for (var i = 0; i < axes.Length; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var (x, y) = axes[i];
                var tipX = 0.5 * x;
                var tipY = 0.5 * y;
                if (result.DirectionMask != 0) yield return (0, 0, tipX, tipY);
                yield return (tipX, tipY, 0.36 * x - 0.08 * y, 0.36 * y + 0.08 * x);
                yield return (tipX, tipY, 0.36 * x + 0.08 * y, 0.36 * y - 0.08 * x);
            }
        }

        private static void AddBlockLine(BlockTableRecord btr, Transaction tr, double x1, double y1, double x2, double y2)
        {
            var ln = new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0)) { Layer = "0" };
            btr.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
        }

        private static SlabResult? AnalyzePoint(Database db, Point2d clickPt, UserSettings settings, string? forcedType,
            double? elevation = null)
        {
            _lastWhy = "未找到封闭板格";
            DrawingLayers.TryGetValue(db, out var layers);
            if (layers?.Outline != null)
            {
                var outline = FindOutline(db, clickPt, layers.Outline, elevation);
                if (!outline.HasValue)
                {
                    _lastWhy = "在已配置的板轮廓图层中没有找到包含所选点的同标高矩形板。";
                    return null;
                }
                return AnalyzeSupportedOutline(db, clickPt, outline.Value.Points, outline.Value.Elevation, settings);
            }

            var radii = new[] { 4000.0, 9000.0, Math.Max(9000.0, settings.SlabMaxSpan) };

            foreach (var r in radii)
            {
                var segs = CollectSegments(db, clickPt.X, clickPt.Y, r);
                if (segs.Count < 3)
                {
                    _lastWhy = $"窗口内梁线太少({segs.Count}段)；可用 SLBLAYER 指定梁线图层";
                    continue;
                }

                var poly = FindContainingFace(segs, settings.SlabExtend, 1.0);
                if (poly == null)
                {
                    _lastWhy = "未找到封闭板格(梁线可能有缺口，可增大 SLBSET 线端延伸)";
                    continue;
                }

                if (poly.Any(p => Math.Abs(p.X) > r - 50.0 || Math.Abs(p.Y) > r - 50.0))
                {
                    _lastWhy = "板格范围超出当前搜索窗口";
                    continue;
                }

                var obb = ComputeObb(poly);
                if (Math.Min(obb.S1, obb.S2) < settings.SlabMinSpan || Math.Max(obb.S1, obb.S2) > settings.SlabMaxSpan)
                {
                    _lastWhy = $"识别到的板格尺寸({obb.S1:0} x {obb.S2:0})超出设定范围";
                    return null;
                }

                var c = InsidePoint(poly, Centroid(poly));
                var shortSpan = Math.Min(obb.S1, obb.S2);
                var longSpan = Math.Max(obb.S1, obb.S2);
                var ratio = longSpan / Math.Max(shortSpan, 1e-6);

                var type = forcedType ?? (ratio >= settings.SlabOneWayRatio ? "ONE" : "TWO");
                var rot = type == "ONE"
                    ? NormHalf(obb.S1 <= obb.S2 ? obb.A1 : obb.A2)
                    : NormQuarter(obb.A1);
                var size = Math.Min(settings.SlabMaxSize, Math.Max(settings.SlabMinSize, settings.SlabFactor * shortSpan));

                return new SlabResult
                {
                    Center = new Point2d(c.X + clickPt.X, c.Y + clickPt.Y),
                    Polygon = poly.Select(q => new Point2d(q.X + clickPt.X, q.Y + clickPt.Y)).ToList(),
                    Type = type,
                    Rotation = rot,
                    Size = size,
                    Ratio = ratio,
                    Span1 = obb.S1,
                    Span2 = obb.S2
                };
            }

            return null;
        }

        private static List<Point2d>? GetRectangle(Polyline pl)
        {
            if (!pl.Closed || pl.NumberOfVertices != 4 || Math.Abs(pl.Normal.Z - 1.0) > 1e-8) return null;
            var points = new List<Point2d>(4);
            for (var i = 0; i < 4; i++)
            {
                if (Math.Abs(pl.GetBulgeAt(i)) > 1e-9) return null;
                var p = pl.GetPoint3dAt(i);
                points.Add(new Point2d(p.X, p.Y));
            }

            for (var i = 0; i < 4; i++)
            {
                var edge = points[(i + 1) % 4] - points[i];
                var next = points[(i + 2) % 4] - points[(i + 1) % 4];
                if (edge.Length <= SideTolerance || next.Length <= SideTolerance ||
                    Math.Abs(edge.DotProduct(next)) > SideTolerance * Math.Min(edge.Length, next.Length))
                    return null;
            }
            return points;
        }

        private static (List<Point2d> Points, double Elevation)? FindOutline(
            Database db, Point2d clickPt, string layer, double? elevation)
        {
            (List<Point2d> Points, double Elevation)? best = null;
            var bestArea = double.MaxValue;
            using var tr = db.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in space)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Polyline pl) ||
                    !string.Equals(pl.Layer, layer, StringComparison.OrdinalIgnoreCase) ||
                    (elevation.HasValue && Math.Abs(pl.Elevation - elevation.Value) > SideTolerance)) continue;

                var points = GetRectangle(pl);
                if (points == null || !IsInsidePolygon(clickPt.X, clickPt.Y, points)) continue;
                var area = points[0].GetDistanceTo(points[1]) * points[1].GetDistanceTo(points[2]);
                if (area >= bestArea) continue;

                bestArea = area;
                best = (points, pl.Elevation);
            }
            return best;
        }

        private static SlabResult? AnalyzeSupportedOutline(
            Database db, Point2d clickPt, List<Point2d> points, double elevation, UserSettings settings)
        {
            var span1 = points[0].GetDistanceTo(points[1]);
            var span2 = points[1].GetDistanceTo(points[2]);
            var shortSpan = Math.Min(span1, span2);
            var longSpan = Math.Max(span1, span2);
            if (shortSpan < settings.SlabMinSpan || longSpan > settings.SlabMaxSpan)
            {
                _lastWhy = $"识别到的板格尺寸({span1:0} x {span2:0})超出设定范围";
                return null;
            }

            var local = points.Select(p => new Point2d(p.X - clickPt.X, p.Y - clickPt.Y)).ToList();
            var beams = CollectSegments(db, clickPt.X, clickPt.Y, span1 + span2 + SideTolerance, elevation);
            var directions = SignedArea(local) > 0 ? new[] { 3, 0, 1, 2 } : new[] { 1, 0, 3, 2 };
            var mask = 0;
            for (var i = 0; i < 4; i++)
            {
                if (HasSideSupport(local[i], local[(i + 1) % 4], beams))
                    mask |= 1 << directions[i];
            }
            return new SlabResult
            {
                Center = new Point2d((points[0].X + points[2].X) * 0.5, (points[0].Y + points[2].Y) * 0.5),
                Polygon = points,
                Rotation = Math.Atan2(local[1].Y - local[0].Y, local[1].X - local[0].X),
                Size = Math.Min(settings.SlabMaxSize, Math.Max(settings.SlabMinSize, settings.SlabFactor * shortSpan)),
                Ratio = longSpan / shortSpan,
                Span1 = span1,
                Span2 = span2,
                DirectionMask = mask,
                Elevation = elevation
            };
        }

        private static bool HasSideSupport(Point2d a, Point2d b, List<Seg2D> beams)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var ux = dx / length;
            var uy = dy / length;
            var intervals = new List<(double Start, double End)>();
            foreach (var beam in beams)
            {
                var ax = beam.X1 - a.X;
                var ay = beam.Y1 - a.Y;
                var bx = beam.X2 - a.X;
                var by = beam.Y2 - a.Y;
                if (Math.Abs(ax * uy - ay * ux) > SideTolerance || Math.Abs(bx * uy - by * ux) > SideTolerance)
                    continue;

                var p = ax * ux + ay * uy;
                var q = bx * ux + by * uy;
                var start = Math.Max(0, Math.Min(p, q));
                var end = Math.Min(length, Math.Max(p, q));
                if (end > start) intervals.Add((start, end));
            }

            var covered = 0.0;
            var lastEnd = 0.0;
            foreach (var (start, end) in intervals.OrderBy(x => x.Start))
            {
                covered += Math.Max(0.0, end - Math.Max(start, lastEnd));
                lastEnd = Math.Max(lastEnd, end);
            }
            return covered >= length * 0.5;
        }

        private static List<Seg2D> CollectSegments(Database db, double ox, double oy, double radius, double? elevation = null)
        {
            DrawingLayers.TryGetValue(db, out var layers);
            var beamLayers = layers?.Beams;
            var result = new List<Seg2D>();
            var minSeg = 20.0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                foreach (ObjectId id in space)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (beamLayers != null && beamLayers.Count > 0)
                    {
                        if (!beamLayers.Contains(ent.Layer, StringComparer.OrdinalIgnoreCase)) continue;
                    }
                    else if (string.Equals(ent.Layer, SymbolLayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!(ent is Line) && !(ent is Polyline) && !(ent is Arc)) continue;

                    try
                    {
                        var ext = ent.GeometricExtents;
                        if (ext.MaxPoint.X < ox - radius || ext.MinPoint.X > ox + radius ||
                            ext.MaxPoint.Y < oy - radius || ext.MinPoint.Y > oy + radius)
                            continue;
                    }
                    catch { }

                    if (ent is Line ln)
                    {
                        if (elevation.HasValue && (Math.Abs(ln.StartPoint.Z - elevation.Value) > SideTolerance ||
                            Math.Abs(ln.EndPoint.Z - elevation.Value) > SideTolerance)) continue;
                        AddSeg(result, ln.StartPoint.X - ox, ln.StartPoint.Y - oy, ln.EndPoint.X - ox, ln.EndPoint.Y - oy, minSeg);
                    }
                    else if (ent is Arc arc)
                    {
                        if (elevation.HasValue) continue;
                        var sw = arc.EndAngle - arc.StartAngle;
                        if (sw <= 0) sw += 2.0 * Math.PI;
                        var pts = DiscretizeArc(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, sw);
                        AddPtsSegs(result, pts, ox, oy, minSeg);
                    }
                    else if (ent is Polyline pl)
                    {
                        if (elevation.HasValue && (Math.Abs(pl.Normal.Z - 1.0) > 1e-8 ||
                            Math.Abs(pl.Elevation - elevation.Value) > SideTolerance)) continue;
                        var n = pl.NumberOfVertices;
                        var segCount = pl.Closed ? n : n - 1;
                        for (var i = 0; i < segCount; i++)
                        {
                            var p1 = pl.GetPoint2dAt(i);
                            var p2 = pl.GetPoint2dAt((i + 1) % n);
                            var b = pl.GetBulgeAt(i);
                            if (Math.Abs(b) < 1e-9)
                            {
                                AddSeg(result, p1.X - ox, p1.Y - oy, p2.X - ox, p2.Y - oy, minSeg);
                            }
                            else if (!elevation.HasValue)
                            {
                                var pts = DiscretizeBulge(p1.X, p1.Y, p2.X, p2.Y, b);
                                AddPtsSegs(result, pts, ox, oy, minSeg);
                            }
                        }
                    }
                }
                tr.Commit();
            }
            return result;
        }

        private static void AddPtsSegs(List<Seg2D> dst, List<Point2d> pts, double ox, double oy, double minSeg)
        {
            for (var i = 0; i + 1 < pts.Count; i++)
                AddSeg(dst, pts[i].X - ox, pts[i].Y - oy, pts[i + 1].X - ox, pts[i + 1].Y - oy, minSeg);
        }

        private static void AddSeg(List<Seg2D> dst, double x1, double y1, double x2, double y2, double minSeg)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len > minSeg)
                dst.Add(new Seg2D(x1, y1, x2, y2, len));
        }

        private static List<Point2d> DiscretizeArc(double cx, double cy, double r, double a0, double sweep)
        {
            var n = Math.Max(2, 1 + (int)(Math.Abs(sweep) / (Math.PI / 24.0)));
            var pts = new List<Point2d>(n + 1);
            for (var k = 0; k <= n; k++)
            {
                var a = a0 + sweep * k / n;
                pts.Add(new Point2d(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            return pts;
        }

        private static List<Point2d> DiscretizeBulge(double x1, double y1, double x2, double y2, double b)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var c = Math.Sqrt(dx * dx + dy * dy);
            if (Math.Abs(b) < 1e-9 || c < 1e-9)
                return new List<Point2d> { new Point2d(x1, y1), new Point2d(x2, y2) };
            var mx = (x1 + x2) * 0.5;
            var my = (y1 + y2) * 0.5;
            var nx = -dy / c;
            var ny = dx / c;
            var off = (c * 0.5) * ((1.0 - b * b) / (2.0 * b));
            var cx = mx + nx * off;
            var cy = my + ny * off;
            var r = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));
            return DiscretizeArc(cx, cy, r, Math.Atan2(y1 - cy, x1 - cx), 4.0 * Math.Atan(b));
        }

        private static List<Point2d>? FindContainingFace(List<Seg2D> segs, double ext, double tol)
        {
            var extSegs = new List<Seg2D>(segs.Count);
            foreach (var s in segs)
            {
                var ux = (s.X2 - s.X1) / s.Len;
                var uy = (s.Y2 - s.Y1) / s.Len;
                var x1 = s.X1 - ux * ext;
                var y1 = s.Y1 - uy * ext;
                var x2 = s.X2 + ux * ext;
                var y2 = s.Y2 + uy * ext;
                var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                if (len > 20.0) extSegs.Add(new Seg2D(x1, y1, x2, y2, len));
            }

            var nodes = new List<Point2d>();
            var hash = new Dictionary<(int, int), List<int>>();
            var adj = new Dictionary<int, List<int>>();

            int GetNode(double x, double y)
            {
                var cx = (int)Math.Floor((x + 1e6) / (3.0 * tol));
                var cy = (int)Math.Floor((y + 1e6) / (3.0 * tol));
                var tolSq = tol * tol;
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (!hash.TryGetValue((cx + dx, cy + dy), out var bucket)) continue;
                        foreach (var idx in bucket)
                        {
                            var p = nodes[idx];
                            var ddx = x - p.X;
                            var ddy = y - p.Y;
                            if (ddx * ddx + ddy * ddy < tolSq) return idx;
                        }
                    }
                }

                var newIdx = nodes.Count;
                nodes.Add(new Point2d(x, y));
                if (!hash.TryGetValue((cx, cy), out var list))
                {
                    list = new List<int>();
                    hash[(cx, cy)] = list;
                }
                list.Add(newIdx);
                adj[newIdx] = new List<int>();
                return newIdx;
            }

            for (var i = 0; i < extSegs.Count; i++)
            {
                var a = extSegs[i];
                var ts = ComputeSplits(a, extSegs, i, tol);
                var dx = a.X2 - a.X1;
                var dy = a.Y2 - a.Y1;
                var prev = -1;
                foreach (var u in ts)
                {
                    var id = GetNode(a.X1 + u * dx, a.Y1 + u * dy);
                    if (prev >= 0 && prev != id)
                    {
                        if (!adj[prev].Contains(id)) adj[prev].Add(id);
                        if (!adj[id].Contains(prev)) adj[id].Add(prev);
                    }
                    prev = id;
                }
            }

            // Prune degree <= 1
            var changed = true;
            while (changed)
            {
                changed = false;
                var keys = adj.Keys.ToList();
                foreach (var k in keys)
                {
                    if (!adj.TryGetValue(k, out var nb) || nb.Count > 1) continue;
                    foreach (var other in nb)
                    {
                        if (adj.TryGetValue(other, out var onb)) onb.Remove(k);
                    }
                    adj.Remove(k);
                    changed = true;
                }
            }

            var rayAngles = new[] { 0.0, 0.7, 1.3, 2.1, 2.9, 3.7, 4.5, 5.3 };
            foreach (var theta in rayAngles)
            {
                var rdx = Math.Cos(theta);
                var rdy = Math.Sin(theta);
                var bestI = -1;
                var bestJ = -1;
                var bestS = double.MaxValue;

                foreach (var kv in adj)
                {
                    var i = kv.Key;
                    var ax = nodes[i].X;
                    var ay = nodes[i].Y;
                    foreach (var j in kv.Value)
                    {
                        if (j <= i) continue;
                        var rx = nodes[j].X - ax;
                        var ry = nodes[j].Y - ay;
                        var den = rdx * ry - rdy * rx;
                        if (Math.Abs(den) <= 1e-12) continue;
                        var s = (ax * ry - ay * rx) / den;
                        var u = (ax * rdy - ay * rdx) / den;
                        if (s > 1e-6 && u > 1e-9 && u < 1.0 - 1e-9 && s < bestS)
                        {
                            bestS = s;
                            bestI = i;
                            bestJ = j;
                        }
                    }
                }

                if (bestI < 0) continue;
                var ea = nodes[bestI];
                var eb = nodes[bestJ];
                var u0 = bestI;
                var v0 = bestJ;
                if ((eb.X - ea.X) * (-ea.Y) - (eb.Y - ea.Y) * (-ea.X) <= 0.0)
                {
                    u0 = bestJ;
                    v0 = bestI;
                }

                var polyIdx = TraceCycle(u0, v0, nodes, adj);
                if (polyIdx == null) continue;
                var pts = polyIdx.Select(idx => nodes[idx]).ToList();
                if (SignedArea(pts) > 1.0 && IsInsidePolygon(0.0, 0.0, pts))
                    return pts;
            }

            return null;
        }

        private static List<double> ComputeSplits(Seg2D a, List<Seg2D> segs, int selfIdx, double tol)
        {
            var x1 = a.X1;
            var y1 = a.Y1;
            var dx = a.X2 - x1;
            var dy = a.Y2 - y1;
            var la = a.Len;
            var ts = new List<double> { 0.0, 1.0 };

            for (var j = 0; j < segs.Count; j++)
            {
                if (j == selfIdx) continue;
                var b = segs[j];
                if (b.MinX > a.MaxX + tol || b.MaxX < a.MinX - tol || b.MinY > a.MaxY + tol || b.MaxY < a.MinY - tol)
                    continue;
                var ex = b.X2 - b.X1;
                var ey = b.Y2 - b.Y1;
                var lb = b.Len;
                var den = dx * ey - dy * ex;
                if (Math.Abs(den) > 1e-9 * la * lb)
                {
                    var t0 = ((b.X1 - x1) * ey - (b.Y1 - y1) * ex) / den;
                    var u0 = ((b.X1 - x1) * dy - (b.Y1 - y1) * dx) / den;
                    if (t0 >= -tol / la && t0 <= 1.0 + tol / la && u0 >= -tol / lb && u0 <= 1.0 + tol / lb)
                        ts.Add(Math.Max(0.0, Math.Min(1.0, t0)));
                }
                else if (Math.Abs((b.X1 - x1) * dy - (b.Y1 - y1) * dx) / la < tol)
                {
                    var tA = ((b.X1 - x1) * dx + (b.Y1 - y1) * dy) / (la * la);
                    if (tA > 0.0 && tA < 1.0) ts.Add(tA);
                    var tB = ((b.X2 - x1) * dx + (b.Y2 - y1) * dy) / (la * la);
                    if (tB > 0.0 && tB < 1.0) ts.Add(tB);
                }
            }

            ts.Sort();
            var res = new List<double>();
            double? prev = null;
            foreach (var u in ts)
            {
                if (!prev.HasValue || (u - prev.Value) * la > tol)
                {
                    res.Add(u);
                    prev = u;
                }
            }
            return res;
        }

        private static List<int>? TraceCycle(int u0, int v0, List<Point2d> nodes, Dictionary<int, List<int>> adj)
        {
            var poly = new List<int>();
            var u = u0;
            var v = v0;
            for (var steps = 0; steps < 4000; steps++)
            {
                poly.Add(u);
                if (!adj.TryGetValue(v, out var nbs) || nbs.Count == 0) return null;
                var back = Math.Atan2(nodes[u].Y - nodes[v].Y, nodes[u].X - nodes[v].X);
                var best = u;
                var bestD = double.MaxValue;
                foreach (var w in nbs)
                {
                    if (w == u) continue;
                    var ang = Math.Atan2(nodes[w].Y - nodes[v].Y, nodes[w].X - nodes[v].X);
                    var d = back - ang;
                    while (d <= 1e-12) d += 2.0 * Math.PI;
                    while (d > 2.0 * Math.PI) d -= 2.0 * Math.PI;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = w;
                    }
                }
                u = v;
                v = best;
                if (u == u0 && v == v0) return poly;
            }
            return null;
        }

        private static double SignedArea(List<Point2d> pts)
        {
            var acc = 0.0;
            var prev = pts[pts.Count - 1];
            foreach (var p in pts)
            {
                acc += prev.X * p.Y - prev.Y * p.X;
                prev = p;
            }
            return 0.5 * acc;
        }

        private static bool IsInsidePolygon(double px, double py, List<Point2d> pts)
        {
            var hit = false;
            var prev = pts[pts.Count - 1];
            foreach (var q in pts)
            {
                if (((prev.Y > py) != (q.Y > py)) &&
                    (px < prev.X + ((py - prev.Y) / (q.Y - prev.Y)) * (q.X - prev.X)))
                {
                    hit = !hit;
                }
                prev = q;
            }
            return hit;
        }

        private static Point2d Centroid(List<Point2d> pts)
        {
            var prev = pts[pts.Count - 1];
            double area = 0, sx = 0, sy = 0;
            foreach (var p in pts)
            {
                var z = prev.X * p.Y - prev.Y * p.X;
                area += z;
                sx += (prev.X + p.X) * z;
                sy += (prev.Y + p.Y) * z;
                prev = p;
            }
            if (Math.Abs(area) > 1e-6)
                return new Point2d(sx / (3.0 * area), sy / (3.0 * area));
            return pts[0];
        }

        private static Point2d InsidePoint(List<Point2d> pts, Point2d c)
        {
            if (IsInsidePolygon(c.X, c.Y, pts)) return c;
            var xs = new List<double>();
            var prev = pts[pts.Count - 1];
            foreach (var q in pts)
            {
                if ((prev.Y > c.Y) != (q.Y > c.Y))
                    xs.Add(prev.X + ((c.Y - prev.Y) / (q.Y - prev.Y)) * (q.X - prev.X));
                prev = q;
            }
            xs.Sort();
            var bestW = -1.0;
            var bestX = c.X;
            for (var i = 0; i + 1 < xs.Count; i += 2)
            {
                var w = xs[i + 1] - xs[i];
                if (w > bestW)
                {
                    bestW = w;
                    bestX = (xs[i] + xs[i + 1]) * 0.5;
                }
            }
            return bestW > 0 ? new Point2d(bestX, c.Y) : c;
        }

        private readonly struct ObbInfo
        {
            public readonly double A1, S1, A2, S2;
            public ObbInfo(double a1, double s1, double a2, double s2)
            {
                A1 = a1; S1 = s1; A2 = a2; S2 = s2;
            }
        }

        private static ObbInfo ComputeObb(List<Point2d> pts)
        {
            var prev = pts[pts.Count - 1];
            var bestArea = double.MaxValue;
            ObbInfo best = default;
            foreach (var p in pts)
            {
                if (Math.Abs(p.X - prev.X) + Math.Abs(p.Y - prev.Y) > 1e-6)
                {
                    var ang = Math.Atan2(p.Y - prev.Y, p.X - prev.X);
                    var ux = Math.Cos(ang);
                    var uy = Math.Sin(ang);
                    var vx = -uy;
                    var vy = ux;
                    double lx = double.MaxValue, hx = double.MinValue;
                    double ly = double.MaxValue, hy = double.MinValue;
                    foreach (var q in pts)
                    {
                        var x = q.X * ux + q.Y * uy;
                        var y = q.X * vx + q.Y * vy;
                        if (x < lx) lx = x;
                        if (x > hx) hx = x;
                        if (y < ly) ly = y;
                        if (y > hy) hy = y;
                    }
                    var area = (hx - lx) * (hy - ly);
                    if (area < bestArea - 1e-6)
                    {
                        bestArea = area;
                        best = new ObbInfo(ang, hx - lx, ang + Math.PI / 2.0, hy - ly);
                    }
                }
                prev = p;
            }
            return best;
        }

        private static double NormHalf(double a)
        {
            while (a >= Math.PI / 2.0) a -= Math.PI;
            while (a < -Math.PI / 2.0) a += Math.PI;
            return a;
        }

        private static double NormQuarter(double a)
        {
            while (a >= Math.PI / 4.0) a -= Math.PI / 2.0;
            while (a < -Math.PI / 4.0) a += Math.PI / 2.0;
            return a;
        }

        private static double AskDouble(Editor ed, string msg, double cur, double min, double max)
        {
            var pdo = new PromptDoubleOptions($"\n{msg} <{cur:0.##}>：")
            {
                DefaultValue = cur,
                UseDefaultValue = true,
                AllowNegative = min < 0,
                AllowZero = min <= 0,
                AllowNone = true
            };
            var r = ed.GetDouble(pdo);
            if (r.Status != PromptStatus.OK) return cur;
            return Math.Max(min, Math.Min(max, r.Value));
        }
    }
}
