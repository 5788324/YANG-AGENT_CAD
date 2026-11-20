using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class StructuralMarkService
    {
        private static readonly Dictionary<string, double> BeamLayerHeights =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public static void RunColumnMark(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
            var selIds = PromptOrImpliedWithSettings(
                ed,
                filter,
                () => $"\n[CMK] 字高={settings.ColTextHeight:0} 取整={settings.ColRoundStep:0.##} 格式={FormatColMode(settings.ColFormat)} 范围={settings.ColMinEdge:0}~{settings.ColMaxEdge:0} 图层={(string.IsNullOrWhiteSpace(settings.ColLayer) ? "同柱" : settings.ColLayer)}",
                "\n选择柱子轮廓(闭合矩形多段线) [改参数(S)]：",
                () => ConfigureColumnSettings(ed, settings));

            if (selIds == null || selIds.Length == 0)
            {
                ed.WriteMessage("\n未选择到多段线。");
                return;
            }

            var logLines = new List<string>
            {
                $"CMK LOG  drawing={Path.GetFileName(doc.Name)}  polylines={selIds.Length}",
                $"text_h={settings.ColTextHeight:0.##} step={settings.ColRoundStep:0.##} fmt={settings.ColFormat} min={settings.ColMinEdge:0.##} max={settings.ColMaxEdge:0.##} layer={(string.IsNullOrWhiteSpace(settings.ColLayer) ? "(same as column)" : settings.ColLayer)}"
            };

            int cnt = 0, skip = 0, bad = 0, outRange = 0;
            var stats = new Dictionary<string, int>(StringComparer.Ordinal);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var targetLayerName = (settings.ColLayer ?? "").Trim();
                if (!string.IsNullOrEmpty(targetLayerName))
                    EnsureLayer(db, tr, targetLayerName, 7);

                var existingTexts = CollectNumericTextBoxes(tr, db.CurrentSpaceId);
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var step = settings.ColRoundStep <= 0 ? 1.0 : settings.ColRoundStep;

                foreach (var id in selIds)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Polyline pl))
                    {
                        bad++;
                        continue;
                    }

                    if (!TryGetRectColumn(pl, out var pts, out var l1, out var l2, out var e1))
                    {
                        bad++;
                        if (pl.NumberOfVertices > 0)
                        {
                            var p0 = pl.GetPoint2dAt(0);
                            logLines.Add($"NOT-RECT at=({p0.X:0.00},{p0.Y:0.00})");
                        }
                        continue;
                    }

                    var minE = Math.Min(l1, l2);
                    var maxE = Math.Max(l1, l2);
                    if (minE < settings.ColMinEdge || maxE > settings.ColMaxEdge)
                    {
                        outRange++;
                        logLines.Add($"OUT-OF-RANGE {l1:0.00}x{l2:0.00} at=({pts[0].X:0.00},{pts[0].Y:0.00})");
                        continue;
                    }

                    var ang = Math.Abs(Math.Atan2(e1.Y, e1.X));
                    if (ang > Math.PI / 2.0) ang = Math.PI - ang;
                    var xd = ang <= Math.PI / 4.0 ? l1 : l2;
                    var yd = ang <= Math.PI / 4.0 ? l2 : l1;
                    xd = RoundToStep(xd, step);
                    yd = RoundToStep(yd, step);

                    string label;
                    if (settings.ColFormat == 2)
                        label = $"{Math.Round(yd):0}x{Math.Round(xd):0}";
                    else if (settings.ColFormat == 3)
                        label = $"{Math.Round(Math.Max(xd, yd)):0}x{Math.Round(Math.Min(xd, yd)):0}";
                    else
                        label = $"{Math.Round(xd):0}x{Math.Round(yd):0}";

                    var minX = pts.Min(p => p.X);
                    var maxX = pts.Max(p => p.X);
                    var maxY = pts.Max(p => p.Y);
                    var cx = (minX + maxX) * 0.5;
                    var th = settings.ColTextHeight;
                    var gap = 0.4 * th;
                    var boxW = Math.Max(3, label.Length) * th * 0.75;

                    if (HasNumericTextInRect(existingTexts, cx - boxW * 0.5, cx + boxW * 0.5, maxY, maxY + gap + th))
                    {
                        skip++;
                        logLines.Add($"SKIP-EXISTING-TEXT {label} at=({cx:0.00},{maxY:0.00})");
                        continue;
                    }

                    var pt = new Point3d(cx, maxY + gap, pl.Elevation);
                    var txt = new DBText
                    {
                        Position = pt,
                        Height = th,
                        TextString = label,
                        Rotation = 0.0,
                        TextStyleId = db.Textstyle,
                        Layer = string.IsNullOrEmpty(targetLayerName) ? pl.Layer : targetLayerName,
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextBottom,
                        AlignmentPoint = pt
                    };

                    space.AppendEntity(txt);
                    tr.AddNewlyCreatedDBObject(txt, true);
                    txt.AdjustAlignment(db);
                    existingTexts.Add(new TextBoxInfo(cx - boxW * 0.5, cx + boxW * 0.5, maxY + gap, maxY + gap + th));

                    cnt++;
                    stats[label] = stats.TryGetValue(label, out var cur) ? cur + 1 : 1;
                    logLines.Add($"LABEL {label} at=({pt.X:0.00},{pt.Y:0.00})");
                }

                tr.Commit();
            }

            logLines.Add("--- size summary ---");
            foreach (var kv in stats.OrderBy(k => k.Key, StringComparer.Ordinal))
                logLines.Add($"{kv.Key} : {kv.Value}");
            logLines.Add($"SUMMARY labels={cnt} skipped_existing={skip} not_rect={bad} out_of_range={outRange}");

            var logPath = TryWriteLog("COL_log.txt", logLines);
            ed.WriteMessage($"\n[CMK] 完成: 新增标注 {cnt} 处, 已有标注跳过 {skip} 处, 非矩形 {bad} 个, 尺寸超范围 {outRange} 个。");
            if (stats.Count > 0)
            {
                ed.WriteMessage("\n尺寸统计:");
                foreach (var kv in stats.OrderBy(k => k.Key, StringComparer.Ordinal))
                    ed.WriteMessage($"\n   {kv.Key}  ×{kv.Value}");
            }
            if (!string.IsNullOrEmpty(logPath))
                ed.WriteMessage($"\n日志已保存: {logPath}");
            ed.WriteMessage("\n如不满意可直接 U 撤销。");
        }

        public static void RunBeamMark(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE") });
            var selIds = PromptOrImpliedWithSettings(
                ed,
                filter,
                () => $"\n[BMK] 字高={settings.BeamTextHeight:0} 梁宽={settings.BeamMinWidth:0}~{settings.BeamMaxWidth:0} 最小段长={settings.BeamMinOverlap:0} 容差={settings.BeamAngleTolDeg:0.#}°/{settings.BeamWidthTol:0.#}mm",
                "\n选择梁边线（直线或多段线直边），可框选整区域 [改参数(S)]：",
                () => ConfigureBeamSettings(ed, settings));

            if (selIds == null || selIds.Length == 0)
            {
                ed.WriteMessage("\n未选择到梁边线。");
                return;
            }

            var angTolSin = Math.Sin(settings.BeamAngleTolDeg * Math.PI / 180.0);
            var step = settings.BeamRoundStep <= 0 ? 1.0 : settings.BeamRoundStep;
            var dmin = settings.BeamMinWidth;
            var dmax = settings.BeamMaxWidth;
            var ovmin = settings.BeamMinOverlap;
            var wtol = settings.BeamWidthTol;
            var th = settings.BeamTextHeight;

            var logLines = new List<string>
            {
                $"BMK LOG  drawing={Path.GetFileName(doc.Name)}  lines={selIds.Length}",
                $"text_h={th:0.##} step={step:0.##} wmin={dmin:0.##} wmax={dmax:0.##} minlen={ovmin:0.##} fmt={settings.BeamFormat} tol_deg={settings.BeamAngleTolDeg:0.##} width_tol={wtol:0.##}"
            };

            var recs = new List<BeamLineRec>();
            var distinctLayers = new List<string>();
            var layerColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in selIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead);
                    var first = recs.Count;
                    if (entity is Line line)
                        AddSegment(line.StartPoint, line.EndPoint, line.Layer, id);
                    else if (entity is Polyline poly && Math.Abs(Math.Abs(poly.Normal.Z) - 1.0) <= 1e-6)
                    {
                        var segmentCount = poly.Closed ? poly.NumberOfVertices : poly.NumberOfVertices - 1;
                        for (var i = 0; i < segmentCount; i++)
                        {
                            if (Math.Abs(poly.GetBulgeAt(i)) > 1e-6) continue;
                            AddSegment(poly.GetPoint3dAt(i), poly.GetPoint3dAt((i + 1) % poly.NumberOfVertices), poly.Layer, id);
                        }
                    }

                    if (recs.Count == first) continue;
                    var layer = recs[first].Layer;
                    if (!distinctLayers.Contains(layer, StringComparer.OrdinalIgnoreCase))
                    {
                        distinctLayers.Add(layer);
                        if (lt.Has(layer))
                        {
                            var ltr = (LayerTableRecord)tr.GetObject(lt[layer], OpenMode.ForRead);
                            layerColors[layer] = Math.Abs(ltr.Color.ColorIndex);
                        }
                    }
                }
                tr.Commit();
            }

            if (recs.Count == 0)
            {
                ed.WriteMessage("\n没有可配对的直边，或直边短于设定梁段长度。");
                return;
            }

            foreach (var lay in distinctLayers)
            {
                var defH = BeamLayerHeights.TryGetValue(lay, out var prevH) ? prevH : 1000.0;
                var colHint = layerColors.TryGetValue(lay, out var ci) ? $" (颜色{ci})" : "";
                var pdo = new PromptDoubleOptions($"\n图层 [{lay}]{colHint} 的梁高(0=不标注) <{defH:0}>：")
                {
                    DefaultValue = defH,
                    UseDefaultValue = true,
                    AllowNegative = false,
                    AllowZero = true,
                    AllowNone = true
                };
                var pdr = ed.GetDouble(pdo);
                if (pdr.Status == PromptStatus.Cancel) return;
                var val = pdr.Status == PromptStatus.OK ? pdr.Value : defH;
                BeamLayerHeights[lay] = val;
                logLines.Add($"LAYER {lay} height={val:0.00}");
            }

            int cnt = 0, skip = 0, fail = 0, nshort = 0, nbetween = 0, nlayer = 0, nwidth = 0;
            var pairedSegments = new HashSet<BeamLineRec>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var existingTexts = CollectNumericTextBoxes(tr, db.CurrentSpaceId);
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                for (var i = 0; i < recs.Count; i++)
                {
                    var a = recs[i];
                    var hgt = BeamLayerHeights[a.Layer];
                    if (hgt <= 0) continue;

                    for (var j = i + 1; j < recs.Count; j++)
                    {
                        var b = recs[j];
                        if (!TryRelate(a, b, angTolSin, out var tlo, out var thi, out var s)) continue;

                        var d = Math.Abs(s);
                        var ov = thi - tlo;

                        if (!string.Equals(a.Layer, b.Layer, StringComparison.OrdinalIgnoreCase))
                        {
                            if (d >= dmin - wtol && d <= dmax + wtol && ov >= ovmin)
                            {
                                nlayer++;
                                var mid = MidPoint(a, tlo, thi, s);
                                logLines.Add($"CROSS-LAYER {a.Layer} vs {b.Layer} w={d:0.00} len={ov:0.00} at=({mid.X:0.00},{mid.Y:0.00})");
                            }
                            continue;
                        }

                        if (d < dmin - wtol || d > dmax + wtol)
                        {
                            if (ov >= ovmin && d >= 0.4 * dmin && d <= 2.0 * dmax)
                            {
                                nwidth++;
                                var mid = MidPoint(a, tlo, thi, s);
                                logLines.Add($"WIDTH-OUT-OF-RANGE layer={a.Layer} w={d:0.00} len={ov:0.00} at=({mid.X:0.00},{mid.Y:0.00})");
                            }
                            continue;
                        }

                        if (ov < ovmin)
                        {
                            nshort++;
                            var mid = MidPoint(a, tlo, thi, s);
                            logLines.Add($"TOO-SHORT layer={a.Layer} w={d:0.00} len={ov:0.00} at=({mid.X:0.00},{mid.Y:0.00})");
                            continue;
                        }

                        if (HasLineBetween(a, b, recs, angTolSin, tlo, thi, s, d, ov))
                        {
                            nbetween++;
                            var mid = MidPoint(a, tlo, thi, s);
                            logLines.Add($"BLOCKED-BY-LINE layer={a.Layer} w={d:0.00} len={ov:0.00} at=({mid.X:0.00},{mid.Y:0.00})");
                            continue;
                        }

                        pairedSegments.Add(a);
                        pairedSegments.Add(b);

                        var tm = (tlo + thi) * 0.5;
                        var cs = s * 0.5;
                        var nx = -a.Uy;
                        var ny = a.Ux;

                        if (HasNumericTextNearBeam(existingTexts, a.Px, a.Py, a.Ux, a.Uy, nx, ny, tm, 0.6 * th, cs, d * 0.5 + 0.6 * th))
                        {
                            skip++;
                            var mid = MidPoint(a, tlo, thi, s);
                            logLines.Add($"SKIP-EXISTING-TEXT layer={a.Layer} mid=({mid.X:0.00},{mid.Y:0.00})");
                            continue;
                        }

                        var locked = lt.Has(a.Layer) && ((LayerTableRecord)tr.GetObject(lt[a.Layer], OpenMode.ForRead)).IsLocked;
                        if (locked)
                        {
                            fail++;
                            logLines.Add($"FAIL-ENTMAKE locked layer={a.Layer}");
                            continue;
                        }

                        var wRounded = RoundToStep(d, step);
                        var ww = Math.Round(wRounded);
                        var hh = Math.Round(hgt);
                        var label = settings.BeamFormat == 2 ? $"{hh:0}x{ww:0}" : $"{ww:0}x{hh:0}";
                        var c = cs + d * 0.5 + 0.3 * th;
                        var pt = new Point3d(a.Px + a.Ux * tm + nx * c, a.Py + a.Uy * tm + ny * c, a.Elev);

                        var txt = new DBText
                        {
                            Position = pt,
                            Height = th,
                            TextString = label,
                            Rotation = Math.Atan2(a.Uy, a.Ux),
                            TextStyleId = db.Textstyle,
                            Layer = a.Layer,
                            HorizontalMode = TextHorizontalMode.TextCenter,
                            VerticalMode = TextVerticalMode.TextBottom,
                            AlignmentPoint = pt
                        };
                        space.AppendEntity(txt);
                        tr.AddNewlyCreatedDBObject(txt, true);
                        txt.AdjustAlignment(db);

                        existingTexts.Add(new TextBoxInfo(pt.X - th, pt.X + th, pt.Y - th, pt.Y + th));
                        cnt++;
                        logLines.Add($"LABEL {label} layer={a.Layer} at=({pt.X:0.00},{pt.Y:0.00})");
                    }
                }

                tr.Commit();
            }

            var unpaired = new HashSet<ObjectId>();
            var wideTolSin = 0.2;
            var k = 0;
            foreach (var rr in recs)
            {
                if (BeamLayerHeights[rr.Layer] > 0 && !pairedSegments.Contains(rr))
                {
                    unpaired.Add(rr.Id);
                    k++;
                    logLines.Add($"UNPAIRED #{k} layer={rr.Layer} p0=({rr.Px:0.00},{rr.Py:0.00}) p1=({rr.Qx:0.00},{rr.Qy:0.00}) len={rr.Len:0.00} angle={rr.AngleDeg():0.00}");
                    var cands = new List<(double Dist, string Msg)>();
                    foreach (var cc in recs)
                    {
                        if (ReferenceEquals(cc, rr)) continue;
                        if (TryRelate(rr, cc, wideTolSin, out var tlo, out var thi, out var s))
                        {
                            var cr = rr.Ux * cc.Uy - rr.Uy * cc.Ux;
                            var angDiff = 180.0 / Math.PI * Math.Atan2(Math.Abs(cr), Math.Sqrt(Math.Max(0.0, 1.0 - cr * cr)));
                            cands.Add((Math.Abs(s), $"      cand layer={cc.Layer} dist={Math.Abs(s):0.00} overlap={thi - tlo:0.00} angle_diff_deg={angDiff:0.00}"));
                        }
                    }
                    foreach (var cand in cands.OrderBy(x => x.Dist).Take(5))
                        logLines.Add(cand.Msg);
                }
            }

            if (unpaired.Count > 0)
                ed.SetImpliedSelection(unpaired.ToArray());

            logLines.Add($"SUMMARY labels={cnt} skipped_existing={skip} failed={fail} too_short={nshort} blocked={nbetween} width_out={nwidth} cross_layer={nlayer} unpaired={unpaired.Count}");
            var logPath = TryWriteLog("BMK_log.txt", logLines);

            ed.WriteMessage($"\n[BMK] 完成: 新增标注 {cnt} 处, 已有标注跳过 {skip} 处" + (fail > 0 ? $", 创建失败 {fail} 处(请检查图层是否锁定)" : ""));
            ed.WriteMessage($"\n未标原因统计: 太短 {nshort}, 夹有其他线 {nbetween}, 宽度略超范围 {nwidth}, 平行但不同图层 {nlayer}");
            ed.WriteMessage($"\n含未配对边段的图元 {unpaired.Count} 个" + (unpaired.Count > 0 ? "，已高亮选中(带夹点)。" : "。") + "可直接 U 撤销。");
            if (!string.IsNullOrEmpty(logPath))
                ed.WriteMessage($"\n日志已保存: {logPath}");

            void AddSegment(Point3d start, Point3d end, string layer, ObjectId id)
            {
                var record = BeamLineRec.TryCreate(start, end, layer, id);
                if (record != null && record.Len >= ovmin) recs.Add(record);
            }
        }

        private static ObjectId[]? PromptOrImpliedWithSettings(
            Editor ed,
            SelectionFilter filter,
            Func<string> banner,
            string prompt,
            Action configureSettings)
        {
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                return implied.Value.GetObjectIds();
            }

            while (true)
            {
                ed.WriteMessage(banner());
                var key = "";
                var opts = new PromptSelectionOptions { MessageForAdding = prompt };
                opts.Keywords.Add("S", "S", "改参数(S)");
                opts.KeywordInput += (_, args) =>
                {
                    key = args.Input ?? "";
                    throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.OK);
                };

                PromptSelectionResult sel;
                try
                {
                    sel = ed.GetSelection(opts, filter);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) when (!string.IsNullOrEmpty(key))
                {
                    configureSettings();
                    continue;
                }

                if (sel.Status != PromptStatus.OK || sel.Value == null) return null;
                return sel.Value.GetObjectIds();
            }
        }

        private static void ConfigureColumnSettings(Editor ed, UserSettings settings)
        {
            settings.ColTextHeight = AskDouble(ed, "文字高度", settings.ColTextHeight, 1.0, 1e6);
            settings.ColRoundStep = AskDouble(ed, "尺寸取整间隔(1=取整, 50=50倍数)", settings.ColRoundStep, 1.0, 1e5);
            settings.ColFormat = AskInt(ed, "文字格式(1=宽x长, 2=长x宽, 3=大边x小边)", settings.ColFormat, 1, 3);
            settings.ColMinEdge = AskDouble(ed, "柱最小边长(mm)", settings.ColMinEdge, 1.0, 1e5);
            settings.ColMaxEdge = AskDouble(ed, "柱最大边长(mm)", settings.ColMaxEdge, settings.ColMinEdge, 1e6);
            var pso = new PromptStringOptions($"\n文字图层(输入 \".\" 或回车=与柱子相同) <{(string.IsNullOrEmpty(settings.ColLayer) ? "同柱" : settings.ColLayer)}>：")
            {
                AllowSpaces = true
            };
            var pr = ed.GetString(pso);
            if (pr.Status == PromptStatus.OK)
            {
                var raw = (pr.StringResult ?? "").Trim();
                if (raw == ".") settings.ColLayer = "";
                else if (raw.Length > 0) settings.ColLayer = raw;
            }
            settings.Save();
        }

        private static void ConfigureBeamSettings(Editor ed, UserSettings settings)
        {
            settings.BeamTextHeight = AskDouble(ed, "文字高度", settings.BeamTextHeight, 1.0, 1e6);
            settings.BeamRoundStep = AskDouble(ed, "宽度取整间隔(1=取整, 50=50倍数)", settings.BeamRoundStep, 1.0, 1e5);
            settings.BeamMinWidth = AskDouble(ed, "最小梁宽(两线最小间距)", settings.BeamMinWidth, 1.0, 1e5);
            settings.BeamMaxWidth = AskDouble(ed, "最大梁宽(两线最大间距)", settings.BeamMaxWidth, settings.BeamMinWidth, 1e6);
            settings.BeamMinOverlap = AskDouble(ed, "梁段最短长度", settings.BeamMinOverlap, 1.0, 1e6);
            settings.BeamFormat = AskInt(ed, "文字格式(1=宽x高, 2=高x宽)", settings.BeamFormat, 1, 2);
            settings.BeamAngleTolDeg = AskDouble(ed, "平行容差(度)", settings.BeamAngleTolDeg, 0.05, 30.0);
            settings.BeamWidthTol = AskDouble(ed, "宽度容差(mm)", settings.BeamWidthTol, 0.0, 500.0);
            settings.Save();
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

        private static int AskInt(Editor ed, string msg, int cur, int min, int max)
        {
            var pio = new PromptIntegerOptions($"\n{msg} <{cur}>：")
            {
                DefaultValue = cur,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowNone = true
            };
            var r = ed.GetInteger(pio);
            if (r.Status != PromptStatus.OK) return cur;
            return Math.Max(min, Math.Min(max, r.Value));
        }

        private static string FormatColMode(int fmt) => fmt == 2 ? "长x宽" : (fmt == 3 ? "大x小" : "宽x长");

        private static double RoundToStep(double value, double step) => step * Math.Floor(value / step + 0.5);

        private static bool TryGetRectColumn(Polyline pl, out Point2d[] rectPts, out double l1, out double l2, out Vector2d e1)
        {
            rectPts = Array.Empty<Point2d>();
            l1 = 0;
            l2 = 0;
            e1 = default;

            var n = pl.NumberOfVertices;
            if (n != 4 && n != 5) return false;
            for (var i = 0; i < n; i++)
            {
                if (Math.Abs(pl.GetBulgeAt(i)) > 1e-6) return false;
            }

            if (n == 4 && !pl.Closed) return false;
            var p0 = pl.GetPoint2dAt(0);
            var p1 = pl.GetPoint2dAt(1);
            var p2 = pl.GetPoint2dAt(2);
            var p3 = pl.GetPoint2dAt(3);
            if (n == 5 && p0.GetDistanceTo(pl.GetPoint2dAt(4)) >= 1e-6) return false;

            e1 = p1 - p0;
            var e2 = p2 - p1;
            l1 = e1.Length;
            l2 = e2.Length;
            if (l1 <= 1e-6 || l2 <= 1e-6) return false;

            var dot = e1.DotProduct(e2) / (l1 * l2);
            var sx = (p0.X + p2.X) - (p1.X + p3.X);
            var sy = (p0.Y + p2.Y) - (p1.Y + p3.Y);
            if (Math.Abs(dot) >= 0.02 || Math.Sqrt(sx * sx + sy * sy) >= 0.01 * Math.Min(l1, l2))
                return false;

            rectPts = new[] { p0, p1, p2, p3 };
            return true;
        }

        private static void EnsureLayer(Database db, Transaction tr, string name, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
            };
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        private readonly struct TextBoxInfo
        {
            public readonly double MinX, MaxX, MinY, MaxY;
            public TextBoxInfo(double minX, double maxX, double minY, double maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }
        }

        private static List<TextBoxInfo> CollectNumericTextBoxes(Transaction tr, ObjectId spaceId)
        {
            var list = new List<TextBoxInfo>();
            var btr = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                string? content = null;
                Point3d fallbackPt = Point3d.Origin;
                if (ent is DBText dt)
                {
                    content = dt.TextString;
                    fallbackPt = dt.Position;
                }
                else if (ent is MText mt)
                {
                    content = mt.Text;
                    fallbackPt = mt.Location;
                }
                else continue;

                if (string.IsNullOrEmpty(content) || !content!.Any(char.IsDigit)) continue;

                try
                {
                    var ext = ((Entity)ent).GeometricExtents;
                    list.Add(new TextBoxInfo(ext.MinPoint.X, ext.MaxPoint.X, ext.MinPoint.Y, ext.MaxPoint.Y));
                }
                catch
                {
                    list.Add(new TextBoxInfo(fallbackPt.X - 1, fallbackPt.X + 1, fallbackPt.Y - 1, fallbackPt.Y + 1));
                }
            }
            return list;
        }

        private static bool HasNumericTextInRect(List<TextBoxInfo> boxes, double minX, double maxX, double minY, double maxY)
        {
            foreach (var b in boxes)
            {
                if (b.MaxX >= minX && b.MinX <= maxX && b.MaxY >= minY && b.MinY <= maxY)
                    return true;
            }
            return false;
        }

        private static bool HasNumericTextNearBeam(
            List<TextBoxInfo> boxes,
            double px, double py,
            double ux, double uy,
            double nx, double ny,
            double tm, double hl,
            double cs, double hw)
        {
            foreach (var b in boxes)
            {
                double minT = double.MaxValue, maxT = double.MinValue;
                double minS = double.MaxValue, maxS = double.MinValue;
                for (var cIdx = 0; cIdx < 4; cIdx++)
                {
                    var wx = (cIdx & 1) == 0 ? b.MinX : b.MaxX;
                    var wy = (cIdx & 2) == 0 ? b.MinY : b.MaxY;
                    var rx = wx - px;
                    var ry = wy - py;
                    var t = rx * ux + ry * uy;
                    var s = rx * nx + ry * ny;
                    if (t < minT) minT = t;
                    if (t > maxT) maxT = t;
                    if (s < minS) minS = s;
                    if (s > maxS) maxS = s;
                }
                if (maxT >= tm - hl && minT <= tm + hl && maxS >= cs - hw && minS <= cs + hw)
                    return true;
            }
            return false;
        }

        private sealed class BeamLineRec
        {
            public double Px, Py, Qx, Qy, Ux, Uy, Len, Elev;
            public string Layer = "";
            public ObjectId Id;

            public static BeamLineRec? TryCreate(Point3d p, Point3d q, string layer, ObjectId id)
            {
                var dx = q.X - p.X;
                var dy = q.Y - p.Y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len <= 1e-6) return null;

                var th = Math.Atan2(dy, dx);
                if (th > Math.PI / 2.0 + 0.0175 || th <= -Math.PI / 2.0 + 0.0175)
                {
                    var tmp = p; p = q; q = tmp;
                    dx = -dx; dy = -dy;
                }

                return new BeamLineRec
                {
                    Px = p.X, Py = p.Y,
                    Qx = q.X, Qy = q.Y,
                    Ux = dx / len, Uy = dy / len,
                    Len = len,
                    Elev = p.Z,
                    Layer = layer,
                    Id = id
                };
            }

            public double AngleDeg()
            {
                var a = 180.0 / Math.PI * Math.Atan2(Uy, Ux);
                return a < 0 ? a + 180.0 : a;
            }
        }

        private static bool TryRelate(BeamLineRec a, BeamLineRec b, double tolSin, out double tlo, out double thi, out double signedDist)
        {
            tlo = thi = signedDist = 0;
            var cr = a.Ux * b.Uy - a.Uy * b.Ux;
            if (Math.Abs(cr) >= tolSin) return false;

            var s0 = a.Ux * (b.Py - a.Py) - a.Uy * (b.Px - a.Px);
            var s1 = a.Ux * (b.Qy - a.Py) - a.Uy * (b.Qx - a.Px);
            var t0 = a.Ux * (b.Px - a.Px) + a.Uy * (b.Py - a.Py);
            var t1 = a.Ux * (b.Qx - a.Px) + a.Uy * (b.Qy - a.Py);

            tlo = Math.Max(0.0, Math.Min(t0, t1));
            thi = Math.Min(a.Len, Math.Max(t0, t1));
            if (thi <= tlo) return false;

            var f = Math.Abs(t1 - t0) > 1e-9 ? ((tlo + thi) * 0.5 - t0) / (t1 - t0) : 0.0;
            signedDist = s0 + f * (s1 - s0);
            return true;
        }

        private static bool HasLineBetween(
            BeamLineRec a, BeamLineRec b, List<BeamLineRec> recs, double tolSin,
            double tlo, double thi, double s, double d, double ov)
        {
            foreach (var c in recs)
            {
                if (ReferenceEquals(c, a) || ReferenceEquals(c, b)) continue;
                if (!TryRelate(a, c, tolSin, out var cLo, out var cHi, out var cs)) continue;
                if (cs * s > 0 && Math.Abs(cs) > 1.0 && Math.Abs(cs) < d - 1.0 &&
                    (Math.Min(thi, cHi) - Math.Max(tlo, cLo)) > 0.2 * ov)
                    return true;
            }
            return false;
        }

        private static Point2d MidPoint(BeamLineRec a, double tlo, double thi, double s)
        {
            var tm = (tlo + thi) * 0.5;
            return new Point2d(a.Px + a.Ux * tm - a.Uy * (s * 0.5), a.Py + a.Uy * tm + a.Ux * (s * 0.5));
        }

        private static string TryWriteLog(string fileName, List<string> lines)
        {
            var path = Path.Combine(RuntimeLogger.LogDirectory, fileName);
            try
            {
                Directory.CreateDirectory(RuntimeLogger.LogDirectory);
                File.WriteAllLines(path, lines, new UTF8Encoding(false));
                return path;
            }
            catch (IOException ex)
            {
                RuntimeLogger.Error("标注日志写入失败。", ex);
                return "";
            }
            catch (UnauthorizedAccessException ex)
            {
                RuntimeLogger.Error("标注日志写入失败。", ex);
                return "";
            }
        }
    }
}
