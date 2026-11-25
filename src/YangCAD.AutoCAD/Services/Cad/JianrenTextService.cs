using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static partial class JianrenTextService
    {
        private static readonly Regex TrailingOrLastNumberRegex = new Regex(@"(?:(?<![\p{L}\p{N}])-)?\d+", RegexOptions.Compiled);
        private static readonly Regex MTextFormatControlRegex = new Regex(@"\\[Pp]", RegexOptions.Compiled);

        // ---------- 1. 视觉包围盒对齐 (L/C/R/T/B/V) ----------
        public static void RunBoxAlign(Document doc, string mode)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var ids = GetOrPromptTextEntities(ed, "TEXT,MTEXT", "\n选择需要对齐的文字(可先选后点)：");
            if (ids == null || ids.Length < 2)
            {
                if (ids != null && ids.Length == 1) ed.WriteMessage("\n至少需要选择 2 个文字才能对齐。");
                return;
            }

            var moved = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var items = new List<(Entity Ent, Extents3d Ext)>();
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (!(ent is DBText || ent is MText)) continue;
                    if (IsLayerLocked(tr, lt, ent.Layer)) continue;
                    if (!TryGetExtents(ent, out var ext)) continue;
                    items.Add((ent, ext));
                }

                if (items.Count < 2)
                {
                    ed.WriteMessage("\n未找到足够的未锁定文字。");
                    tr.Commit();
                    return;
                }

                double target = mode switch
                {
                    "L" => items.Min(x => x.Ext.MinPoint.X),
                    "R" => items.Max(x => x.Ext.MaxPoint.X),
                    "C" => items.OrderByDescending(x => x.Ext.MaxPoint.Y).Select(x => (x.Ext.MinPoint.X + x.Ext.MaxPoint.X) * 0.5).First(),
                    "T" => items.Max(x => x.Ext.MaxPoint.Y),
                    "B" => items.Min(x => x.Ext.MinPoint.Y),
                    "V" => items.Average(x => (x.Ext.MinPoint.Y + x.Ext.MaxPoint.Y) * 0.5),
                    _ => items.Min(x => x.Ext.MinPoint.X)
                };

                foreach (var (ent, ext) in items)
                {
                    double dx = 0, dy = 0;
                    switch (mode)
                    {
                        case "L": dx = target - ext.MinPoint.X; break;
                        case "R": dx = target - ext.MaxPoint.X; break;
                        case "C": dx = target - (ext.MinPoint.X + ext.MaxPoint.X) * 0.5; break;
                        case "T": dy = target - ext.MaxPoint.Y; break;
                        case "B": dy = target - ext.MinPoint.Y; break;
                        case "V": dy = target - (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5; break;
                    }
                    if (Math.Abs(dx) > 1e-6 || Math.Abs(dy) > 1e-6)
                    {
                        ent.UpgradeOpen();
                        ent.TransformBy(Matrix3d.Displacement(new Vector3d(dx, dy, 0)));
                        moved++;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[文字对齐] 已精确对齐 {moved} 个文字。");
        }

        public static void RunAlignAlongCurve(Document doc)
        {
            var ed = doc.Editor;
            var options = new PromptEntityOptions("\n点选直线、多段线、圆或圆弧：");
            options.SetRejectMessage("\n请选择直线、多段线、圆或圆弧。");
            foreach (var type in new[] { typeof(Line), typeof(Polyline), typeof(Circle), typeof(Arc) })
                options.AddAllowedClass(type, true);
            var pick = ed.GetEntity(options);
            if (pick.Status != PromptStatus.OK) return;

            var kind = pick.ObjectId.ObjectClass.DxfName;
            if (kind == "CIRCLE" || kind == "ARC")
                RunAlignToArc(doc, pick.ObjectId);
            else
                RunAlignToLine(doc, pick);
        }

        public static void RunTextSplitJoin(Document doc)
        {
            var ed = doc.Editor;
            var ids = GetOrPromptTextEntities(ed, "TEXT,MTEXT", "\n选择要拆合的文字：");
            if (ids == null || ids.Length == 0) return;

            var singles = ids.Where(id => id.ObjectClass.DxfName == "TEXT").ToArray();
            var multis = ids.Where(id => id.ObjectClass.DxfName == "MTEXT").ToArray();
            if (singles.Length == 0)
            {
                RunMultiToSingle(doc, multis);
                return;
            }
            if (singles.Length == 1 && multis.Length == 0)
            {
                RunExplodeToChars(doc, singles);
                return;
            }

            var options = new PromptKeywordOptions("\n文字拆合：");
            if (singles.Length > 1) options.Keywords.Add("J", "J", "连接成一行(J)");
            options.Keywords.Add("M", "M", "合并为多行(M)");
            if (multis.Length > 0) options.Keywords.Add("S", "S", "将多行拆为单行(S)");
            if (singles.Length == 1) options.Keywords.Add("E", "E", "拆成单字(E)");
            var mode = ed.GetKeywords(options);
            if (mode.Status != PromptStatus.OK) return;
            switch (mode.StringResult)
            {
                case "J": RunJoinText(doc, singles); break;
                case "M": RunSingleToMulti(doc, singles); break;
                case "S": RunMultiToSingle(doc, multis); break;
                case "E": RunExplodeToChars(doc, singles); break;
            }
        }

        // ---------- 2. 按线对齐 ----------
        public static void RunAlignToLine(Document doc, PromptEntityResult? reference = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var ids = GetOrPromptTextEntities(
                ed,
                "TEXT,MTEXT",
                "\n选择需要按线对齐的文字：");
            if (ids == null || ids.Length == 0) return;

            Point3d pickPt;
            double segAngle;
            Point3d segPoint;

            while (true)
            {
                var peo = new PromptEntityOptions($"\n点选参照直线/多段线 [改偏移距(O)={settings.TextLineAlignOffset:0.#}mm]：");
                peo.Keywords.Add("O", "O", "偏移距(O)");
                var per = reference ?? ed.GetEntity(peo);
                reference = null;
                if (per.Status == PromptStatus.Cancel || per.Status == PromptStatus.None) return;
                if (per.Status == PromptStatus.Keyword)
                {
                    settings.TextLineAlignOffset = AskDouble(ed, "文字距参照线的法向偏移(mm)", settings.TextLineAlignOffset, -1e5, 1e5);
                    settings.Save();
                    continue;
                }

                if (per.Status != PromptStatus.OK) continue;
                pickPt = per.PickedPoint;

                var ok = false;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                    if (ent != null)
                    {
                        var closest = ent.GetClosestPointTo(pickPt, false);
                        var deriv = ent.GetFirstDerivative(closest);
                        if (deriv.Length > 1e-9)
                        {
                            segAngle = MakeReadableAngle(Math.Atan2(deriv.Y, deriv.X));
                            segPoint = closest;
                            ok = true;
                        }
                        else
                        {
                            segAngle = 0;
                            segPoint = closest;
                        }
                    }
                    else
                    {
                        segAngle = 0;
                        segPoint = pickPt;
                    }
                    tr.Commit();
                }

                if (ok) break;
                ed.WriteMessage("\n请点选直线、多段线或圆弧等曲线对象。");
            }

            var ux = Math.Cos(segAngle);
            var uy = Math.Sin(segAngle);
            var nx = -uy;
            var ny = ux;
            var offset = settings.TextLineAlignOffset;
            var count = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (!(ent is DBText || ent is MText)) continue;
                    if (IsLayerLocked(tr, lt, ent.Layer)) continue;

                    var oldCenter = TryGetCenter(ent) ?? GetAnchor(ent);
                    var proj = (oldCenter.X - segPoint.X) * ux + (oldCenter.Y - segPoint.Y) * uy;
                    var tgt = new Point3d(segPoint.X + ux * proj + nx * offset, segPoint.Y + uy * proj + ny * offset, oldCenter.Z);

                    ent.UpgradeOpen();
                    if (ent is DBText dt) dt.Rotation = segAngle;
                    else if (ent is MText mt) mt.Rotation = segAngle;

                    var newCenter = TryGetCenter(ent) ?? GetAnchor(ent);
                    ent.TransformBy(Matrix3d.Displacement(tgt - newCenter));
                    count++;
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTLINE] 已按线方向({segAngle * 180.0 / Math.PI:0.0}°)对齐 {count} 个文字。");
        }

        // ---------- 3. 按弧环绕文字 ----------
        public static void RunAlignToArc(Document doc, ObjectId pickedArc = default)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var ids = GetOrPromptTextEntities(
                ed,
                "TEXT,MTEXT",
                "\n选择需要沿弧环绕的文字(可选 1 句自动拆字，或选多个单字)：");
            if (ids == null || ids.Length == 0) return;

            ObjectId arcId = pickedArc;
            while (arcId.IsNull)
            {
                var peo = new PromptEntityOptions($"\n点选圆弧或圆 [朝向={(settings.TextArcInward ? "向心(I)" : "背心(O)")}/偏移={settings.TextArcOffset:0.#}(D)]：");
                peo.Keywords.Add("I", "I", "向心(I)");
                peo.Keywords.Add("O", "O", "背心(O)");
                peo.Keywords.Add("D", "D", "偏移(D)");
                var per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.Cancel || per.Status == PromptStatus.None) return;
                if (per.Status == PromptStatus.Keyword)
                {
                    var key = per.StringResult.ToUpperInvariant();
                    if (key == "I") settings.TextArcInward = true;
                    else if (key == "O") settings.TextArcInward = false;
                    else if (key == "D") settings.TextArcOffset = AskDouble(ed, "相对圆弧半径偏移(mm, +向外/-向内)", settings.TextArcOffset, -1e5, 1e5);
                    settings.Save();
                    continue;
                }

                if (per.Status != PromptStatus.OK) continue;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (ent is Arc || ent is Circle)
                        arcId = per.ObjectId;
                    else
                        ed.WriteMessage("\n请点选圆弧(ARC)或圆(CIRCLE)。");
                    tr.Commit();
                }
            }

            var inward = settings.TextArcInward;
            var radOffset = settings.TextArcOffset;
            var placed = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var curve = tr.GetObject(arcId, OpenMode.ForRead);
                Point3d center;
                double radius, startAngle, endAngle, elev;

                if (curve is Arc arc)
                {
                    center = arc.Center;
                    radius = arc.Radius + radOffset;
                    startAngle = arc.StartAngle;
                    endAngle = arc.EndAngle;
                    elev = arc.Center.Z;
                    while (endAngle <= startAngle) endAngle += 2.0 * Math.PI;
                }
                else if (curve is Circle circle)
                {
                    center = circle.Center;
                    radius = circle.Radius + radOffset;
                    startAngle = Math.PI * 0.85;
                    endAngle = Math.PI * 0.15 + 2.0 * Math.PI;
                    elev = circle.Center.Z;
                }
                else
                {
                    tr.Commit();
                    return;
                }

                if (radius <= 1.0) radius = 1.0;

                var firstEnt = tr.GetObject(ids[0], OpenMode.ForRead) as Entity;
                if (ids.Length == 1 && firstEnt != null)
                {
                    if (IsOnLockedLayer(tr, firstEnt)) return;
                    var rawText = ExtractPlainText(firstEnt);
                    if (string.IsNullOrEmpty(rawText))
                    {
                        tr.Commit();
                        return;
                    }

                    var chars = rawText.ToCharArray();
                    var height = firstEnt is DBText dt ? dt.Height : (firstEnt is MText mt ? mt.TextHeight : 250.0);
                    var styleId = firstEnt is DBText dt2 ? dt2.TextStyleId : (firstEnt is MText mt2 ? mt2.TextStyleId : db.Textstyle);
                    var layer = firstEnt.Layer;

                    var span = endAngle - startAngle;
                    if (curve is Circle)
                    {
                        var charStepAng = (height * 1.15) / radius;
                        var totalSpan = Math.Min(Math.PI * 1.8, charStepAng * Math.Max(1, chars.Length - 1));
                        startAngle = Math.PI * 0.5 + totalSpan * 0.5;
                        endAngle = Math.PI * 0.5 - totalSpan * 0.5;
                        span = endAngle - startAngle;
                    }
                    else
                    {
                        // Top arc usually reads from left (larger angle) to right (smaller angle)
                        var tmp = startAngle;
                        startAngle = endAngle;
                        endAngle = tmp;
                        span = endAngle - startAngle;
                    }

                    var margin = chars.Length > 1 ? 0.08 * span : 0.5 * span;
                    var usableSpan = span - 2.0 * margin;
                    for (var i = 0; i < chars.Length; i++)
                    {
                        var t = chars.Length == 1 ? 0.5 : (double)i / (chars.Length - 1);
                        var theta = chars.Length == 1 ? (startAngle + endAngle) * 0.5 : (startAngle + margin + usableSpan * t);
                        var pt = new Point3d(center.X + radius * Math.Cos(theta), center.Y + radius * Math.Sin(theta), elev);
                        var rot = inward ? theta - Math.PI * 0.5 : theta + Math.PI * 0.5;

                        var chText = new DBText
                        {
                            Position = pt,
                            AlignmentPoint = pt,
                            HorizontalMode = TextHorizontalMode.TextCenter,
                            VerticalMode = TextVerticalMode.TextVerticalMid,
                            Height = height,
                            Rotation = rot,
                            TextString = chars[i].ToString(),
                            TextStyleId = styleId,
                            Layer = layer
                        };
                        space.AppendEntity(chText);
                        tr.AddNewlyCreatedDBObject(chText, true);
                        chText.AdjustAlignment(db);
                        placed++;
                    }

                    firstEnt.UpgradeOpen();
                    firstEnt.Erase();
                }
                else
                {
                    var textEnts = ids
                        .Select(id => tr.GetObject(id, OpenMode.ForRead))
                        .OfType<Entity>()
                        .Where(e => (e is DBText || e is MText) && !IsOnLockedLayer(tr, e))
                        .OrderBy(e => TryGetCenter(e)?.X ?? GetAnchor(e).X)
                        .ToList();

                    double a0 = endAngle, a1 = startAngle;
                    var span = a1 - a0;
                    var margin = textEnts.Count > 1 ? 0.08 * span : 0.5 * span;
                    var usable = span - 2.0 * margin;

                    for (var i = 0; i < textEnts.Count; i++)
                    {
                        var ent = textEnts[i]!;
                        var t = textEnts.Count == 1 ? 0.5 : (double)i / (textEnts.Count - 1);
                        var theta = textEnts.Count == 1 ? (a0 + a1) * 0.5 : (a0 + margin + usable * t);
                        var pt = new Point3d(center.X + radius * Math.Cos(theta), center.Y + radius * Math.Sin(theta), elev);
                        var rot = inward ? theta - Math.PI * 0.5 : theta + Math.PI * 0.5;

                        ent.UpgradeOpen();
                        if (ent is DBText dt) dt.Rotation = rot;
                        else if (ent is MText mt) mt.Rotation = rot;

                        var cur = TryGetCenter(ent) ?? GetAnchor(ent);
                        ent.TransformBy(Matrix3d.Displacement(pt - cur));
                        placed++;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTARC] 已沿圆弧排布 {placed} 个字符/文字。");
        }

        // ---------- 4. 智能递增复制 ----------
        public static void RunIncrementCopy(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var peo = new PromptEntityOptions("\n选择要递增复制的源文字或属性块：");
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string sourceText = "";
            bool isBlockAttr = false;
            Point3d basePt;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                if (ent is DBText dt)
                {
                    sourceText = dt.TextString;
                    basePt = dt.Position;
                }
                else if (ent is MText mt)
                {
                    sourceText = mt.Contents;
                    basePt = mt.Location;
                }
                else if (ent is BlockReference br && br.AttributeCollection.Count > 0)
                {
                    isBlockAttr = true;
                    var att = tr.GetObject(br.AttributeCollection[0], OpenMode.ForRead) as AttributeReference;
                    sourceText = att?.TextString ?? "";
                    basePt = br.Position;
                }
                else
                {
                    ed.WriteMessage("\n请选择单行文字、多行文字或带属性的图块。");
                    tr.Commit();
                    return;
                }
                tr.Commit();
            }

            var step = settings.TextIncStep;
            var skipZeroFloor = settings.TextIncSkipZeroFloor;
            var copied = 0;
            var index = 1;
            var baseUcs = basePt.TransformBy(ed.CurrentUserCoordinateSystem.Inverse());

            while (true)
            {
                var nextPreview = StepText(sourceText, step * index, skipZeroFloor);
                var ppo = new PromptPointOptions($"\n点击下一个放置点(将生成: {nextPreview}) [改增量(S)={step}/定距阵列(A)/跳0层(F)={(skipZeroFloor ? "开" : "关")}] <结束>：")
                {
                    UseBasePoint = true,
                    BasePoint = baseUcs,
                    AllowNone = true
                };
                ppo.Keywords.Add("S", "S", "改增量(S)");
                ppo.Keywords.Add("A", "A", "定距阵列(A)");
                ppo.Keywords.Add("F", "F", "跳0层(F)");

                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.Cancel || ppr.Status == PromptStatus.None) break;

                if (ppr.Status == PromptStatus.Keyword)
                {
                    var key = ppr.StringResult.ToUpperInvariant();
                    if (key == "S")
                    {
                        step = AskInt(ed, "递增步长(可为负数如 -1)", step, -1000, 1000);
                        if (step == 0) step = 1;
                        settings.TextIncStep = step;
                        settings.Save();
                    }
                    else if (key == "F")
                    {
                        skipZeroFloor = !skipZeroFloor;
                        settings.TextIncSkipZeroFloor = skipZeroFloor;
                        settings.Save();
                    }
                    else if (key == "A")
                    {
                        var dirPtRes = ed.GetPoint(new PromptPointOptions("\n点击阵列第 1 个目标位置(决定方向与间距)：") { UseBasePoint = true, BasePoint = baseUcs });
                        if (dirPtRes.Status != PromptStatus.OK) continue;
                        var count = AskInt(ed, "总共生成多少个递增副本", 5, 1, 200);
                        var delta = dirPtRes.Value.TransformBy(ed.CurrentUserCoordinateSystem) - basePt;
                        for (var k = 1; k <= count; k++)
                        {
                            var valText = StepText(sourceText, step * k, skipZeroFloor);
                            PlaceIncrementClone(db, per.ObjectId, isBlockAttr, delta * k, valText);
                            copied++;
                        }
                        break;
                    }
                    continue;
                }

                if (ppr.Status == PromptStatus.OK)
                {
                    var targetWcs = ppr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                    var nextStr = StepText(sourceText, step * index, skipZeroFloor);
                    PlaceIncrementClone(db, per.ObjectId, isBlockAttr, targetWcs - basePt, nextStr);
                    copied++;
                    index++;
                }
            }

            ed.WriteMessage($"\n[YGINC] 共生成 {copied} 个递增副本。");
        }

        private static void PlaceIncrementClone(Database db, ObjectId srcId, bool isBlockAttr, Vector3d displacement, string newText)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var src = (Entity)tr.GetObject(srcId, OpenMode.ForRead);
                var clone = (Entity)src.Clone();
                clone.TransformBy(Matrix3d.Displacement(displacement));

                if (clone is DBText dt)
                {
                    dt.TextString = newText;
                    space.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                }
                else if (clone is MText mt)
                {
                    mt.Contents = newText;
                    space.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                }
                else if (isBlockAttr && src is BlockReference srcBr && clone is BlockReference cloneBr)
                {
                    space.AppendEntity(cloneBr);
                    tr.AddNewlyCreatedDBObject(cloneBr, true);
                    var srcBt = (BlockTableRecord)tr.GetObject(srcBr.BlockTableRecord, OpenMode.ForRead);
                    var first = true;
                    foreach (ObjectId attDefId in srcBt)
                    {
                        if (!(tr.GetObject(attDefId, OpenMode.ForRead) is AttributeDefinition ad) || ad.Constant) continue;
                        var ar = new AttributeReference();
                        ar.SetAttributeFromBlock(ad, cloneBr.BlockTransform);
                        var matched = FindSrcAttrValue(tr, srcBr, ad.Tag);
                        ar.TextString = first ? newText : (matched ?? ad.TextString);
                        first = false;
                        cloneBr.AttributeCollection.AppendAttribute(ar);
                        tr.AddNewlyCreatedDBObject(ar, true);
                    }
                }
                tr.Commit();
            }
        }

        private static string? FindSrcAttrValue(Transaction tr, BlockReference br, string tag)
        {
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is AttributeReference ar &&
                    string.Equals(ar.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    return ar.TextString;
            }
            return null;
        }

        private static string StepText(string src, int offset, bool skipZeroFloor)
        {
            if (string.IsNullOrEmpty(src) || offset == 0) return src;
            var matches = TrailingOrLastNumberRegex.Matches(src);
            if (matches.Count > 0)
            {
                var m = matches[matches.Count - 1];
                if (int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
                {
                    var hasFloorTag = (m.Index + m.Length < src.Length) &&
                                      (src[m.Index + m.Length] == 'F' || src[m.Index + m.Length] == 'f' || src[m.Index + m.Length] == '层');
                    var next = AdvanceInteger(num, offset, skipZeroFloor && hasFloorTag);
                    var neg = next < 0;
                    var absOld = m.Value.TrimStart('-');
                    var nextStr = Math.Abs(next).ToString(CultureInfo.InvariantCulture);
                    if (absOld.Length > nextStr.Length && absOld.StartsWith("0", StringComparison.Ordinal))
                        nextStr = nextStr.PadLeft(absOld.Length, '0');
                    if (neg) nextStr = "-" + nextStr;
                    return src.Substring(0, m.Index) + nextStr + src.Substring(m.Index + m.Length);
                }
            }

            // Fallback: increment last ASCII letter
            var chars = src.ToCharArray();
            for (var i = chars.Length - 1; i >= 0; i--)
            {
                var c = chars[i];
                if (c >= 'A' && c <= 'Z')
                {
                    var code = ((c - 'A' + offset) % 26 + 26) % 26;
                    chars[i] = (char)('A' + code);
                    return new string(chars);
                }
                if (c >= 'a' && c <= 'z')
                {
                    var code = ((c - 'a' + offset) % 26 + 26) % 26;
                    chars[i] = (char)('a' + code);
                    return new string(chars);
                }
            }
            return src;
        }

        private static int AdvanceInteger(int cur, int offset, bool skipZero)
        {
            if (!skipZero) return cur + offset;
            var stepDir = offset > 0 ? 1 : -1;
            var remain = Math.Abs(offset);
            var val = cur;
            while (remain > 0)
            {
                val += stepDir;
                if (val == 0) val += stepDir;
                remain--;
            }
            return val;
        }

        // ---------- 5. 连接文字 / 单转多 / 多转单 / 炸碎 ----------
        public static void RunJoinText(Document doc, ObjectId[]? selected = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var ids = selected ?? GetOrPromptTextEntities(
                ed,
                "TEXT",
                "\n选择要按顺序连接为一句的单行文字：");
            if (ids == null || ids.Length < 2) return;

            var joined = "";
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var texts = ids.Select(id => tr.GetObject(id, OpenMode.ForRead))
                    .OfType<DBText>()
                    .Where(t => !IsOnLockedLayer(tr, t))
                    .ToList();
                if (texts.Count < 2) { tr.Commit(); return; }

                var refAng = texts[0].Rotation;
                var ux = Math.Cos(refAng);
                var uy = Math.Sin(refAng);
                texts.Sort((a, b) =>
                {
                    var pa = a.Position.X * ux + a.Position.Y * uy;
                    var pb = b.Position.X * ux + b.Position.Y * uy;
                    return pa.CompareTo(pb);
                });

                joined = string.Concat(texts.Select(t => t.TextString ?? ""));
                texts[0].UpgradeOpen();
                texts[0].TextString = joined;
                foreach (var source in texts.Skip(1))
                {
                    source.UpgradeOpen();
                    source.Erase();
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTJOIN] 已合并为：{joined}");
        }

        public static void RunSingleToMulti(Document doc, ObjectId[]? selected = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var ids = selected ?? GetOrPromptTextEntities(
                ed,
                "TEXT",
                "\n框选要转为多行文字(MTEXT)的单行文字：");
            if (ids == null || ids.Length == 0) return;

            int lineCount;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var texts = ids.Select(id => tr.GetObject(id, OpenMode.ForRead))
                    .OfType<DBText>()
                    .Where(t => !IsOnLockedLayer(tr, t))
                    .ToList();
                if (texts.Count == 0) { tr.Commit(); return; }

                var ang = texts[0].Rotation;
                var ux = Math.Cos(ang);
                var uy = Math.Sin(ang);
                var nx = -uy;
                var ny = ux;
                // Sort top-to-bottom along local Y descending, then left-to-right
                texts.Sort((a, b) =>
                {
                    var ya = a.Position.X * nx + a.Position.Y * ny;
                    var yb = b.Position.X * nx + b.Position.Y * ny;
                    if (Math.Abs(ya - yb) > texts[0].Height * 0.45)
                        return yb.CompareTo(ya);
                    var xa = a.Position.X * ux + a.Position.Y * uy;
                    var xb = b.Position.X * ux + b.Position.Y * uy;
                    return xa.CompareTo(xb);
                });

                var top = texts[0];
                var content = string.Join(@"\P", texts.Select(t => t.TextString ?? ""));
                lineCount = texts.Count;

                var loc = top.Position;
                if (TryGetExtents(top, out var ext))
                    loc = new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, top.Position.Z);

                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var mt = new MText
                {
                    Location = loc,
                    Attachment = AttachmentPoint.TopLeft,
                    TextHeight = top.Height,
                    Rotation = ang,
                    TextStyleId = top.TextStyleId,
                    Layer = top.Layer,
                    Color = top.Color,
                    Contents = content
                };
                space.AppendEntity(mt);
                tr.AddNewlyCreatedDBObject(mt, true);

                foreach (var t in texts)
                {
                    t.UpgradeOpen();
                    t.Erase();
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTS2M] 已将 {lineCount} 条单行文字合并为 1 个 MTEXT。");
        }

        public static void RunMultiToSingle(Document doc, ObjectId[]? selected = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var ids = selected ?? GetOrPromptTextEntities(
                ed,
                "MTEXT",
                "\n选择要拆分为单行文字的多行文字(MTEXT)：");
            if (ids == null || ids.Length == 0) return;

            var created = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is MText mt) || IsOnLockedLayer(tr, mt)) continue;
                    var raw = MTextFormatControlRegex.Replace(mt.Contents ?? "", "\n").Replace("\r\n", "\n");
                    var lines = raw.Split(new[] { '\n' }, StringSplitOptions.None);
                    var ang = mt.Rotation;
                    var sp = mt.TextHeight * 1.5;
                    var dx = Math.Sin(ang);
                    var dy = -Math.Cos(ang);
                    var topPt = TryGetExtents(mt, out var ext)
                        ? new Point3d(ext.MinPoint.X, ext.MaxPoint.Y - mt.TextHeight, mt.Location.Z)
                        : mt.Location;

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var lineStr = StripMTextInlineCodes(lines[i]);
                        var pt = new Point3d(topPt.X + i * sp * dx, topPt.Y + i * sp * dy, topPt.Z);
                        var dt = new DBText
                        {
                            Position = pt,
                            Height = mt.TextHeight,
                            Rotation = ang,
                            TextString = lineStr,
                            TextStyleId = mt.TextStyleId,
                            Layer = mt.Layer,
                            Color = mt.Color
                        };
                        space.AppendEntity(dt);
                        tr.AddNewlyCreatedDBObject(dt, true);
                        created++;
                    }
                    mt.UpgradeOpen();
                    mt.Erase();
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTM2S] 已拆分为 {created} 条单行文字。");
        }

        public static void RunExplodeToChars(Document doc, ObjectId[]? selected = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var ids = selected ?? GetOrPromptTextEntities(
                ed,
                "TEXT",
                "\n选择要炸碎为单个字符的单行文字：");
            if (ids == null || ids.Length == 0) return;

            var totalChars = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is DBText dt) || IsOnLockedLayer(tr, dt)) continue;
                    var text = dt.TextString ?? "";
                    if (text.Length <= 1) continue;

                    var weights = text.Select(c => c > 127 ? 1.0 : 0.62).ToArray();
                    var sumWeight = weights.Sum();
                    if (sumWeight <= 0) sumWeight = 1.0;

                    var ang = dt.Rotation;
                    var ux = Math.Cos(ang);
                    var uy = Math.Sin(ang);

                    var totalLen = dt.Height * 0.9 * sumWeight;
                    var basePos = dt.Position;
                    if (TryGetExtents(dt, out var ext))
                    {
                        var diag = new Vector3d(ext.MaxPoint.X - ext.MinPoint.X, ext.MaxPoint.Y - ext.MinPoint.Y, 0);
                        var projLen = Math.Abs(diag.X * ux + diag.Y * uy);
                        if (projLen > dt.Height * 0.3) totalLen = projLen;

                        var minProj = double.MaxValue;
                        for (var k = 0; k < 4; k++)
                        {
                            var cx = (k & 1) == 0 ? ext.MinPoint.X : ext.MaxPoint.X;
                            var cy = (k & 2) == 0 ? ext.MinPoint.Y : ext.MaxPoint.Y;
                            var p = cx * ux + cy * uy;
                            if (p < minProj)
                            {
                                minProj = p;
                                basePos = new Point3d(cx, cy, dt.Position.Z);
                            }
                        }
                        // Project bottom-left baseline point onto baseline passing through dt.Position
                        var nx = -uy;
                        var ny = ux;
                        var anchor = GetAnchor(dt);
                        var baselineS = anchor.X * nx + anchor.Y * ny;
                        var startS = basePos.X * nx + basePos.Y * ny;
                        basePos = new Point3d(basePos.X + (baselineS - startS) * nx, basePos.Y + (baselineS - startS) * ny, dt.Position.Z);
                    }

                    double acc = 0;
                    for (var i = 0; i < text.Length; i++)
                    {
                        var adv = totalLen * (acc / sumWeight);
                        acc += weights[i];
                        var pt = new Point3d(basePos.X + adv * ux, basePos.Y + adv * uy, dt.Position.Z);
                        var ch = new DBText
                        {
                            Position = pt,
                            Height = dt.Height,
                            WidthFactor = dt.WidthFactor,
                            Rotation = ang,
                            TextString = text[i].ToString(),
                            TextStyleId = dt.TextStyleId,
                            Layer = dt.Layer,
                            Color = dt.Color
                        };
                        space.AppendEntity(ch);
                        tr.AddNewlyCreatedDBObject(ch, true);
                        totalChars++;
                    }
                    dt.UpgradeOpen();
                    dt.Erase();
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTEXP] 已炸碎为 {totalChars} 个单字。");
        }

        // ---------- 6. 去空格 / 去换行 / 去多余空格 / 去多余换行 ----------
        // ---------- 10. 跨实体 刷内容 / 换内容 / 改特性 ----------
        public static void RunContentBrush(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var srcPick = ed.GetNestedEntity("\n点选源文字(支持 TEXT/MTEXT/块属性/引线/标注)：");
            if (srcPick.Status != PromptStatus.OK) return;

            string? srcContent = null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(srcPick.ObjectId, OpenMode.ForRead) as Entity;
                srcContent = ent != null ? ReadAnyTextContent(tr, ent) : null;
                if (srcContent == null && ent is Dimension dim)
                {
                    srcContent = dim.Measurement.ToString("0.##", CultureInfo.InvariantCulture);
                }
                tr.Commit();
            }

            if (srcContent == null)
            {
                ed.WriteMessage("\n源对象不包含可读取的文字内容。");
                return;
            }

            var brushed = 0;
            while (true)
            {
                var tgtPick = ed.GetNestedEntity($"\n点选目标文字/属性/引线/标注(将刷为 \"{Truncate(srcContent, 24)}\") <退出>：");
                if (tgtPick.Status != PromptStatus.OK) break;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (tr.GetObject(tgtPick.ObjectId, OpenMode.ForRead) is Entity ent && !IsLayerLocked(tr, lt, ent.Layer))
                    {
                        if (WriteAnyTextContent(tr, ent, srcContent)) brushed++;
                    }
                    tr.Commit();
                }
            }

            ed.WriteMessage($"\n[YGTBRUSH] 共刷入 {brushed} 个目标文字。");
        }

        public static void RunContentSwap(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var p1 = ed.GetNestedEntity("\n点选第 1 个文字/属性/引线/标注：");
            if (p1.Status != PromptStatus.OK) return;
            var p2 = ed.GetNestedEntity("\n点选第 2 个要交换内容的文字/属性/引线/标注：");
            if (p2.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var e1 = tr.GetObject(p1.ObjectId, OpenMode.ForRead) as Entity;
                var e2 = tr.GetObject(p2.ObjectId, OpenMode.ForRead) as Entity;
                if (e1 == null || e2 == null) { tr.Commit(); return; }

                var s1 = ReadAnyTextContent(tr, e1);
                var s2 = ReadAnyTextContent(tr, e2);
                if (s1 == null || s2 == null)
                {
                    ed.WriteMessage("\n所选对象之一不包含可交换的文字内容。");
                    tr.Commit();
                    return;
                }

                if (IsOnLockedLayer(tr, e1) || IsOnLockedLayer(tr, e2) ||
                    !WriteAnyTextContent(tr, e1, s2) || !WriteAnyTextContent(tr, e2, s1))
                {
                    ed.WriteMessage("\n[YGTSWAP] 有对象无法写入，已保留原内容。");
                    return;
                }
                tr.Commit();
                ed.WriteMessage($"\n[YGTSWAP] 已互换内容：\"{Truncate(s1, 18)}\" ⇄ \"{Truncate(s2, 18)}\"");
            }
        }

        public static void RunPropertyBrush(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var srcPick = ed.GetEntity("\n点选样式样本文字(TEXT/MTEXT)：");
            if (srcPick.Status != PromptStatus.OK) return;

            double height = 250, widthFactor = 1.0;
            ObjectId styleId = ObjectId.Null;
            string layer = "0";
            Autodesk.AutoCAD.Colors.Color color;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var src = tr.GetObject(srcPick.ObjectId, OpenMode.ForRead) as Entity;
                if (src is DBText dt)
                {
                    height = dt.Height;
                    widthFactor = dt.WidthFactor;
                    styleId = dt.TextStyleId;
                    layer = dt.Layer;
                    color = dt.Color;
                }
                else if (src is MText mt)
                {
                    height = mt.TextHeight;
                    styleId = mt.TextStyleId;
                    layer = mt.Layer;
                    color = mt.Color;
                }
                else
                {
                    ed.WriteMessage("\n请点选 TEXT 或 MTEXT 作为样本。");
                    tr.Commit();
                    return;
                }
                tr.Commit();
            }

            var ids = GetOrPromptTextEntities(ed, "TEXT,MTEXT", "\n框选需要统一字高/字体/图层/颜色的目标文字：");
            if (ids == null || ids.Length == 0) return;

            var count = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (IsLayerLocked(tr, lt, ent.Layer)) continue;
                    ent.UpgradeOpen();
                    ent.Layer = layer;
                    ent.Color = color;
                    if (ent is DBText dt)
                    {
                        dt.Height = height;
                        dt.WidthFactor = widthFactor;
                        if (!styleId.IsNull) dt.TextStyleId = styleId;
                        count++;
                    }
                    else if (ent is MText mt)
                    {
                        mt.TextHeight = height;
                        if (!styleId.IsNull) mt.TextStyleId = styleId;
                        count++;
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[YGTPROP] 已将 {count} 个文字统一为样本特性(字高={height:0.##}, 图层={layer})。");
        }

        // ---------- 11. 跑道框 / 带消隐遮罩(Wipeout)文字框 ----------
        public static void RunTextBoxAdvanced(Document doc, bool slotShape, bool withWipeout,
            ObjectId[]? selected = null, bool circle = false)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var ids = selected ?? GetOrPromptTextEntities(ed, "TEXT,MTEXT", $"\n选择需要加{(slotShape ? "跑道框" : "遮罩矩形框")}的文字(留白 {settings.TextFrameMarginMm:0.##}mm)：");
            if (ids == null || ids.Length == 0) return;

            var margin = settings.TextFrameMarginMm / AnnotationUtilitiesService.MillimetersPerUnit(db);
            var created = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var dot = withWipeout ? (DrawOrderTable)tr.GetObject(space.DrawOrderTableId, OpenMode.ForWrite) : null;
                var textTopIds = new ObjectIdCollection();

                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent) || IsOnLockedLayer(tr, ent)) continue;
                    if (!TryGetExtents(ent, out var ext)) continue;

                    var minX = ext.MinPoint.X - margin;
                    var maxX = ext.MaxPoint.X + margin;
                    var minY = ext.MinPoint.Y - margin;
                    var maxY = ext.MaxPoint.Y + margin;
                    var z = ext.MinPoint.Z;

                    var centerX = (minX + maxX) * 0.5;
                    var centerY = (minY + maxY) * 0.5;
                    var circleRadius = Math.Sqrt(Math.Pow((ext.MaxPoint.X - ext.MinPoint.X) * 0.5, 2) +
                                                 Math.Pow((ext.MaxPoint.Y - ext.MinPoint.Y) * 0.5, 2)) + margin;

                    if (withWipeout)
                    {
                        var woPts = circle ? BuildCircularWipeout(centerX, centerY, circleRadius) :
                            BuildWipeoutLoop(minX, maxX, minY, maxY, slotShape);
                        var wo = new Wipeout { Layer = ent.Layer };
                        wo.SetFrom(woPts, Vector3d.ZAxis);
                        if (z != 0) wo.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, z)));
                        space.AppendEntity(wo);
                        tr.AddNewlyCreatedDBObject(wo, true);
                        textTopIds.Add(ent.ObjectId);
                    }

                    if (circle)
                    {
                        var roundFrame = new Circle(new Point3d(centerX, centerY, z), Vector3d.ZAxis, circleRadius)
                        {
                            Layer = ent.Layer
                        };
                        space.AppendEntity(roundFrame);
                        tr.AddNewlyCreatedDBObject(roundFrame, true);
                        if (withWipeout) textTopIds.Add(roundFrame.ObjectId);
                        created++;
                        continue;
                    }

                    var pl = new Polyline { Closed = true, Elevation = z, Layer = ent.Layer };
                    if (slotShape)
                    {
                        var r = (maxY - minY) * 0.5;
                        var cy = (minY + maxY) * 0.5;
                        var leftX = minX;
                        var rightX = maxX;
                        pl.AddVertexAt(0, new Point2d(leftX, cy - r), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(1, new Point2d(rightX, cy - r), 1.0, 0.0, 0.0);
                        pl.AddVertexAt(2, new Point2d(rightX, cy + r), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(3, new Point2d(leftX, cy + r), 1.0, 0.0, 0.0);
                    }
                    else
                    {
                        pl.AddVertexAt(0, new Point2d(minX, minY), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(1, new Point2d(maxX, minY), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(2, new Point2d(maxX, maxY), 0.0, 0.0, 0.0);
                        pl.AddVertexAt(3, new Point2d(minX, maxY), 0.0, 0.0, 0.0);
                    }

                    space.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    if (withWipeout) textTopIds.Add(pl.ObjectId);
                    created++;
                }

                if (dot != null && textTopIds.Count > 0)
                    dot.MoveToTop(textTopIds);

                tr.Commit();
            }

            ed.WriteMessage($"\n[文字加框] 已为 {created} 个文字生成边框" + (withWipeout ? "(含背景消隐遮罩)。" : "。"));
        }

        private static Point2dCollection BuildCircularWipeout(double x, double y, double radius)
        {
            const int segments = 48;
            var points = new Point2dCollection();
            for (var i = 0; i < segments; i++)
            {
                var angle = i * 2 * Math.PI / segments;
                points.Add(new Point2d(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle)));
            }
            points.Add(points[0]);
            return points;
        }

        private static Point2dCollection BuildWipeoutLoop(double minX, double maxX, double minY, double maxY, bool slot)
        {
            var col = new Point2dCollection();
            if (!slot)
            {
                col.Add(new Point2d(minX, minY));
                col.Add(new Point2d(maxX, minY));
                col.Add(new Point2d(maxX, maxY));
                col.Add(new Point2d(minX, maxY));
                col.Add(new Point2d(minX, minY));
                return col;
            }

            var r = (maxY - minY) * 0.5;
            var cy = (minY + maxY) * 0.5;
            const int segs = 8;
            for (var i = 0; i <= segs; i++)
            {
                var a = -Math.PI * 0.5 + Math.PI * i / segs;
                col.Add(new Point2d(maxX + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            for (var i = 0; i <= segs; i++)
            {
                var a = Math.PI * 0.5 + Math.PI * i / segs;
                col.Add(new Point2d(minX + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            col.Add(col[0]);
            return col;
        }

        // ---------- 12. 文字排版综合参数设置 (Ribbon ↘ 入口) ----------
        public static void RunTextSettings(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            ed.WriteMessage($"\n[文字设置] 段落行距={settings.TextAlignSpacing:0.##}x | 沿线偏移={settings.TextLineAlignOffset:0.#}mm | 递增步长={settings.TextIncStep} | 加框留白={settings.TextFrameMarginMm:0.##}mm");

            settings.TextAlignSpacing = AskDouble(ed, "段落对齐行距倍数", settings.TextAlignSpacing, 0.5, 10.0);
            settings.TextLineAlignOffset = AskDouble(ed, "按线对齐偏移(mm)", settings.TextLineAlignOffset, -1e5, 1e5);
            var step = AskInt(ed, "递增复制默认步长", settings.TextIncStep, -1000, 1000);
            if (step != 0) settings.TextIncStep = step;
            settings.TextFrameMarginMm = AskDouble(ed, "文字加框默认留白(mm)", settings.TextFrameMarginMm, 0.0, 1e5);
            settings.Save();
            ed.WriteMessage("\n[YGTSET] 文字排版默认参数已保存。");
        }

        // ---------- Shared Helpers ----------
        private static string? ReadAnyTextContent(Transaction tr, Entity ent)
        {
            if (ent is DBText dt) return dt.TextString ?? "";
            if (ent is MText mt) return mt.Contents ?? "";
            if (ent is AttributeReference ar) return ar.TextString ?? "";
            if (ent is Dimension dim)
                return string.IsNullOrEmpty(dim.DimensionText) || dim.DimensionText == "<>"
                    ? null
                    : dim.DimensionText;
            if (ent is MLeader ml && ml.ContentType == ContentType.MTextContent) return ml.MText?.Contents ?? "";
            if (ent is BlockReference br && br.AttributeCollection.Count > 0)
                return (tr.GetObject(br.AttributeCollection[0], OpenMode.ForRead) as AttributeReference)?.TextString ?? "";
            return null;
        }

        private static bool WriteAnyTextContent(Transaction tr, Entity ent, string value)
        {
            try
            {
                ent.UpgradeOpen();
                if (ent is DBText dt) { dt.TextString = value; return true; }
                if (ent is MText mt) { mt.Contents = value; return true; }
                if (ent is AttributeReference ar) { ar.TextString = value; return true; }
                if (ent is Dimension dim) { dim.DimensionText = value; return true; }
                if (ent is MLeader ml && ml.ContentType == ContentType.MTextContent)
                {
                    var mlMt = ml.MText;
                    if (mlMt == null) return false;
                    mlMt.Contents = value;
                    ml.MText = mlMt;
                    return true;
                }
                if (ent is BlockReference br && br.AttributeCollection.Count > 0 &&
                    tr.GetObject(br.AttributeCollection[0], OpenMode.ForRead) is AttributeReference firstAtt &&
                    !IsOnLockedLayer(tr, firstAtt))
                {
                    firstAtt.UpgradeOpen();
                    firstAtt.TextString = value;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string ExtractPlainText(Entity ent)
        {
            if (ent is DBText dt) return dt.TextString ?? "";
            if (ent is MText mt) return mt.Text ?? "";
            return "";
        }

        private static string StripMTextInlineCodes(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var cleaned = Regex.Replace(s, @"\{\\[^;]+;([^\}]*)\}", "$1");
            return cleaned.Replace(@"\~", " ").Replace("{", "").Replace("}", "");
        }

        private static ObjectId[]? GetOrPromptTextEntities(Editor ed, string dxfTypes, string prompt)
        {
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                var acceptedTypes = dxfTypes.Split(',');
                var ids = implied.Value.GetObjectIds()
                    .Where(id => acceptedTypes.Contains(id.ObjectClass.DxfName, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                if (ids.Length > 0) return ids;
            }
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, dxfTypes) });
            var sr = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = prompt }, filter);
            if (sr.Status != PromptStatus.OK || sr.Value == null) return null;
            return sr.Value.GetObjectIds();
        }

        private static bool TryGetExtents(Entity ent, out Extents3d ext)
        {
            try { ext = ent.GeometricExtents; return true; }
            catch { ext = default; return false; }
        }

        private static Point3d? TryGetCenter(Entity ent)
        {
            if (!TryGetExtents(ent, out var ext)) return null;
            return new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) * 0.5,
                (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5,
                (ext.MinPoint.Z + ext.MaxPoint.Z) * 0.5);
        }

        private static Point3d GetAnchor(Entity ent)
        {
            if (ent is DBText dt)
                return dt.HorizontalMode != TextHorizontalMode.TextLeft || dt.VerticalMode != TextVerticalMode.TextBase
                    ? dt.AlignmentPoint : dt.Position;
            if (ent is MText mt) return mt.Location;
            return Point3d.Origin;
        }

        private static double MakeReadableAngle(double rad)
        {
            while (rad <= -Math.PI) rad += 2.0 * Math.PI;
            while (rad > Math.PI) rad -= 2.0 * Math.PI;
            if (rad > Math.PI * 0.5 + 1e-4) rad -= Math.PI;
            else if (rad <= -Math.PI * 0.5 + 1e-4) rad += Math.PI;
            return rad;
        }

        private static bool IsOnLockedLayer(Transaction tr, Entity entity)
            => ((LayerTableRecord)tr.GetObject(entity.LayerId, OpenMode.ForRead)).IsLocked;

        private static bool IsLayerLocked(Transaction tr, LayerTable lt, string layerName)
            => lt.Has(layerName) && ((LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead)).IsLocked;

        private static double AskDouble(Editor ed, string msg, double cur, double min, double max)
        {
            var pdo = new PromptDoubleOptions($"\n{msg} <{cur:0.##}>：")
            {
                DefaultValue = cur,
                UseDefaultValue = true,
                AllowNegative = min < 0,
                AllowZero = min <= 0 && max >= 0,
                AllowNone = true
            };
            var r = ed.GetDouble(pdo);
            return r.Status == PromptStatus.OK ? Math.Max(min, Math.Min(max, r.Value)) : cur;
        }

        private static int AskInt(Editor ed, string msg, int cur, int min, int max)
        {
            var pio = new PromptIntegerOptions($"\n{msg} <{cur}>：")
            {
                DefaultValue = cur,
                UseDefaultValue = true,
                AllowNegative = min < 0,
                AllowZero = min <= 0 && max >= 0,
                AllowNone = true
            };
            var r = ed.GetInteger(pio);
            return r.Status == PromptStatus.OK ? Math.Max(min, Math.Min(max, r.Value)) : cur;
        }

        private static string Truncate(string s, int maxLen) => s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }
}
