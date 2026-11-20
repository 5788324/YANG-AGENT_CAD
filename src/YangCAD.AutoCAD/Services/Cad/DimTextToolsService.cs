using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using CoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static class DimTextToolsService
    {
        public static void RunDimCheck(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION,TEXT,MTEXT") });

            ObjectId[]? ids = null;
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                ids = implied.Value.GetObjectIds();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
            }
            else
            {
                while (true)
                {
                    var modeLabel = settings.DimCheckMode == "1" ? "1-真实测量值" : "2-显示舍入值";
                    ed.WriteMessage($"\n[DIMCHK] 当前检查方式={modeLabel} | 基准倍数={settings.DimRoundStep:0.###}");
                    var key = "";
                    var opts = new PromptSelectionOptions { MessageForAdding = "\n选择要检查的标注或数字文字 [真实值(1)/显示值(2)/改参数(S)]：" };
                    opts.Keywords.Add("1", "1", "真实值(1)");
                    opts.Keywords.Add("2", "2", "显示值(2)");
                    opts.Keywords.Add("S", "S", "改参数(S)");
                    opts.KeywordInput += (_, args) =>
                    {
                        key = args.Input ?? "";
                        throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.OK);
                    };

                    PromptSelectionResult sel;
                    try { sel = ed.GetSelection(opts, filter); }
                    catch (Autodesk.AutoCAD.Runtime.Exception) when (!string.IsNullOrEmpty(key))
                    {
                        if (key == "1" || key == "2")
                        {
                            settings.DimCheckMode = key;
                            settings.Save();
                        }
                        else
                        {
                            RunDimSet(doc);
                            settings = UserSettings.Load();
                        }
                        continue;
                    }

                    if (sel.Status != PromptStatus.OK || sel.Value == null) return;
                    ids = sel.Value.GetObjectIds();
                    break;
                }
            }

            var step = settings.DimRoundStep <= 0 ? 5.0 : settings.DimRoundStep;
            var useDisp = settings.DimCheckMode == "2";
            var bad = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    var v = GetNumericValue(ent, useDisp);
                    if (!v.HasValue) continue;
                    if (OffsetFromStep(v.Value, step) > 1e-6) bad.Add(id);
                }
                tr.Commit();
            }

            if (bad.Count > 0)
            {
                ed.SetImpliedSelection(bad.ToArray());
                ed.WriteMessage($"\n[方式{settings.DimCheckMode}] 已高亮选中 {bad.Count} 个尾数不是 {step:0.##} 倍数的标注 (共检查 {ids.Length} 个)。");
            }
            else
            {
                ed.WriteMessage($"\n没有发现尾数不是 {step:0.##} 倍数的标注 (共检查 {ids.Length} 个)。");
            }
        }

        public static void RunDimFix(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION,TEXT,MTEXT") });
            var ids = GetOrPromptSelection(ed, filter, "\n选择需要取整修正的标注或数字文字：", "个对象");
            if (ids == null || ids.Length == 0) return;

            var step = settings.DimRoundStep <= 0 ? 5.0 : settings.DimRoundStep;
            var tol = settings.DimRoundTol;
            int cnt = 0, fail = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    var v = GetNumericValue(ent, false);
                    if (!v.HasValue) continue;
                    var target = NearestMultiple(v.Value, step);
                    var diff = Math.Abs(v.Value - target);
                    if (diff <= 1e-9 || diff > tol) continue;

                    if (IsLayerLocked(tr, lt, ent.Layer))
                    {
                        fail++;
                        continue;
                    }

                    try
                    {
                        ent.UpgradeOpen();
                        var text = FormatRound(target);
                        if (ent is Dimension dim) dim.DimensionText = text;
                        else if (ent is DBText dt) dt.TextString = text;
                        else if (ent is MText mt) mt.Contents = text;
                        cnt++;
                    }
                    catch
                    {
                        fail++;
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[DIMFIX] 已修改 {cnt} 个标注" + (fail > 0 ? $", 失败 {fail} 个(图层可能锁定)" : "") + "。");
        }

        public static void RunDimRestore(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
            var ids = GetOrPromptSelection(ed, filter, "\n选择要恢复真实测量值的标注：", "个标注");
            if (ids == null || ids.Length == 0) return;

            int cnt = 0, fail = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Dimension dim)) continue;
                    var ov = (dim.DimensionText ?? "").Trim();
                    if (string.IsNullOrEmpty(ov) || ov == "<>") continue;
                    if (IsLayerLocked(tr, lt, dim.Layer)) { fail++; continue; }
                    try
                    {
                        dim.UpgradeOpen();
                        dim.DimensionText = "";
                        cnt++;
                    }
                    catch { fail++; }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[DIMRE] 已恢复 {cnt} 个标注的原值" + (fail > 0 ? $", 失败 {fail} 个" : "") + "。");
        }

        public static void RunDimFlip(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });

            var mode = "A";
            var implied = ed.SelectImplied();
            ObjectId[]? ids = null;
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                ids = implied.Value.GetObjectIds();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
            }

            while (true)
            {
                var pko = new PromptKeywordOptions($"\n翻转方式 [A-只翻倒着的/B-全部翻转/R-切换参考({(settings.UpwardRef == "W" ? "世界" : "屏幕")})] <A>：", "A B R")
                {
                    AllowNone = true
                };
                pko.Keywords.Default = "A";
                var kr = ed.GetKeywords(pko);
                if (kr.Status == PromptStatus.Cancel) return;
                var input = string.IsNullOrEmpty(kr.StringResult) ? "A" : kr.StringResult.ToUpperInvariant();
                if (input == "R")
                {
                    settings.UpwardRef = settings.UpwardRef == "W" ? "S" : "W";
                    settings.Save();
                    continue;
                }
                mode = input;
                break;
            }

            if (ids == null || ids.Length == 0)
            {
                ids = GetOrPromptSelection(ed, filter, "\n选择需要翻转文字的标注：", "个标注");
                if (ids == null || ids.Length == 0) return;
            }

            var twist = settings.UpwardRef == "W" ? 0.0 : GetViewTwist();
            int cnt = 0, skip = 0, fail = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Dimension dim)) continue;
                    var res = TryFlipDim(tr, lt, dim, mode == "B", twist);
                    if (res == OpOutcome.Done) cnt++;
                    else if (res == OpOutcome.Skip) skip++;
                    else if (res == OpOutcome.Fail) fail++;
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[DIMFZ] 已翻转 {cnt} 个标注的文字" +
                            (skip > 0 ? $", 跳过 {skip} 个(类型不支持)" : "") +
                            (fail > 0 ? $", 失败 {fail} 个" : "") + "。");
        }

        public static void RunTextUp(Document doc, bool askRef = false)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION,TEXT,MTEXT,INSERT") });

            ObjectId[]? ids = null;
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                ids = implied.Value.GetObjectIds();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                ed.WriteMessage($"\n使用当前选中的 {ids.Length} 个对象。");
            }
            else if (askRef)
            {
                while (true)
                {
                    var key = "";
                    var opts = new PromptSelectionOptions
                    {
                        MessageForAdding = $"\n选择文字/标注/属性块(向上参考={(settings.UpwardRef == "W" ? "世界坐标" : "当前屏幕")}) [切换参考(R)]："
                    };
                    opts.Keywords.Add("R", "R", "切换参考(R)");
                    opts.KeywordInput += (_, args) =>
                    {
                        key = args.Input ?? "";
                        throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.OK);
                    };

                    PromptSelectionResult sr;
                    try { sr = ed.GetSelection(opts, filter); }
                    catch (Autodesk.AutoCAD.Runtime.Exception) when (!string.IsNullOrEmpty(key))
                    {
                        settings.UpwardRef = settings.UpwardRef == "W" ? "S" : "W";
                        settings.Save();
                        continue;
                    }

                    if (sr.Status != PromptStatus.OK || sr.Value == null) return;
                    ids = sr.Value.GetObjectIds();
                    break;
                }
            }
            else
            {
                ids = GetOrPromptSelection(ed, filter, "\n选择需要正向朝上的文字/标注/属性块：", "个对象");
                if (ids == null || ids.Length == 0) return;
            }

            var twist = settings.UpwardRef == "W" ? 0.0 : GetViewTwist();
            int done = 0, ok = 0, skip = 0, fail = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (ent is Dimension dim)
                    {
                        RecordOutcome(TryFlipDim(tr, lt, dim, false, twist), ref done, ref ok, ref skip, ref fail);
                    }
                    else if (ent is DBText || ent is MText)
                    {
                        RecordOutcome(TryUprightText(tr, lt, ent, twist), ref done, ref ok, ref skip, ref fail);
                    }
                    else if (ent is BlockReference br)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                                RecordOutcome(TryUprightText(tr, lt, att, twist), ref done, ref ok, ref skip, ref fail);
                        }
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[TXTUP] 已将 {done} 个倒置/反向文字转向上；{ok} 个本来已向上" +
                            (skip > 0 ? $"; 跳过 {skip} 个" : "") +
                            (fail > 0 ? $"; 失败 {fail} 个" : "") + "。");
        }

        public static void RunTextAlign(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            ObjectId refId = ObjectId.Null;
            while (refId.IsNull)
            {
                var peo = new PromptEntityOptions($"\n选择基准文字 [改行距(S)] <当前 {settings.TextAlignSpacing:0.##} 倍字高>：");
                peo.Keywords.Add("S", "S", "改行距(S)");
                peo.AllowNone = true;
                var per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.Cancel || per.Status == PromptStatus.None) return;
                if (per.Status == PromptStatus.Keyword)
                {
                    var pdo = new PromptDoubleOptions($"\n新的行距倍数 <{settings.TextAlignSpacing:0.##}>：")
                    {
                        DefaultValue = settings.TextAlignSpacing,
                        UseDefaultValue = true,
                        AllowNegative = false,
                        AllowZero = false,
                        AllowNone = true
                    };
                    var dr = ed.GetDouble(pdo);
                    if (dr.Status == PromptStatus.OK && dr.Value > 0.1)
                    {
                        settings.TextAlignSpacing = dr.Value;
                        settings.Save();
                    }
                    continue;
                }

                if (per.Status != PromptStatus.OK) continue;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (ent is DBText || ent is MText)
                        refId = per.ObjectId;
                    else
                        ed.WriteMessage("\n请选择单行文字(TEXT)或多行文字(MTEXT)。");
                    tr.Commit();
                }
            }

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selected = GetOrPromptSelection(ed, filter, "\n选择需要向下对齐的其余文字：", "个对象");
            if (selected == null) return;

            var others = new List<(double LocalY, ObjectId Id)>();
            Point3d p0;
            double ang, sp;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var refEnt = (Entity)tr.GetObject(refId, OpenMode.ForRead);
                p0 = GetTextAnchor(refEnt);
                ang = GetTextAngle(refEnt);
                var h = refEnt is DBText dt ? dt.Height : (refEnt is MText mt ? mt.TextHeight : 250.0);
                sp = h * settings.TextAlignSpacing;

                foreach (var id in selected)
                {
                    if (id == refId) continue;
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (!(ent is DBText || ent is MText) || IsLayerLocked(tr, lt, ent.Layer)) continue;
                    var p = GetTextAnchor(ent);
                    var ly = (p.X - p0.X) * (-Math.Sin(ang)) + (p.Y - p0.Y) * Math.Cos(ang);
                    others.Add((ly, id));
                }

                if (others.Count == 0)
                {
                    ed.WriteMessage("\n没有选择其他文字。");
                    tr.Commit();
                    return;
                }

                others.Sort((a, b) => b.LocalY.CompareTo(a.LocalY));
                var dx = Math.Sin(ang);
                var dy = -Math.Cos(ang);

                for (var n = 1; n <= others.Count; n++)
                {
                    var item = (Entity)tr.GetObject(others[n - 1].Id, OpenMode.ForRead);
                    item.UpgradeOpen();
                    var tgt = new Point3d(p0.X + n * sp * dx, p0.Y + n * sp * dy, p0.Z);

                    if (item is DBText dtItem)
                    {
                        dtItem.HorizontalMode = TextHorizontalMode.TextLeft;
                        dtItem.VerticalMode = TextVerticalMode.TextBase;
                        dtItem.Rotation = ang;
                        dtItem.Position = new Point3d(tgt.X, tgt.Y, dtItem.Position.Z);
                        dtItem.AdjustAlignment(db);
                    }
                    else if (item is MText mtItem)
                    {
                        mtItem.Rotation = ang;
                        mtItem.Attachment = AttachmentPoint.BottomLeft;
                        mtItem.Location = new Point3d(tgt.X, tgt.Y, mtItem.Location.Z);
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[TXAL] 完成: 共对齐 {others.Count} 个文字, 行距 {sp:0.##}。");
        }

        public static void RunDimSet(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            ed.WriteMessage($"\n当前: 基准倍数={settings.DimRoundStep:0.###}  取整容差={settings.DimRoundTol:0.###}");

            var p1 = new PromptDoubleOptions($"\n基准倍数 <{settings.DimRoundStep:0.###}>：")
            {
                DefaultValue = settings.DimRoundStep,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = false,
                AllowNone = true
            };
            var r1 = ed.GetDouble(p1);
            if (r1.Status == PromptStatus.OK && r1.Value > 0) settings.DimRoundStep = r1.Value;

            var p2 = new PromptDoubleOptions($"\n取整容差(离倍数多近才改) <{settings.DimRoundTol:0.###}>：")
            {
                DefaultValue = settings.DimRoundTol,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = true,
                AllowNone = true
            };
            var r2 = ed.GetDouble(p2);
            if (r2.Status == PromptStatus.OK && r2.Value >= 0) settings.DimRoundTol = r2.Value;

            settings.Save();
            ed.WriteMessage("\n[DFSET] 已保存。");
        }

        public static void RunDimInfo(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var per = ed.GetEntity("\n选择一个标注以查看调试信息：");
            if (per.Status != PromptStatus.OK) return;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is Dimension dim)
                {
                    ed.WriteMessage($"\n类型={dim.GetType().Name} 测量值={dim.Measurement:0.###} 文字旋转={dim.TextRotation:0.###} 文字位置=({dim.TextPosition.X:0.##},{dim.TextPosition.Y:0.##})");
                }
                tr.Commit();
            }
        }

        public static void RunTxtInfo(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var per = ed.GetEntity("\n选择一个文字以查看调试信息：");
            if (per.Status != PromptStatus.OK) return;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (ent is DBText dt)
                {
                    ed.WriteMessage($"\n类型=TEXT 旋转={dt.Rotation:0.###} 反向X={dt.IsMirroredInX} 倒置Y={dt.IsMirroredInY} 视图扭转={GetViewTwist():0.###}");
                }
                else if (ent is MText mt)
                {
                    ed.WriteMessage($"\n类型=MTEXT 旋转={mt.Rotation:0.###} 法线=({mt.Normal.X:0.##},{mt.Normal.Y:0.##},{mt.Normal.Z:0.##}) 视图扭转={GetViewTwist():0.###}");
                }
                tr.Commit();
            }
        }

        private enum OpOutcome { Done, Ok, Skip, Fail }

        private static void RecordOutcome(OpOutcome o, ref int done, ref int ok, ref int skip, ref int fail)
        {
            if (o == OpOutcome.Done) done++;
            else if (o == OpOutcome.Ok) ok++;
            else if (o == OpOutcome.Skip) skip++;
            else fail++;
        }

        private static OpOutcome TryFlipDim(Transaction tr, LayerTable lt, Dimension dim, bool forceAll, double twist)
        {
            if (!TryGetDimLineBaseAndPoint(dim, out var baseAngle, out var p10))
                return OpOutcome.Skip;

            var rot = dim.TextRotation;
            var eff = NormAngle(ReadableAngle(baseAngle) + rot + twist);
            if (!forceAll && !IsWrongReadingDir(eff))
                return OpOutcome.Ok;

            if (IsLayerLocked(tr, lt, dim.Layer)) return OpOutcome.Fail;
            try
            {
                dim.UpgradeOpen();
                dim.UsingDefaultTextPosition = false;
                dim.TextPosition = ReflectAcrossLine(dim.TextPosition, p10, baseAngle);
                dim.TextRotation = NormAngle(rot + Math.PI);
                return OpOutcome.Done;
            }
            catch
            {
                return OpOutcome.Fail;
            }
        }

        private static OpOutcome TryUprightText(Transaction tr, LayerTable lt, Entity ent, double twist)
        {
            if (IsLayerLocked(tr, lt, ent.Layer)) return OpOutcome.Fail;

            var modified = false;
            try
            {
                if (ent is DBText dt && (dt.IsMirroredInX || dt.IsMirroredInY))
                {
                    var c0 = TryCenter(dt);
                    dt.UpgradeOpen();
                    dt.IsMirroredInX = false;
                    dt.IsMirroredInY = false;
                    var c1 = TryCenter(dt);
                    if (c0.HasValue && c1.HasValue)
                        dt.TransformBy(Matrix3d.Displacement(c0.Value - c1.Value));
                    modified = true;
                }

                double? rot = ent is DBText t ? t.Rotation : (ent is MText m ? m.Rotation : (double?)null);
                if (!rot.HasValue) return modified ? OpOutcome.Done : OpOutcome.Skip;

                if (IsWrongReadingDir(NormAngle(rot.Value + twist)))
                {
                    var center = TryCenter(ent);
                    if (!center.HasValue) return OpOutcome.Fail;
                    ent.UpgradeOpen();
                    ent.TransformBy(Matrix3d.Rotation(Math.PI, Vector3d.ZAxis, center.Value));
                    modified = true;
                }

                return modified ? OpOutcome.Done : OpOutcome.Ok;
            }
            catch
            {
                return OpOutcome.Fail;
            }
        }

        private static Point3d? TryCenter(Entity ent)
        {
            try
            {
                var ext = ent.GeometricExtents;
                return new Point3d(
                    (ext.MinPoint.X + ext.MaxPoint.X) * 0.5,
                    (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5,
                    (ext.MinPoint.Z + ext.MaxPoint.Z) * 0.5);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetDimLineBaseAndPoint(Dimension dim, out double baseAngle, out Point3d p10)
        {
            baseAngle = 0;
            p10 = Point3d.Origin;
            if (dim is AlignedDimension ad)
            {
                var v = ad.XLine2Point - ad.XLine1Point;
                baseAngle = Math.Atan2(v.Y, v.X);
                p10 = ad.DimLinePoint;
                return true;
            }
            if (dim is RotatedDimension rd)
            {
                baseAngle = rd.Rotation;
                p10 = rd.DimLinePoint;
                return true;
            }
            return false;
        }

        private static Point3d ReflectAcrossLine(Point3d p, Point3d p0, double baseAngle)
        {
            var dx = Math.Cos(baseAngle);
            var dy = Math.Sin(baseAngle);
            var vx = p.X - p0.X;
            var vy = p.Y - p0.Y;
            var al = vx * dx + vy * dy;
            var px = vx - al * dx;
            var py = vy - al * dy;
            return new Point3d(p.X - 2.0 * px, p.Y - 2.0 * py, p.Z);
        }

        private static double? GetNumericValue(Entity ent, bool useDisplayPrecision)
        {
            if (ent is Dimension dim)
            {
                if (dim is LineAngularDimension2 || dim is Point3AngularDimension) return null;
                var ov = (dim.DimensionText ?? "").Trim();
                if (!string.IsNullOrEmpty(ov) && ov != "<>")
                    return TryParsePureNumber(ov);
                var m = dim.Measurement;
                if (useDisplayPrecision)
                {
                    var prec = Math.Max(0, Math.Min(8, dim.Dimdec));
                    var factor = Math.Pow(10.0, prec);
                    m = Math.Round(m * factor, MidpointRounding.AwayFromZero) / factor;
                }
                return m;
            }

            if (ent is DBText dt) return TryParsePureNumber(dt.TextString);
            if (ent is MText mt) return TryParsePureNumber(mt.Text);
            return null;
        }

        private static double? TryParsePureNumber(string? s)
        {
            var t = (s ?? "").Trim();
            if (t.Length == 0) return null;
            foreach (var ch in t)
            {
                if (!((ch >= '0' && ch <= '9') || ch == '.' || ch == '-')) return null;
            }
            return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        private static double NearestMultiple(double v, double step)
            => step * Math.Round(v / step, MidpointRounding.AwayFromZero);

        private static double OffsetFromStep(double v, double step)
            => Math.Abs(v - NearestMultiple(v, step));

        private static string FormatRound(double v)
        {
            var r = Math.Round(v);
            return Math.Abs(v - r) < 1e-9
                ? r.ToString("0", CultureInfo.InvariantCulture)
                : v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static ObjectId[]? GetOrPromptSelection(Editor ed, SelectionFilter filter, string prompt, string unitName)
        {
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                var ids = implied.Value.GetObjectIds();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                ed.WriteMessage($"\n使用当前选中的 {ids.Length} {unitName}。");
                return ids;
            }
            var sr = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = prompt }, filter);
            if (sr.Status != PromptStatus.OK || sr.Value == null) return null;
            return sr.Value.GetObjectIds();
        }

        private static bool IsLayerLocked(Transaction tr, LayerTable lt, string layerName)
            => lt.Has(layerName) && ((LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead)).IsLocked;

        private static double NormAngle(double a)
        {
            const double twoPi = 2.0 * Math.PI;
            while (a < 0) a += twoPi;
            while (a >= twoPi) a -= twoPi;
            return a;
        }

        private static bool IsWrongReadingDir(double a)
            => a > Math.PI * 0.5 + 1e-4 && a <= Math.PI * 1.5 + 1e-4;

        private static double ReadableAngle(double a)
        {
            a = NormAngle(a);
            return IsWrongReadingDir(a) ? NormAngle(a - Math.PI) : a;
        }

        private static double GetViewTwist()
        {
            try { return Convert.ToDouble(CoreApp.GetSystemVariable("VIEWTWIST")); }
            catch { return 0.0; }
        }

        private static Point3d GetTextAnchor(Entity ent)
        {
            if (ent is DBText dt)
            {
                if (dt.HorizontalMode != TextHorizontalMode.TextLeft || dt.VerticalMode != TextVerticalMode.TextBase)
                    return dt.AlignmentPoint;
                return dt.Position;
            }
            if (ent is MText mt) return mt.Location;
            return Point3d.Origin;
        }

        private static double GetTextAngle(Entity ent)
        {
            if (ent is DBText dt) return dt.Rotation;
            if (ent is MText mt) return mt.Rotation;
            return 0.0;
        }
    }
}
