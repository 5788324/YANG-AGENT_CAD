using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// 源泉风格填充图案增强工具集（兼容实体剪裁、挖出不删拆分、按孤岛/按线拆分、布尔合并、去关联、边界重建、改基点）。
    /// </summary>
    public static partial class YqHatchToolsService
    {
        private const double AreaTol = 1e-4;
        private const double PtTol = 1e-4;

        private sealed class LoopRecord : IDisposable
        {
            public Polyline Polyline { get; }
            public Region? Region { get; set; }
            public double Area { get; set; }
            public Extents3d Extents { get; set; }
            public int Depth { get; set; }
            public int ParentIndex { get; set; } = -1;

            public LoopRecord(Polyline polyline)
            {
                Polyline = polyline;
            }

            public void Dispose()
            {
                try { Region?.Dispose(); } catch { }
                try { Polyline.Dispose(); } catch { }
            }
        }

        // ============================================================
        // 1. 剪裁填充图案 (HHJ -> YGHCLIP / YGHCLIPSPLIT / YGHCLIPINT)
        //    S: 挖除框内（从原填充扣除区域）
        //    K: 拆分挖出（从原填充挖掉该区域，但不删除，生成独立新填充）
        //    I: 仅留框内（保留选中区域内的填充，裁掉外部）
        // ============================================================
        public static void RunHatchClip(Document doc, string? forcedMode = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();
            var mode = string.IsNullOrEmpty(forcedMode) ? settings.HatchClipMode : forcedMode!.ToUpperInvariant();

            var selectedIds = PromptOptionalHatchSelection(
                ed,
                $"\n选择要剪裁的填充图案 [当前模式:{DescribeClipMode(mode)}] <回车按剪裁框自动感应>：");
            if (selectedIds == null) return;

            var clipResult = PromptClipBoundary(ed, db, mode);
            if (clipResult == null) return;

            var (clipPolyWcs, chosenMode) = clipResult.Value;
            using (clipPolyWcs)
            {
                if (string.IsNullOrEmpty(forcedMode) && chosenMode != settings.HatchClipMode)
                {
                    settings.HatchClipMode = chosenMode;
                    settings.Save();
                }

                ObjectId[] candidateIds = selectedIds;
                if (candidateIds.Length == 0)
                {
                    candidateIds = FindHatchesIntersectingExtents(db, clipPolyWcs);
                    if (candidateIds.Length == 0)
                    {
                        ed.WriteMessage("\n未在剪裁范围内找到可处理的填充图案。");
                        return;
                    }
                }

                var processed = 0;
                var createdSplit = 0;
                var erasedEmpty = 0;
                var fallbackUsed = 0;
                var skipped = 0;
                try
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                        foreach (var id in candidateIds)
                        {
                            if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch hatch)) continue;
                            if (IsLayerLocked(tr, lt, hatch.Layer)) continue;
                            if (!ExtentsOverlap2d(hatch, clipPolyWcs)) continue;

                            hatch.UpgradeOpen();
                            if (TryClipSingleHatch(tr, space, hatch, clipPolyWcs, chosenMode,
                                out var addedCount, out var deleted, out var usedFallback))
                            {
                                processed++;
                                createdSplit += addedCount;
                                if (deleted) erasedEmpty++;
                                if (usedFallback) fallbackUsed++;
                            }
                            else
                            {
                                skipped++;
                            }
                        }

                        tr.Commit();
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n填充剪裁失败，本次修改已撤销：{ex.Message}");
                    return;
                }

                if (chosenMode == "K")
                {
                    ed.WriteMessage(
                        $"\n[剪裁填充:拆分挖出不删] 已处理 {processed} 个填充，新拆分出 {createdSplit} 个独立填充图案。" +
                        (fallbackUsed > 0 ? $" (二维多边形备用引擎 {fallbackUsed})" : ""));
                }
                else
                {
                    ed.WriteMessage(
                        $"\n[剪裁填充:{DescribeClipMode(chosenMode)}] 已处理 {processed} 个填充" +
                        (erasedEmpty > 0 ? $"，其中 {erasedEmpty} 个已完全裁空移除。" : "。"));
                }
                if (skipped > 0)
                    ed.WriteMessage($"\n已跳过 {skipped} 个未生成裁切结果的填充，原填充保留。");
            }
        }

        // ============================================================
        // 2. 拆分填充图案 (HHX -> YGHSPLIT)
        // ============================================================
        public static void RunHatchSplit(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var sel = GetOrPromptSelection(ed, filter, "\n选择要拆分的填充图案：");
            if (sel == null || sel.Length == 0) return;

            var multiIslandIds = new List<ObjectId>();
            var singleIslandIds = new List<ObjectId>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in sel)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch hatch)) continue;
                    if (IsLayerLocked(tr, lt, hatch.Layer)) continue;

                    var shift = ComputeHatchLocalOrigin2d(hatch);
                    if (TryExtractLoopHierarchy(hatch, shift, out var records, out var islands))
                    {
                        using (DisposeList(records))
                        {
                            if (islands.Count > 1) multiIslandIds.Add(id);
                            else singleIslandIds.Add(id);
                        }
                    }
                    else
                    {
                        var extLoops = 0;
                        for (var i = 0; i < hatch.NumberOfLoops; i++)
                        {
                            if ((hatch.LoopTypeAt(i) & HatchLoopTypes.External) != 0) extLoops++;
                        }
                        if (extLoops > 1) multiIslandIds.Add(id);
                        else singleIslandIds.Add(id);
                    }
                }
                tr.Commit();
            }

            if (multiIslandIds.Count == 0 && singleIslandIds.Count == 0)
            {
                ed.WriteMessage("\n所选填充所在图层可能已锁定，未作修改。");
                return;
            }

            var splitLinePts = new List<Point3d>();
            if (singleIslandIds.Count > 0)
            {
                if (multiIslandIds.Count > 0)
                {
                    var pko = new PromptKeywordOptions(
                        $"\n检测到 {multiIslandIds.Count} 个多区域填充和 {singleIslandIds.Count} 个单区域填充，是否同时按线切分单区域填充？[是(Y)/仅拆多区域(N)] <N>：",
                        "Y N")
                    { AllowNone = true };
                    pko.Keywords.Default = "N";
                    var kr = ed.GetKeywords(pko);
                    if (kr.Status == PromptStatus.Cancel) return;
                    if (string.Equals(kr.StringResult, "Y", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!PromptCutLinePoints(ed, db, splitLinePts)) return;
                    }
                }
                else
                {
                    ed.WriteMessage("\n所选填充为单一连通区域，进入【按线一刀切分】模式：");
                    if (!PromptCutLinePoints(ed, db, splitLinePts)) return;
                }
            }

            var splitIslandsTotal = 0;
            var cutLinesTotal = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                foreach (var id in multiIslandIds)
                {
                    if (!(tr.GetObject(id, OpenMode.ForWrite) is Hatch hatch)) continue;
                    var pieceCount = SplitHatchByIslands(tr, space, hatch);
                    if (pieceCount > 1) splitIslandsTotal += pieceCount;
                }

                if (splitLinePts.Count >= 2)
                {
                    foreach (var id in singleIslandIds)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForWrite) is Hatch hatch)) continue;
                        var pieceCount = SplitHatchByCutPoints(tr, space, hatch, splitLinePts);
                        if (pieceCount > 1) cutLinesTotal += pieceCount;
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage(
                $"\n[拆分填充] 孤岛拆分生成 {splitIslandsTotal} 个独立填充" +
                (cutLinesTotal > 0 ? $"，切分线拆分生成 {cutLinesTotal} 个独立填充。" : "。"));
        }

        // ============================================================
        // 3. 合并填充图案 (HHU -> YGHUNION)
        // ============================================================
        public static void RunHatchUnion(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var sel = GetOrPromptSelection(ed, filter, "\n选择要合并的两个或多个填充图案(以首个选中的样式为准)：");
            if (sel == null || sel.Length < 2)
            {
                ed.WriteMessage("\n请至少选择 2 个填充图案进行合并。");
                return;
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                var editable = new List<Hatch>();
                foreach (var id in sel)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch h) || IsLayerLocked(tr, lt, h.Layer))
                        continue;
                    h.UpgradeOpen();
                    editable.Add(h);
                }

                if (editable.Count < 2)
                {
                    ed.WriteMessage("\n可编辑的填充不足 2 个（请检查图层是否锁定）。");
                    tr.Commit();
                    return;
                }

                var template = editable[0];
                var localOrigin = ComputeHatchLocalOrigin2d(template);
                Region? unionRegion = null;
                var failedHatches = new List<Hatch>();

                foreach (var h in editable)
                {
                    if (TryBuildHatchCompositeRegion(h, localOrigin, out var hReg, requireAllLoops: true) && hReg != null)
                    {
                        if (unionRegion == null)
                        {
                            unionRegion = hReg;
                        }
                        else
                        {
                            try
                            {
                                unionRegion.BooleanOperation(BooleanOperationType.BoolUnite, hReg);
                            }
                            catch
                            {
                                failedHatches.Add(h);
                            }
                            finally
                            {
                                try { hReg.Dispose(); } catch { }
                            }
                        }
                    }
                    else
                    {
                        failedHatches.Add(h);
                    }
                }

                if (unionRegion != null && failedHatches.Count == 0)
                {
                    using (unionRegion)
                    {
                        if (TryPopulateHatchFromRegion(tr, space, template, unionRegion, localOrigin,
                            groupIntoSingleHatch: true, updateInPlace: template, out _, requireCompleteResult: true))
                        {
                            for (var i = 1; i < editable.Count; i++) editable[i].Erase();
                            tr.Commit();
                            ed.WriteMessage($"\n[合并填充] 已将 {editable.Count} 个填充布尔融合成 1 个整体填充图案。");
                            return;
                        }
                    }
                }
                else
                {
                    try { unionRegion?.Dispose(); } catch { }
                }

                template.Associative = false;
                try { template.RemoveAssociatedObjectIds(); } catch { }
                var addedLoops = 0;
                for (var i = 1; i < editable.Count; i++)
                {
                    var h = editable[i];
                    for (var li = 0; li < h.NumberOfLoops; li++)
                    {
                        using var poly = ConvertLoopToPolyline(h, h.GetLoopAt(li));
                        var loopType = (h.LoopTypeAt(li) & HatchLoopTypes.External) != 0
                            ? HatchLoopTypes.External
                            : HatchLoopTypes.Default;
                        if (poly == null || !AppendPolylineAsLoop(template, poly, loopType, new Vector2d(0.0, 0.0)))
                        {
                            ed.WriteMessage("\n[合并填充] 有边界无法完整转换，已保留原填充。");
                            return;
                        }
                        addedLoops++;
                    }
                }

                template.EvaluateHatch(false);
                for (var i = 1; i < editable.Count; i++) editable[i].Erase();
                tr.Commit();
                ed.WriteMessage($"\n[合并填充] 已合并 {editable.Count} 个填充图案（追加 {addedLoops} 条边界环）。");
            }
        }

        // ============================================================
        // 4. 去除填充关联 (HHGL -> YGHDIS)
        // ============================================================
        public static void RunHatchDisassociate(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var ids = PromptOptionalHatchSelection(ed, "\n选择要去除关联性的填充图案 <回车处理当前空间全部关联填充>：");
            if (ids == null) return;

            var count = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                IEnumerable<ObjectId> targets = ids.Length > 0 ? ids : space.Cast<ObjectId>();
                foreach (var id in targets)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch hatch)) continue;
                    if (!hatch.Associative && hatch.GetAssociatedObjectIds().Count == 0) continue;
                    if (IsLayerLocked(tr, lt, hatch.Layer)) continue;

                    hatch.UpgradeOpen();
                    hatch.Associative = false;
                    try { hatch.RemoveAssociatedObjectIds(); } catch { }
                    count++;
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[去除填充关联] 已解除 {count} 个填充图案的边界关联。");
        }

        // ============================================================
        // 5. 重建填充边界 (HHB -> YGHBOUND)
        // ============================================================
        public static void RunHatchBoundary(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var settings = UserSettings.Load();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var sel = GetOrPromptSelection(ed, filter, "\n选择要提取/重建边界线的填充图案：");
            if (sel == null || sel.Length == 0) return;

            var defKw = settings.HatchBoundAssociative ? "A" : "N";
            var pko = new PromptKeywordOptions(
                $"\n生成多段线边界后是否同时关联？[生成并关联(A)/仅生成独立边界(N)] <{defKw}>：",
                "A N")
            { AllowNone = true };
            pko.Keywords.Default = defKw;
            var kr = ed.GetKeywords(pko);
            if (kr.Status == PromptStatus.Cancel) return;

            var kw = string.IsNullOrEmpty(kr.StringResult) ? defKw : kr.StringResult.ToUpperInvariant();
            var associate = kw == "A";
            if (associate != settings.HatchBoundAssociative)
            {
                settings.HatchBoundAssociative = associate;
                settings.Save();
            }

            var boundariesCreated = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                foreach (var id in sel)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch hatch)) continue;
                    var loopIds = new List<(HatchLoopTypes Type, ObjectId PolyId)>();

                    for (var i = 0; i < hatch.NumberOfLoops; i++)
                    {
                        var poly = ConvertLoopToPolyline(hatch, hatch.GetLoopAt(i));
                        if (poly == null) continue;

                        poly.Layer = hatch.Layer;
                        poly.Color = hatch.Color;
                        var newId = space.AppendEntity(poly);
                        tr.AddNewlyCreatedDBObject(poly, true);
                        boundariesCreated++;

                        var loopFlag = (hatch.LoopTypeAt(i) & HatchLoopTypes.External) != 0
                            ? HatchLoopTypes.External
                            : HatchLoopTypes.Default;
                        loopIds.Add((loopFlag, newId));
                    }

                    if (associate && loopIds.Count > 0 && !IsLayerLocked(tr, lt, hatch.Layer))
                    {
                        hatch.UpgradeOpen();
                        hatch.Associative = false;
                        try { hatch.RemoveAssociatedObjectIds(); } catch { }
                        while (hatch.NumberOfLoops > 0)
                            hatch.RemoveLoopAt(hatch.NumberOfLoops - 1);

                        hatch.Associative = true;
                        foreach (var (ltype, polyId) in loopIds)
                        {
                            var col = new ObjectIdCollection { polyId };
                            hatch.AppendLoop(ltype, col);
                        }
                        try { hatch.EvaluateHatch(true); } catch { }
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[重建填充边界] 已生成 {boundariesCreated} 条闭合多段线边界{(associate ? "（已重新关联）" : "")}。");
        }

        // ============================================================
        // 6. 重设填充图案基准点 (HHI -> YGHBASE)
        // ============================================================
        public static void RunHatchOrigin(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var sel = GetOrPromptSelection(ed, filter, "\n选择要调整起铺基点的填充图案：");
            if (sel == null || sel.Length == 0) return;

            var ppo = new PromptPointOptions("\n指定新的填充图案基准点(例如墙角/排砖起点)：");
            var ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            var pickedWcs = ppr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            var updated = 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in sel)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Hatch hatch)) continue;
                    if (IsLayerLocked(tr, lt, hatch.Layer)) continue;

                    hatch.UpgradeOpen();
                    var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, hatch.Normal));
                    var ocsPt = pickedWcs.TransformBy(w2ocs);
                    hatch.Origin = new Point2d(ocsPt.X, ocsPt.Y);
                    try { hatch.EvaluateHatch(false); } catch { }
                    updated++;
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[重设填充基准点] 已更新 {updated} 个填充图案的起铺原点。");
        }

        // ============================================================
        // 核心几何：单项剪裁执行（ACIS Region 布尔 + 局部坐标平移 + 2D多边形备用引擎）
        // ============================================================
        private static bool TryClipSingleHatch(
            Transaction tr,
            BlockTableRecord space,
            Hatch hatch,
            Polyline clipPolyWcs,
            string mode,
            out int extraCreated,
            out bool erasedOriginal,
            out bool usedFallback)
        {
            extraCreated = 0;
            erasedOriginal = false;
            usedFallback = false;

            var localOrigin = ComputeHatchLocalOrigin2d(hatch);
            using var clipPolyOcs = ProjectWcsPolylineToHatchPlane(clipPolyWcs, hatch.Normal, hatch.Elevation, localOrigin);
            if (clipPolyOcs == null) return false;

            using var clipRegion = CreateRegionFromCurve(clipPolyOcs);
            if (clipRegion == null || clipRegion.Area < AreaTol) return false;

            if (TryBuildHatchCompositeRegion(hatch, localOrigin, out var hatchRegion, requireAllLoops: true) && hatchRegion != null)
            {
                using (hatchRegion)
                {
                    var operation = mode == "I" ? BooleanOperationType.BoolIntersect : BooleanOperationType.BoolSubtract;
                    using var remaining = (Region)hatchRegion.Clone();
                    if (TryApplyClipBoolean(remaining, operation, clipRegion))
                    {
                        if (mode != "K")
                        {
                            if (remaining.Area < AreaTol)
                            {
                                hatch.Erase();
                                erasedOriginal = true;
                                return true;
                            }

                            if (TryPopulateHatchFromRegion(tr, space, hatch, remaining, localOrigin,
                                groupIntoSingleHatch: true, updateInPlace: hatch, out _, requireCompleteResult: true))
                                return true;
                        }
                        else
                        {
                            using var inside = (Region)hatchRegion.Clone();
                            if (TryApplyClipBoolean(inside, BooleanOperationType.BoolIntersect, clipRegion))
                            {
                                if (inside.Area < AreaTol || remaining.Area < AreaTol) return false;

                                if (TryPopulateHatchFromRegion(tr, space, hatch, remaining, localOrigin,
                                    groupIntoSingleHatch: true, updateInPlace: hatch, out _, requireCompleteResult: true))
                                {
                                    if (!TryPopulateHatchFromRegion(tr, space, hatch, inside, localOrigin,
                                        groupIntoSingleHatch: false, updateInPlace: null, out var splitPieces, requireCompleteResult: true))
                                        throw new InvalidOperationException("无法生成挖出的独立填充。");

                                    extraCreated = splitPieces.Count;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // ACIS 无法处理异常环时，使用严格二维凸包/矩形精确多边形裁切引擎，绝不直接叠加未裁切的框作为黑洞
            usedFallback = true;
            return TryClipBy2dPolygonFallback(tr, space, hatch, clipPolyOcs, localOrigin, mode, out extraCreated, out erasedOriginal);
        }

        private static bool TryApplyClipBoolean(Region target, BooleanOperationType operation, Region clip)
        {
            using var operand = (Region)clip.Clone();
            try
            {
                target.BooleanOperation(operation, operand);
                return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 精确二维多边形裁切备用引擎（Sutherland-Hodgman 交集 + 凸多边形半平面差集分区）。
        /// 仅在 ACIS Region 拒绝脏几何体时触发；不会把跨边界的大框当成未裁切内洞塞入原填充。
        /// </summary>
        private static bool TryClipBy2dPolygonFallback(
            Transaction tr,
            BlockTableRecord space,
            Hatch hatch,
            Polyline clipPolyOcsLocal,
            Point2d localOrigin,
            string mode,
            out int extraCreated,
            out bool erasedOriginal)
        {
            extraCreated = 0;
            erasedOriginal = false;

            var clipPoly2d = GetStraightVertices(clipPolyOcsLocal);
            if (clipPoly2d == null || clipPoly2d.Count < 3 || !IsConvexPolygonCcw(clipPoly2d)) return false;

            var rawPolys = new List<(List<Point2d> Pts, double Area)>();
            for (var i = 0; i < hatch.NumberOfLoops; i++)
            {
                var loop = hatch.GetLoopAt(i);
                if (!loop.IsPolyline && (loop.Curves == null || loop.Curves.Cast<Curve2d>().Any(curve => !(curve is LineSegment2d))))
                    return false;

                using var lp = ConvertLoopToPolyline(hatch, loop, -localOrigin.GetAsVector());
                if (lp == null) return false;
                var pts = GetStraightVertices(lp);
                if (pts == null || pts.Count < 3) return false;
                var area = Math.Abs(ComputeSignedArea2d(pts));
                if (area < AreaTol) return false;
                if (ComputeSignedArea2d(pts) < 0) pts.Reverse();
                rawPolys.Add((pts, area));
            }
            if (rawPolys.Count == 0) return false;

            rawPolys.Sort((a, b) => b.Area.CompareTo(a.Area));
            var outerPolys = new List<List<Point2d>>();
            var holePolys = new List<List<Point2d>>();

            for (var i = 0; i < rawPolys.Count; i++)
            {
                var depth = 0;
                var samplePt = FindPointInsideOrOnPolygon(rawPolys[i].Pts);
                for (var j = 0; j < i; j++)
                {
                    if (IsPointInPolygon2d(samplePt, rawPolys[j].Pts)) depth++;
                }
                if (depth % 2 == 0) outerPolys.Add(rawPolys[i].Pts);
                else holePolys.Add(rawPolys[i].Pts);
            }

            if (outerPolys.Count == 0) return false;

            var insidePolys = new List<List<Point2d>>();
            var outsidePolys = new List<List<Point2d>>();

            foreach (var outer in outerPolys)
            {
                PartitionPolygonByConvexClip2d(outer, clipPoly2d, outsidePolys, insidePolys);
            }

            // 分配内洞：仅将完全或部分落在 inside / outside 内部的内洞作为 Default 洞环
            var insideHoles = new List<List<Point2d>>();
            var outsideHoles = new List<List<Point2d>>();
            foreach (var hole in holePolys)
            {
                PartitionPolygonByConvexClip2d(hole, clipPoly2d, outsideHoles, insideHoles);
            }

            insidePolys.RemoveAll(p => Math.Abs(ComputeSignedArea2d(p)) < AreaTol);
            outsidePolys.RemoveAll(p => Math.Abs(ComputeSignedArea2d(p)) < AreaTol);
            FilterSliverPolys2d(insidePolys);
            FilterSliverPolys2d(outsidePolys);

            if (mode == "I")
            {
                if (insidePolys.Count == 0)
                {
                    hatch.Erase();
                    erasedOriginal = true;
                    return true;
                }
                RebuildHatchFrom2dPolygons(hatch, insidePolys, insideHoles, localOrigin);
                return true;
            }

            if (mode == "S")
            {
                if (outsidePolys.Count == 0)
                {
                    hatch.Erase();
                    erasedOriginal = true;
                    return true;
                }
                RebuildHatchFrom2dPolygons(hatch, outsidePolys, outsideHoles, localOrigin);
                return true;
            }

            // mode == "K"
            if (insidePolys.Count == 0 || outsidePolys.Count == 0) return false;

            RebuildHatchFrom2dPolygons(hatch, outsidePolys, outsideHoles, localOrigin);
            foreach (var piece in insidePolys)
            {
                var clone = CreateEmptyClone(hatch);
                var pieceHoles = FilterContainedPolygons(piece, insideHoles);
                RebuildHatchFrom2dPolygons(clone, new List<List<Point2d>> { piece }, pieceHoles, localOrigin);
                space.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                extraCreated++;
            }
            return true;
        }

        private static void RebuildHatchFrom2dPolygons(
            Hatch target,
            List<List<Point2d>> outers,
            List<List<Point2d>> holes,
            Point2d localOrigin)
        {
            target.Associative = false;
            try { target.RemoveAssociatedObjectIds(); } catch { }
            while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);

            var shiftBack = localOrigin.GetAsVector();
            foreach (var o in outers)
            {
                AppendPointListAsLoop(target, o, HatchLoopTypes.External, shiftBack);
            }
            foreach (var h in holes)
            {
                AppendPointListAsLoop(target, h, HatchLoopTypes.Default, shiftBack);
            }
            target.EvaluateHatch(false);
        }

        private static List<List<Point2d>> FilterContainedPolygons(List<Point2d> outer, List<List<Point2d>> candidates)
        {
            var res = new List<List<Point2d>>();
            foreach (var c in candidates)
            {
                if (c.Count >= 3 && IsPointInPolygon2d(FindPointInsideOrOnPolygon(c), outer))
                    res.Add(c);
            }
            return res;
        }

        private static void AppendPointListAsLoop(Hatch target, List<Point2d> pts, HatchLoopTypes type, Vector2d shiftBack)
        {
            if (pts.Count < 3) return;
            var pCol = new Point2dCollection();
            var bCol = new DoubleCollection();
            foreach (var p in pts)
            {
                pCol.Add(p + shiftBack);
                bCol.Add(0.0);
            }
            target.AppendLoop(type | HatchLoopTypes.Polyline, pCol, bCol);
        }

        /// <summary>
        /// 用凸多边形边界逐边划分任意多边形：rem 被切入外侧的碎片进入 outside，最终剩在所有内侧的即交集 inside。
        /// </summary>
        private static void PartitionPolygonByConvexClip2d(
            List<Point2d> subject,
            List<Point2d> convexClipCcw,
            List<List<Point2d>> outsideOut,
            List<List<Point2d>> insideOut)
        {
            var rem = subject;
            for (var i = 0; i < convexClipCcw.Count; i++)
            {
                if (rem.Count < 3) break;
                var a = convexClipCcw[i];
                var b = convexClipCcw[(i + 1) % convexClipCcw.Count];

                var outPiece = ClipPolygonHalfPlane(rem, a, b, keepLeftSide: false);
                if (outPiece.Count >= 3 && Math.Abs(ComputeSignedArea2d(outPiece)) >= AreaTol)
                {
                    if (ComputeSignedArea2d(outPiece) < 0) outPiece.Reverse();
                    outsideOut.Add(outPiece);
                }

                rem = ClipPolygonHalfPlane(rem, a, b, keepLeftSide: true);
            }

            if (rem.Count >= 3 && Math.Abs(ComputeSignedArea2d(rem)) >= AreaTol)
            {
                if (ComputeSignedArea2d(rem) < 0) rem.Reverse();
                insideOut.Add(rem);
            }
        }

        private static List<Point2d> ClipPolygonHalfPlane(List<Point2d> poly, Point2d a, Point2d b, bool keepLeftSide)
        {
            var res = new List<Point2d>();
            if (poly.Count < 3) return res;

            var sign = keepLeftSide ? 1.0 : -1.0;
            bool IsInside(Point2d p) => ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)) * sign >= -1e-7;

            Point2d Intersect(Point2d s, Point2d e)
            {
                var x1 = s.X; var y1 = s.Y; var x2 = e.X; var y2 = e.Y;
                var x3 = a.X; var y3 = a.Y; var x4 = b.X; var y4 = b.Y;
                var denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
                if (Math.Abs(denom) < 1e-12) return e;
                var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
                t = Math.Max(0.0, Math.Min(1.0, t));
                return new Point2d(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
            }

            for (var i = 0; i < poly.Count; i++)
            {
                var curr = poly[i];
                var prev = poly[(i - 1 + poly.Count) % poly.Count];
                var currIn = IsInside(curr);
                var prevIn = IsInside(prev);

                if (currIn)
                {
                    if (!prevIn) AddUniquePt(res, Intersect(prev, curr));
                    AddUniquePt(res, curr);
                }
                else if (prevIn)
                {
                    AddUniquePt(res, Intersect(prev, curr));
                }
            }

            if (res.Count >= 2 && res[0].GetDistanceTo(res[res.Count - 1]) < PtTol)
                res.RemoveAt(res.Count - 1);
            return res;
        }

        private static void AddUniquePt(List<Point2d> list, Point2d pt)
        {
            if (list.Count == 0 || list[list.Count - 1].GetDistanceTo(pt) > PtTol)
                list.Add(pt);
        }

        // ============================================================
        // 拆分执行：按多孤岛 / 按切分线
        // ============================================================
        private static int SplitHatchByIslands(Transaction tr, BlockTableRecord space, Hatch hatch)
        {
            var localOrigin = ComputeHatchLocalOrigin2d(hatch);
            if (!TryExtractLoopHierarchy(hatch, localOrigin, out var records, out var islands))
                return 0;

            using (DisposeList(records))
            {
                if (islands.Count <= 1) return 0;

                var shiftBack = localOrigin.GetAsVector();
                var createdCount = 0;
                for (var i = 0; i < islands.Count; i++)
                {
                    var (outerIdx, holeIndices) = islands[i];
                    var target = i == 0 ? hatch : CreateEmptyClone(hatch);
                    if (i == 0)
                    {
                        target.Associative = false;
                        try { target.RemoveAssociatedObjectIds(); } catch { }
                        while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);
                    }

                    AppendPolylineAsLoop(target, records[outerIdx].Polyline, HatchLoopTypes.External, shiftBack);
                    foreach (var hi in holeIndices)
                    {
                        AppendPolylineAsLoop(target, records[hi].Polyline, HatchLoopTypes.Default, shiftBack);
                    }

                    try { target.EvaluateHatch(false); } catch { }

                    if (i > 0)
                    {
                        space.AppendEntity(target);
                        tr.AddNewlyCreatedDBObject(target, true);
                    }
                    createdCount++;
                }

                return createdCount;
            }
        }

        private static int SplitHatchByCutPoints(
            Transaction tr,
            BlockTableRecord space,
            Hatch hatch,
            IReadOnlyList<Point3d> cutPtsWcs)
        {
            if (cutPtsWcs.Count < 2) return 0;
            var localOrigin = ComputeHatchLocalOrigin2d(hatch);
            if (!TryBuildHatchCompositeRegion(hatch, localOrigin, out var hatchRegion) || hatchRegion == null)
                return 0;

            using (hatchRegion)
            {
                var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, hatch.Normal));
                var cutPtsLocal = cutPtsWcs.Select(p =>
                {
                    var tp = p.TransformBy(w2ocs);
                    return new Point2d(tp.X - localOrigin.X, tp.Y - localOrigin.Y);
                }).ToList();

                var ext = hatch.GeometricExtents;
                var pMinOcs = ext.MinPoint.TransformBy(w2ocs);
                var pMaxOcs = ext.MaxPoint.TransformBy(w2ocs);
                var dx = Math.Abs(pMaxOcs.X - pMinOcs.X);
                var dy = Math.Abs(pMaxOcs.Y - pMinOcs.Y);
                var span = Math.Sqrt(dx * dx + dy * dy) + 1000.0;

                if (cutPtsLocal.Count == 2)
                {
                    var p1 = cutPtsLocal[0];
                    var p2 = cutPtsLocal[1];
                    var dir = p2 - p1;
                    if (dir.Length < 1e-6) return 0;
                    var u = dir.GetNormal();
                    var n = new Vector2d(-u.Y, u.X);
                    var mid = new Point2d((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5);
                    var a = mid - u * span;
                    var b = mid + u * span;

                    using var half1 = BuildOcsRectPoly(hatch.Normal, hatch.Elevation, a, b, b + n * span, a + n * span);
                    using var half2 = BuildOcsRectPoly(hatch.Normal, hatch.Elevation, b, a, a - n * span, b - n * span);
                    using var reg1 = CreateRegionFromCurve(half1);
                    using var reg2 = CreateRegionFromCurve(half2);
                    if (reg1 == null || reg2 == null) return 0;

                    using var sideA = (Region)hatchRegion.Clone();
                    using var sideB = (Region)hatchRegion.Clone();
                    try
                    {
                        sideA.BooleanOperation(BooleanOperationType.BoolIntersect, reg1);
                        sideB.BooleanOperation(BooleanOperationType.BoolIntersect, reg2);
                    }
                    catch
                    {
                        return 0;
                    }

                    if (sideA.Area < AreaTol || sideB.Area < AreaTol) return 0;

                    if (!TryPopulateHatchFromRegion(tr, space, hatch, sideA, localOrigin,
                        groupIntoSingleHatch: false, updateInPlace: hatch, out var piecesA, requireCompleteResult: true) ||
                        !TryPopulateHatchFromRegion(tr, space, hatch, sideB, localOrigin,
                        groupIntoSingleHatch: false, updateInPlace: null, out var piecesB, requireCompleteResult: true))
                        throw new InvalidOperationException("无法完整重建两侧填充边界，已取消拆分。");
                    return piecesA.Count + piecesB.Count;
                }

                using var slitReg = BuildSlitRegionFromPolyline(hatch.Normal, hatch.Elevation, cutPtsLocal);
                if (slitReg == null) return 0;

                using var cutReg = (Region)hatchRegion.Clone();
                try
                {
                    cutReg.BooleanOperation(BooleanOperationType.BoolSubtract, slitReg);
                }
                catch
                {
                    return 0;
                }

                if (cutReg.Area < AreaTol) return 0;
                if (TryPopulateHatchFromRegion(tr, space, hatch, cutReg, localOrigin,
                    groupIntoSingleHatch: false, updateInPlace: hatch, out var allPieces))
                {
                    return allPieces.Count;
                }
                return 0;
            }
        }

        private static (Polyline ClipPolyWcs, string Mode)? PromptClipBoundary(Editor ed, Database db, string initialMode)
        {
            var mode = initialMode;
            while (true)
            {
                var ppo = new PromptPointOptions(
                    $"\n指定剪裁矩形第一角点 或 [选闭合边界(E)/挖除框内(S)/拆分挖出(K)/仅留框内(I)] <{DescribeClipMode(mode)}>：",
                    "E S K I");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.Cancel) return null;

                if (ppr.Status == PromptStatus.Keyword)
                {
                    var kw = ppr.StringResult.ToUpperInvariant();
                    if (kw == "S" || kw == "K" || kw == "I")
                    {
                        mode = kw;
                        ed.WriteMessage($"\n已切换剪裁模式：{DescribeClipMode(mode)}");
                        continue;
                    }
                    if (kw == "E")
                    {
                        var peo = new PromptEntityOptions("\n选择作为剪裁边界的闭合曲线(多段线/圆/椭圆)：");
                        peo.SetRejectMessage("\n请选择闭合曲线(Polyline/Circle/Ellipse/Spline)。");
                        peo.AddAllowedClass(typeof(Curve), false);
                        var per = ed.GetEntity(peo);
                        if (per.Status != PromptStatus.OK) return null;

                        using var tr = db.TransactionManager.StartTransaction();
                        if (!(tr.GetObject(per.ObjectId, OpenMode.ForRead) is Curve curve)) return null;
                        if (!curve.Closed)
                        {
                            ed.WriteMessage("\n所选边界未闭合，请重新选择闭合图形。");
                            continue;
                        }
                        var wcsPoly = ConvertAnyClosedCurveToPolylineWcs(curve);
                        tr.Commit();
                        if (wcsPoly == null)
                        {
                            ed.WriteMessage("\n无法将该边界转换为剪裁轮廓，请重新选择。");
                            continue;
                        }
                        return (wcsPoly, mode);
                    }
                }

                if (ppr.Status != PromptStatus.OK) return null;
                var p1Ucs = ppr.Value;
                var pcr = ed.GetCorner(new PromptCornerOptions("\n指定剪裁矩形对角点：", p1Ucs));
                if (pcr.Status != PromptStatus.OK) return null;
                var p2Ucs = pcr.Value;

                if (Math.Abs(p1Ucs.X - p2Ucs.X) < 1e-4 || Math.Abs(p1Ucs.Y - p2Ucs.Y) < 1e-4)
                {
                    ed.WriteMessage("\n剪裁矩形面积为零，请重新指定。");
                    continue;
                }

                // 严格规整为 UCS +Z 右手系逆时针 (CCW) 矩形，避免任何方向拉框产生 -Z 反向法线
                var minX = Math.Min(p1Ucs.X, p2Ucs.X);
                var maxX = Math.Max(p1Ucs.X, p2Ucs.X);
                var minY = Math.Min(p1Ucs.Y, p2Ucs.Y);
                var maxY = Math.Max(p1Ucs.Y, p2Ucs.Y);
                var ucs2wcs = ed.CurrentUserCoordinateSystem;
                var z = p1Ucs.Z;

                var c0 = new Point3d(minX, minY, z).TransformBy(ucs2wcs);
                var c1 = new Point3d(maxX, minY, z).TransformBy(ucs2wcs);
                var c2 = new Point3d(maxX, maxY, z).TransformBy(ucs2wcs);
                var c3 = new Point3d(minX, maxY, z).TransformBy(ucs2wcs);

                var planeNormal = (c1 - c0).CrossProduct(c3 - c0);
                if (planeNormal.Length < 1e-8) planeNormal = Vector3d.ZAxis;
                planeNormal = planeNormal.GetNormal();
                var w2p = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, planeNormal));
                var p0Plane = c0.TransformBy(w2p);

                var rect = new Polyline { Normal = planeNormal, Elevation = p0Plane.Z, Closed = true };
                var corners = new[] { c0, c1, c2, c3 };
                for (var i = 0; i < 4; i++)
                {
                    var tp = corners[i].TransformBy(w2p);
                    rect.AddVertexAt(i, new Point2d(tp.X, tp.Y), 0.0, 0.0, 0.0);
                }
                SanitizeAndOrientPolyline2d(rect, wantCCW: true);
                return (rect, mode);
            }
        }

        private static Polyline? ConvertAnyClosedCurveToPolylineWcs(Curve curve)
        {
            if (curve is Polyline pl)
            {
                var clone = (Polyline)pl.Clone();
                return SanitizeAndOrientPolyline2d(clone, wantCCW: true) ? clone : DisposeNull(clone);
            }

            if (curve is Circle cir)
            {
                var w2p = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, cir.Normal));
                var cOcs = cir.Center.TransformBy(w2p);
                var p = new Polyline { Normal = cir.Normal, Elevation = cOcs.Z, Closed = true };
                p.AddVertexAt(0, new Point2d(cOcs.X + cir.Radius, cOcs.Y), 1.0, 0.0, 0.0);
                p.AddVertexAt(1, new Point2d(cOcs.X - cir.Radius, cOcs.Y), 1.0, 0.0, 0.0);
                return SanitizeAndOrientPolyline2d(p, wantCCW: true) ? p : DisposeNull(p);
            }

            var norm = Vector3d.ZAxis;
            double elev = 0.0;
            try
            {
                var plane = curve.GetPlane();
                norm = plane.Normal;
                elev = curve.StartPoint.TransformBy(Matrix3d.WorldToPlane(new Plane(Point3d.Origin, norm))).Z;
            }
            catch { }

            var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, norm));
            return SampleCurve3dToPolyline(curve, w2ocs, norm, elev, closed: true);
        }

        private static bool PromptCutLinePoints(Editor ed, Database db, List<Point3d> outPtsWcs)
        {
            outPtsWcs.Clear();
            var ppo = new PromptPointOptions("\n指定切分线第一点：");
            ppo.Keywords.Add("E", "E", "选现有直线或多段线(E)");
            var ppr = ed.GetPoint(ppo);
            if (ppr.Status == PromptStatus.Cancel) return false;

            if (ppr.Status == PromptStatus.Keyword)
            {
                var peo = new PromptEntityOptions("\n选择用于切分填充的直线或多段线：");
                peo.SetRejectMessage("\n请选择直线(LINE)或多段线(POLYLINE)。");
                peo.AddAllowedClass(typeof(Line), false);
                peo.AddAllowedClass(typeof(Polyline), false);
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return false;

                using var tr = db.TransactionManager.StartTransaction();
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (ent is Line line)
                {
                    outPtsWcs.Add(line.StartPoint);
                    outPtsWcs.Add(line.EndPoint);
                }
                else if (ent is Polyline poly && poly.NumberOfVertices >= 2)
                {
                    if (!poly.HasBulges)
                    {
                        for (var i = 0; i < poly.NumberOfVertices; i++)
                            outPtsWcs.Add(poly.GetPoint3dAt(i));
                        if (poly.Closed) outPtsWcs.Add(poly.GetPoint3dAt(0));
                    }
                    else
                    {
                        var steps = Math.Max(32, poly.NumberOfVertices * 8);
                        var len = poly.Length;
                        for (var i = 0; i <= steps; i++)
                            outPtsWcs.Add(poly.GetPointAtDist(len * i / steps));
                    }
                }
                tr.Commit();
                return outPtsWcs.Count >= 2;
            }

            if (ppr.Status != PromptStatus.OK) return false;
            var p1Ucs = ppr.Value;
            var p2Opts = new PromptPointOptions("\n指定切分线第二点：")
            {
                BasePoint = p1Ucs,
                UseBasePoint = true
            };
            var ppr2 = ed.GetPoint(p2Opts);
            if (ppr2.Status != PromptStatus.OK) return false;

            var ucs2wcs = ed.CurrentUserCoordinateSystem;
            outPtsWcs.Add(p1Ucs.TransformBy(ucs2wcs));
            outPtsWcs.Add(ppr2.Value.TransformBy(ucs2wcs));
            return true;
        }

        private static ObjectId[]? PromptOptionalHatchSelection(Editor ed, string prompt)
        {
            var hatchClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Hatch));
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value.Count > 0)
            {
                var preIds = implied.Value.GetObjectIds()
                    .Where(id => id.ObjectClass != null && id.ObjectClass.IsDerivedFrom(hatchClass))
                    .ToArray();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                if (preIds.Length > 0) return preIds;
            }

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = prompt,
                AllowDuplicates = false
            };
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status == PromptStatus.Cancel) return null;
            if (psr.Status == PromptStatus.Error || psr.Status == PromptStatus.None) return Array.Empty<ObjectId>();
            return psr.Status == PromptStatus.OK ? psr.Value.GetObjectIds() : Array.Empty<ObjectId>();
        }

        private static ObjectId[]? GetOrPromptSelection(Editor ed, SelectionFilter filter, string prompt)
        {
            var hatchClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Hatch));
            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value.Count > 0)
            {
                var preIds = implied.Value.GetObjectIds()
                    .Where(id => id.ObjectClass != null && id.ObjectClass.IsDerivedFrom(hatchClass))
                    .ToArray();
                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                if (preIds.Length > 0) return preIds;
            }

            var pso = new PromptSelectionOptions { MessageForAdding = prompt };
            var psr = ed.GetSelection(pso, filter);
            return psr.Status == PromptStatus.OK ? psr.Value.GetObjectIds() : null;
        }

        private static ObjectId[] FindHatchesIntersectingExtents(Database db, Polyline clipPoly)
        {
            var list = new List<ObjectId>();
            using var tr = db.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in space)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Hatch hatch && ExtentsOverlap2d(hatch, clipPoly))
                {
                    list.Add(id);
                }
            }
            tr.Commit();
            return list.ToArray();
        }

        private static bool ExtentsOverlap2d(Entity a, Entity b)
        {
            try
            {
                var ea = a.GeometricExtents;
                var eb = b.GeometricExtents;
                return !(ea.MaxPoint.X < eb.MinPoint.X - 1e-2 ||
                         ea.MinPoint.X > eb.MaxPoint.X + 1e-2 ||
                         ea.MaxPoint.Y < eb.MinPoint.Y - 1e-2 ||
                         ea.MinPoint.Y > eb.MaxPoint.Y + 1e-2);
            }
            catch
            {
                return true;
            }
        }

        private static bool ExtentsContains2d(Extents3d outer, Extents3d inner, double tol)
        {
            return inner.MinPoint.X >= outer.MinPoint.X - tol &&
                   inner.MaxPoint.X <= outer.MaxPoint.X + tol &&
                   inner.MinPoint.Y >= outer.MinPoint.Y - tol &&
                   inner.MaxPoint.Y <= outer.MaxPoint.Y + tol;
        }

        private static bool IsLayerLocked(Transaction tr, LayerTable lt, string layerName)
        {
            if (string.IsNullOrEmpty(layerName) || !lt.Has(layerName)) return false;
            return ((LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead)).IsLocked;
        }

        private static string DescribeClipMode(string mode)
        {
            switch (mode)
            {
                case "K": return "K-拆分挖出(保留)";
                case "I": return "I-仅留框内";
                default: return "S-挖除框内";
            }
        }

        private static Polyline? DisposeNull(Polyline poly)
        {
            try { poly.Dispose(); } catch { }
            return null;
        }

        private static IDisposable DisposeList(List<LoopRecord> list) => new ListDisposer(list);

        private sealed class ListDisposer : IDisposable
        {
            private readonly List<LoopRecord> _list;
            public ListDisposer(List<LoopRecord> list) => _list = list;
            public void Dispose()
            {
                foreach (var item in _list) item.Dispose();
            }
        }
    }
}
