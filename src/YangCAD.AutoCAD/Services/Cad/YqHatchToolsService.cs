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
    public static class YqHatchToolsService
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

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    foreach (var id in candidateIds)
                    {
                        if (!(tr.GetObject(id, OpenMode.ForWrite) is Hatch hatch)) continue;
                        if (IsLayerLocked(tr, lt, hatch.Layer)) continue;
                        if (!ExtentsOverlap2d(hatch, clipPolyWcs)) continue;

                        if (TryClipSingleHatch(tr, space, hatch, clipPolyWcs, chosenMode,
                            out var addedCount, out var deleted, out var usedFallback))
                        {
                            processed++;
                            createdSplit += addedCount;
                            if (deleted) erasedEmpty++;
                            if (usedFallback) fallbackUsed++;
                        }
                    }

                    tr.Commit();
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
                    if (tr.GetObject(id, OpenMode.ForWrite) is Hatch h && !IsLayerLocked(tr, lt, h.Layer))
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
                    if (TryBuildHatchCompositeRegion(h, localOrigin, out var hReg) && hReg != null)
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
                            groupIntoSingleHatch: true, updateInPlace: template, out _))
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
                        try
                        {
                            var poly = ConvertLoopToPolyline(h, h.GetLoopAt(li));
                            if (poly != null)
                            {
                                using (poly)
                                {
                                    var ltFlag = (h.LoopTypeAt(li) & HatchLoopTypes.External) != 0
                                        ? HatchLoopTypes.External
                                        : HatchLoopTypes.Default;
                                    AppendPolylineAsLoop(template, poly, ltFlag, new Vector2d(0.0, 0.0));
                                    addedLoops++;
                                }
                            }
                        }
                        catch { }
                    }
                    h.Erase();
                }

                try { template.EvaluateHatch(false); } catch { }
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

            if (TryBuildHatchCompositeRegion(hatch, localOrigin, out var hatchRegion) && hatchRegion != null)
            {
                using (hatchRegion)
                {
                    if (mode == "S")
                    {
                        using var subReg = (Region)hatchRegion.Clone();
                        using var cReg = (Region)clipRegion.Clone();
                        try
                        {
                            subReg.BooleanOperation(BooleanOperationType.BoolSubtract, cReg);
                            if (subReg.Area < AreaTol)
                            {
                                hatch.Erase();
                                erasedOriginal = true;
                                return true;
                            }

                            if (TryPopulateHatchFromRegion(tr, space, hatch, subReg, localOrigin,
                                groupIntoSingleHatch: true, updateInPlace: hatch, out _))
                                return true;
                        }
                        catch { }
                    }
                    else if (mode == "I")
                    {
                        using var intReg = (Region)hatchRegion.Clone();
                        using var cReg = (Region)clipRegion.Clone();
                        try
                        {
                            intReg.BooleanOperation(BooleanOperationType.BoolIntersect, cReg);
                            if (intReg.Area < AreaTol)
                            {
                                hatch.Erase();
                                erasedOriginal = true;
                                return true;
                            }

                            if (TryPopulateHatchFromRegion(tr, space, hatch, intReg, localOrigin,
                                groupIntoSingleHatch: true, updateInPlace: hatch, out _))
                                return true;
                        }
                        catch { }
                    }
                    else
                    {
                        // mode == "K": 从原填充中挖掉该区域，但不删除，生成独立新填充
                        using var outsideReg = (Region)hatchRegion.Clone();
                        using var insideReg = (Region)hatchRegion.Clone();
                        using var cReg1 = (Region)clipRegion.Clone();
                        using var cReg2 = (Region)clipRegion.Clone();
                        try
                        {
                            outsideReg.BooleanOperation(BooleanOperationType.BoolSubtract, cReg1);
                            insideReg.BooleanOperation(BooleanOperationType.BoolIntersect, cReg2);

                            if (insideReg.Area < AreaTol) return false;

                            if (outsideReg.Area < AreaTol)
                            {
                                return TryPopulateHatchFromRegion(tr, space, hatch, insideReg, localOrigin,
                                    groupIntoSingleHatch: true, updateInPlace: hatch, out _);
                            }

                            var outOk = TryPopulateHatchFromRegion(tr, space, hatch, outsideReg, localOrigin,
                                groupIntoSingleHatch: true, updateInPlace: hatch, out _);
                            if (outOk)
                            {
                                if (TryPopulateHatchFromRegion(tr, space, hatch, insideReg, localOrigin,
                                    groupIntoSingleHatch: false, updateInPlace: null, out var splitPieces))
                                {
                                    extraCreated += splitPieces.Count;
                                }
                                return true;
                            }
                        }
                        catch { }
                    }
                }
            }

            // ACIS 无法处理异常环时，使用严格二维凸包/矩形精确多边形裁切引擎，绝不直接叠加未裁切的框作为黑洞
            usedFallback = true;
            return TryClipBy2dPolygonFallback(tr, space, hatch, clipPolyOcs, localOrigin, mode, out extraCreated, out erasedOriginal);
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

            var clipPoly2d = FlattenPolyline2d(clipPolyOcsLocal, 24);
            if (clipPoly2d.Count < 3 || !IsConvexPolygonCcw(clipPoly2d)) return false;

            var rawPolys = new List<(List<Point2d> Pts, double Area)>();
            for (var i = 0; i < hatch.NumberOfLoops; i++)
            {
                using var lp = ConvertLoopToPolyline(hatch, hatch.GetLoopAt(i), -localOrigin.GetAsVector());
                if (lp == null) continue;
                var pts = FlattenPolyline2d(lp, 24);
                if (pts.Count < 3) continue;
                var area = Math.Abs(ComputeSignedArea2d(pts));
                if (area < AreaTol) continue;
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
            if (insidePolys.Count == 0) return false;
            if (outsidePolys.Count == 0)
            {
                RebuildHatchFrom2dPolygons(hatch, insidePolys, insideHoles, localOrigin);
                return true;
            }

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
            try { target.EvaluateHatch(false); } catch { }
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

                    var createdPieces = 0;
                    if (TryPopulateHatchFromRegion(tr, space, hatch, sideA, localOrigin,
                        groupIntoSingleHatch: false, updateInPlace: hatch, out var piecesA))
                    {
                        createdPieces += piecesA.Count;
                    }
                    if (TryPopulateHatchFromRegion(tr, space, hatch, sideB, localOrigin,
                        groupIntoSingleHatch: false, updateInPlace: null, out var piecesB))
                    {
                        createdPieces += piecesB.Count;
                    }
                    return createdPieces;
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

        // ============================================================
        // 环提取、统一 CCW 定向、局部坐标对齐与 Region 布尔重构
        // ============================================================
        private static bool TryExtractLoopHierarchy(
            Hatch hatch,
            Point2d localOrigin,
            out List<LoopRecord> records,
            out List<(int OuterIndex, List<int> HoleIndices)> islands)
        {
            records = new List<LoopRecord>();
            islands = new List<(int OuterIndex, List<int> HoleIndices)>();
            var shift = -localOrigin.GetAsVector();

            for (var i = 0; i < hatch.NumberOfLoops; i++)
            {
                var loop = hatch.GetLoopAt(i);
                var poly = ConvertLoopToPolyline(hatch, loop, shift);
                if (poly == null) continue;

                var reg = CreateRegionFromCurve(poly);
                if (reg == null || reg.Area < AreaTol)
                {
                    try { reg?.Dispose(); } catch { }
                    poly.Dispose();
                    continue;
                }

                records.Add(new LoopRecord(poly)
                {
                    Region = reg,
                    Area = reg.Area,
                    Extents = poly.GeometricExtents
                });
            }

            if (records.Count == 0) return false;

            records.Sort((a, b) => b.Area.CompareTo(a.Area));

            for (var i = 0; i < records.Count; i++)
            {
                var containers = new List<int>();
                for (var j = 0; j < i; j++)
                {
                    if (records[j].Area <= records[i].Area * 1.0001) continue;
                    if (!ExtentsContains2d(records[j].Extents, records[i].Extents, 1e-2)) continue;
                    if (IsRegionContainedIn(records[i].Region!, records[j].Region!, records[i].Area))
                    {
                        containers.Add(j);
                    }
                }
                records[i].Depth = containers.Count;
                if (containers.Count > 0)
                {
                    records[i].ParentIndex = containers[containers.Count - 1];
                }
            }

            var islandMap = new Dictionary<int, List<int>>();
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Depth % 2 == 0)
                {
                    islandMap[i] = new List<int>();
                }
                else
                {
                    var p = records[i].ParentIndex;
                    if (p >= 0 && islandMap.TryGetValue(p, out var holes))
                    {
                        holes.Add(i);
                    }
                }
            }

            foreach (var kv in islandMap)
            {
                islands.Add((kv.Key, kv.Value));
            }
            return islands.Count > 0;
        }

        private static bool TryBuildHatchCompositeRegion(Hatch hatch, Point2d localOrigin, out Region? composite)
        {
            composite = null;
            if (!TryExtractLoopHierarchy(hatch, localOrigin, out var records, out var islands))
                return false;

            using (DisposeList(records))
            {
                Region? total = null;
                try
                {
                    foreach (var (outerIdx, holeIndices) in islands)
                    {
                        using var shell = (Region)records[outerIdx].Region!.Clone();
                        foreach (var hi in holeIndices)
                        {
                            using var hole = (Region)records[hi].Region!.Clone();
                            shell.BooleanOperation(BooleanOperationType.BoolSubtract, hole);
                        }

                        if (shell.Area < AreaTol) continue;

                        if (total == null)
                        {
                            total = (Region)shell.Clone();
                        }
                        else
                        {
                            total.BooleanOperation(BooleanOperationType.BoolUnite, shell);
                        }
                    }

                    composite = total;
                    return composite != null && composite.Area >= AreaTol;
                }
                catch
                {
                    try { total?.Dispose(); } catch { }
                    return false;
                }
            }
        }

        private static bool TryPopulateHatchFromRegion(
            Transaction tr,
            BlockTableRecord space,
            Hatch template,
            Region region,
            Point2d localOrigin,
            bool groupIntoSingleHatch,
            Hatch? updateInPlace,
            out List<Hatch> resultingHatches)
        {
            resultingHatches = new List<Hatch>();
            var loopPolys = ExplodeRegionToClosedPolylines(region, template.Normal, template.Elevation);
            if (loopPolys.Count == 0) return false;

            var records = new List<LoopRecord>();
            var shiftBack = localOrigin.GetAsVector();

            try
            {
                foreach (var poly in loopPolys)
                {
                    var reg = CreateRegionFromCurve(poly);
                    if (reg == null || reg.Area < AreaTol)
                    {
                        try { reg?.Dispose(); } catch { }
                        poly.Dispose();
                        continue;
                    }
                    records.Add(new LoopRecord(poly)
                    {
                        Region = reg,
                        Area = reg.Area,
                        Extents = poly.GeometricExtents
                    });
                }

                if (records.Count == 0) return false;

                records.Sort((a, b) => b.Area.CompareTo(a.Area));
                for (var i = 0; i < records.Count; i++)
                {
                    var containers = new List<int>();
                    for (var j = 0; j < i; j++)
                    {
                        if (records[j].Area <= records[i].Area * 1.0001) continue;
                        if (!ExtentsContains2d(records[j].Extents, records[i].Extents, 1e-2)) continue;
                        if (IsRegionContainedIn(records[i].Region!, records[j].Region!, records[i].Area))
                        {
                            containers.Add(j);
                        }
                    }
                    records[i].Depth = containers.Count;
                    if (containers.Count > 0) records[i].ParentIndex = containers[containers.Count - 1];
                }

                var islands = new Dictionary<int, List<int>>();
                for (var i = 0; i < records.Count; i++)
                {
                    if (records[i].Depth % 2 == 0)
                    {
                        islands[i] = new List<int>();
                    }
                    else
                    {
                        var p = records[i].ParentIndex;
                        if (p >= 0 && islands.TryGetValue(p, out var holes)) holes.Add(i);
                    }
                }

                if (islands.Count == 0) return false;
                FilterSliverIslands(records, islands);
                if (islands.Count == 0) return false;

                if (groupIntoSingleHatch)
                {
                    var target = updateInPlace ?? CreateEmptyClone(template);
                    if (updateInPlace != null)
                    {
                        target.Associative = false;
                        try { target.RemoveAssociatedObjectIds(); } catch { }
                        while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);
                    }

                    foreach (var kv in islands)
                    {
                        AppendPolylineAsLoop(target, records[kv.Key].Polyline, HatchLoopTypes.External, shiftBack);
                        foreach (var hi in kv.Value)
                            AppendPolylineAsLoop(target, records[hi].Polyline, HatchLoopTypes.Default, shiftBack);
                    }

                    try { target.EvaluateHatch(false); } catch { }

                    if (updateInPlace == null)
                    {
                        space.AppendEntity(target);
                        tr.AddNewlyCreatedDBObject(target, true);
                    }
                    resultingHatches.Add(target);
                    return true;
                }

                var idx = 0;
                foreach (var kv in islands)
                {
                    var useExisting = updateInPlace != null && idx == 0;
                    var target = useExisting ? updateInPlace! : CreateEmptyClone(template);
                    if (useExisting)
                    {
                        target.Associative = false;
                        try { target.RemoveAssociatedObjectIds(); } catch { }
                        while (target.NumberOfLoops > 0) target.RemoveLoopAt(target.NumberOfLoops - 1);
                    }

                    AppendPolylineAsLoop(target, records[kv.Key].Polyline, HatchLoopTypes.External, shiftBack);
                    foreach (var hi in kv.Value)
                        AppendPolylineAsLoop(target, records[hi].Polyline, HatchLoopTypes.Default, shiftBack);

                    try { target.EvaluateHatch(false); } catch { }

                    if (!useExisting)
                    {
                        space.AppendEntity(target);
                        tr.AddNewlyCreatedDBObject(target, true);
                    }
                    resultingHatches.Add(target);
                    idx++;
                }

                return resultingHatches.Count > 0;
            }
            finally
            {
                foreach (var r in records) r.Dispose();
            }
        }

        private static bool IsSliverGeometry(double area, double perimeter, double maxArea, double maxThickness)
        {
            if (perimeter <= 1e-8) return true;
            var thickness = 2.0 * area / perimeter;
            return thickness < 0.25 && thickness < maxThickness * 0.01 && area < maxArea * 0.005;
        }

        private static void FilterSliverIslands(List<LoopRecord> records, Dictionary<int, List<int>> islands)
        {
            if (islands.Count <= 1) return;
            var metrics = new List<(int Key, double Area, double Perim)>(islands.Count);
            var maxArea = 0.0;
            var maxThick = 0.0;
            foreach (var key in islands.Keys)
            {
                var area = records[key].Area;
                var perim = ComputePolylinePerimeter2d(records[key].Polyline);
                var thick = perim > 1e-8 ? 2.0 * area / perim : 0.0;
                metrics.Add((key, area, perim));
                if (area > maxArea) maxArea = area;
                if (thick > maxThick) maxThick = thick;
            }

            foreach (var m in metrics)
            {
                if (IsSliverGeometry(m.Area, m.Perim, maxArea, maxThick))
                {
                    islands.Remove(m.Key);
                }
            }
        }

        private static void FilterSliverPolys2d(List<List<Point2d>> polys)
        {
            if (polys.Count <= 1) return;
            var metrics = new List<(double Area, double Perim)>(polys.Count);
            var maxArea = 0.0;
            var maxThick = 0.0;
            for (var i = 0; i < polys.Count; i++)
            {
                var area = Math.Abs(ComputeSignedArea2d(polys[i]));
                var perim = ComputePolygonPerimeter2d(polys[i]);
                var thick = perim > 1e-8 ? 2.0 * area / perim : 0.0;
                metrics.Add((area, perim));
                if (area > maxArea) maxArea = area;
                if (thick > maxThick) maxThick = thick;
            }

            for (var i = polys.Count - 1; i >= 0; i--)
            {
                if (IsSliverGeometry(metrics[i].Area, metrics[i].Perim, maxArea, maxThick))
                {
                    polys.RemoveAt(i);
                }
            }
        }

        private static double ComputePolylinePerimeter2d(Polyline poly)
        {
            try
            {
                var len = poly.Length;
                if (len > 1e-8 && !double.IsNaN(len) && !double.IsInfinity(len)) return len;
            }
            catch { }

            return ComputePolygonPerimeter2d(ExtractVerticesList(poly));
        }

        private static List<Point2d> ExtractVerticesList(Polyline poly)
        {
            var n = poly.NumberOfVertices;
            var pts = new List<Point2d>(n);
            for (var i = 0; i < n; i++) pts.Add(poly.GetPoint2dAt(i));
            return pts;
        }

        private static double ComputePolygonPerimeter2d(IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 2) return 0.0;
            var sum = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                sum += pts[i].GetDistanceTo(pts[(i + 1) % pts.Count]);
            }
            return sum;
        }

        private static bool IsRegionContainedIn(Region inner, Region outer, double innerArea)
        {
            using var cloneInner = (Region)inner.Clone();
            using var cloneOuter = (Region)outer.Clone();
            try
            {
                cloneInner.BooleanOperation(BooleanOperationType.BoolIntersect, cloneOuter);
                return cloneInner.Area >= innerArea * 0.5;
            }
            catch
            {
                return false;
            }
        }

        private static Hatch CreateEmptyClone(Hatch src)
        {
            var dst = (Hatch)src.Clone();
            dst.Associative = false;
            try { dst.RemoveAssociatedObjectIds(); } catch { }
            while (dst.NumberOfLoops > 0)
            {
                dst.RemoveLoopAt(dst.NumberOfLoops - 1);
            }
            dst.Origin = src.Origin;
            return dst;
        }

        private static void AppendPolylineAsLoop(Hatch hatch, Polyline poly, HatchLoopTypes baseType, Vector2d offset)
        {
            var (pts, bulges) = ExtractVertices2d(poly, offset);
            if (pts.Count < 2) return;
            if (pts.Count == 2 && Math.Abs(bulges[0]) < 1e-5 && Math.Abs(bulges[1]) < 1e-5) return;
            hatch.AppendLoop(baseType | HatchLoopTypes.Polyline, pts, bulges);
        }

        private static (Point2dCollection Pts, DoubleCollection Bulges) ExtractVertices2d(Polyline poly, Vector2d offset)
        {
            var pts = new Point2dCollection();
            var bulges = new DoubleCollection();
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                pts.Add(poly.GetPoint2dAt(i) + offset);
                bulges.Add(poly.GetBulgeAt(i));
            }
            return (pts, bulges);
        }

        private static Point2d ComputeHatchLocalOrigin2d(Hatch hatch)
        {
            try
            {
                var ext = hatch.GeometricExtents;
                var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, hatch.Normal));
                var pMin = ext.MinPoint.TransformBy(w2ocs);
                var pMax = ext.MaxPoint.TransformBy(w2ocs);
                return new Point2d(0.5 * (pMin.X + pMax.X), 0.5 * (pMin.Y + pMax.Y));
            }
            catch
            {
                return Point2d.Origin;
            }
        }

        // ============================================================
        // HatchLoop <-> Polyline / Curve2d / Region 转换（含首尾链拼合 & CCW 统一）
        // ============================================================
        private static Polyline? ConvertLoopToPolyline(Hatch hatch, HatchLoop loop, Vector2d shift = default)
        {
            if (loop == null) return null;

            if (loop.IsPolyline && loop.Polyline != null && loop.Polyline.Count >= 2)
            {
                var bvs = loop.Polyline;
                var count = bvs.Count;
                if (count > 2 && bvs[0].Vertex.GetDistanceTo(bvs[count - 1].Vertex) < PtTol)
                    count--;

                var poly = new Polyline
                {
                    Normal = hatch.Normal,
                    Elevation = hatch.Elevation,
                    Closed = true
                };
                for (var i = 0; i < count; i++)
                {
                    poly.AddVertexAt(i, bvs[i].Vertex + shift, bvs[i].Bulge, 0.0, 0.0);
                }
                return SanitizeAndOrientPolyline2d(poly, wantCCW: true) ? poly : DisposeNull(poly);
            }

            if (loop.Curves == null || loop.Curves.Count == 0) return null;

            // 单圆弧闭环（完整圆）
            if (loop.Curves.Count == 1 && loop.Curves[0] is CircularArc2d singleArc &&
                singleArc.StartPoint.GetDistanceTo(singleArc.EndPoint) < PtTol)
            {
                var p0 = singleArc.StartPoint + shift;
                var c = singleArc.Center + shift;
                var opp = new Point2d(2 * c.X - p0.X, 2 * c.Y - p0.Y);
                var b = singleArc.IsClockWise ? -1.0 : 1.0;
                var circlePoly = new Polyline
                {
                    Normal = hatch.Normal,
                    Elevation = hatch.Elevation,
                    Closed = true
                };
                circlePoly.AddVertexAt(0, p0, b, 0.0, 0.0);
                circlePoly.AddVertexAt(1, opp, b, 0.0, 0.0);
                return SanitizeAndOrientPolyline2d(circlePoly, wantCCW: true) ? circlePoly : DisposeNull(circlePoly);
            }

            // 将 Curve2dCollection 标准化为段并首尾链拼合（防止部分曲线方向颠倒导致缺顶点）
            var segs = new List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)>();
            foreach (Curve2d c2 in loop.Curves)
            {
                if (c2 is LineSegment2d ls)
                {
                    var s = ls.StartPoint + shift;
                    var e = ls.EndPoint + shift;
                    if (s.GetDistanceTo(e) > 1e-8)
                        segs.Add((s, e, 0.0, null));
                }
                else if (c2 is CircularArc2d ca)
                {
                    var iv = ca.GetInterval();
                    var mid = ca.EvaluatePoint((iv.LowerBound + iv.UpperBound) * 0.5) + shift;
                    var s = ca.StartPoint + shift;
                    var e = ca.EndPoint + shift;
                    var bulge = ComputeThreePointBulge(s, mid, e);
                    segs.Add((s, e, bulge, null));
                }
                else
                {
                    var sampled = SampleCurve2d(c2, 24).Select(p => p + shift).ToList();
                    if (sampled.Count >= 2)
                        segs.Add((sampled[0], sampled[sampled.Count - 1], 0.0, sampled));
                }
            }

            var chainedPolys = ChainSegmentsIntoClosedPolylines(segs, hatch.Normal, hatch.Elevation);
            if (chainedPolys.Count == 0)
            {
                // HPGAPTOL 容差边界若存在 > 0.05 微小缺口，用无距离上限单环桥接兜底
                chainedPolys = ChainSingleHatchLoopSegments(segs, hatch.Normal, hatch.Elevation);
            }
            if (chainedPolys.Count == 0) return null;

            // 返回最大环并清理多余临时环
            chainedPolys.Sort((a, b) => Math.Abs(GetPolylineSignedArea2d(b)).CompareTo(Math.Abs(GetPolylineSignedArea2d(a))));
            for (var i = 1; i < chainedPolys.Count; i++) chainedPolys[i].Dispose();
            return chainedPolys[0];
        }

        private static List<Polyline> ExplodeRegionToClosedPolylines(Region rootRegion, Vector3d normal, double elevation)
        {
            var result = new List<Polyline>();
            ExplodeRegionRecursive(rootRegion, normal, elevation, result);
            return result;
        }

        private static void ExplodeRegionRecursive(Region region, Vector3d normal, double elevation, List<Polyline> output)
        {
            using var objs = new DBObjectCollection();
            try
            {
                region.Explode(objs);
            }
            catch
            {
                return;
            }

            var childRegions = new List<Region>();
            var curves = new List<Curve>();

            foreach (DBObject obj in objs)
            {
                if (obj is Region child) childRegions.Add(child);
                else if (obj is Curve curve) curves.Add(curve);
                else { try { obj.Dispose(); } catch { } }
            }

            // 每个子 Region 各自独立 Explode 并串联边界，彻底防止不同岛屿/孔洞边界混串
            foreach (var child in childRegions)
            {
                using (child)
                {
                    ExplodeRegionRecursive(child, normal, elevation, output);
                }
            }

            if (curves.Count == 0) return;

            var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, normal));
            var openSegs = new List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)>();

            foreach (var c in curves)
            {
                using (c)
                {
                    if (c is Circle circle)
                    {
                        var centerOcs = circle.Center.TransformBy(w2ocs);
                        var p0 = new Point2d(centerOcs.X + circle.Radius, centerOcs.Y);
                        var p1 = new Point2d(centerOcs.X - circle.Radius, centerOcs.Y);
                        var p = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
                        p.AddVertexAt(0, p0, 1.0, 0.0, 0.0);
                        p.AddVertexAt(1, p1, 1.0, 0.0, 0.0);
                        if (SanitizeAndOrientPolyline2d(p, wantCCW: true)) output.Add(p);
                        else p.Dispose();
                        continue;
                    }

                    if (c is Line line)
                    {
                        var sp = line.StartPoint.TransformBy(w2ocs);
                        var ep = line.EndPoint.TransformBy(w2ocs);
                        var s2 = new Point2d(sp.X, sp.Y);
                        var e2 = new Point2d(ep.X, ep.Y);
                        if (s2.GetDistanceTo(e2) > 1e-8)
                            openSegs.Add((s2, e2, 0.0, null));
                        continue;
                    }

                    if (c is Arc arc)
                    {
                        var sp = arc.StartPoint.TransformBy(w2ocs);
                        var ep = arc.EndPoint.TransformBy(w2ocs);
                        var midWcs = arc.GetPointAtParameter((arc.StartParam + arc.EndParam) * 0.5);
                        var mp = midWcs.TransformBy(w2ocs);
                        var s2 = new Point2d(sp.X, sp.Y);
                        var m2 = new Point2d(mp.X, mp.Y);
                        var e2 = new Point2d(ep.X, ep.Y);
                        var b = ComputeThreePointBulge(s2, m2, e2);
                        openSegs.Add((s2, e2, b, null));
                        continue;
                    }

                    if (c.Closed)
                    {
                        var closedPoly = SampleCurve3dToPolyline(c, w2ocs, normal, elevation, closed: true);
                        if (closedPoly != null) output.Add(closedPoly);
                    }
                    else
                    {
                        var pts = SampleCurve3dPoints(c, w2ocs, 32);
                        if (pts.Count >= 2)
                            openSegs.Add((pts[0], pts[pts.Count - 1], 0.0, pts));
                    }
                }
            }

            if (openSegs.Count > 0)
            {
                output.AddRange(ChainSegmentsIntoClosedPolylines(openSegs, normal, elevation));
            }
        }

        private static List<Polyline> ChainSegmentsIntoClosedPolylines(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> openSegs,
            Vector3d normal,
            double elevation)
        {
            var result = new List<Polyline>();
            var used = new bool[openSegs.Count];

            for (var startIdx = 0; startIdx < openSegs.Count; startIdx++)
            {
                if (used[startIdx]) continue;
                used[startIdx] = true;

                var chain = new List<(Point2d Pt, double Bulge)>();
                AppendSegToChain(chain, openSegs[startIdx], reverse: false);
                var chainStart = openSegs[startIdx].Start;
                var chainEnd = openSegs[startIdx].End;

                var closed = false;
                for (var guard = 0; guard < openSegs.Count + 2; guard++)
                {
                    if (chainEnd.GetDistanceTo(chainStart) < 5e-2 && IsValidLoopChain(chain))
                    {
                        if (chainEnd.GetDistanceTo(chainStart) > 1e-5)
                            chain.Add((chainEnd, 0.0));
                        closed = true;
                        break;
                    }

                    var (bestIdx, bestRev, bestDist) = FindNearestUnusedSeg(openSegs, used, chainEnd, 5e-2);
                    if (bestIdx < 0) break;
                    used[bestIdx] = true;
                    if (bestDist > 1e-5)
                        chain.Add((chainEnd, 0.0));
                    AppendSegToChain(chain, openSegs[bestIdx], bestRev);
                    chainEnd = bestRev ? openSegs[bestIdx].Start : openSegs[bestIdx].End;
                }

                if (closed && TryBuildPolylineFromChain(chain, normal, elevation, out var poly) && poly != null)
                {
                    result.Add(poly);
                }
            }

            return result;
        }

        private static List<Polyline> ChainSingleHatchLoopSegments(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> segs,
            Vector3d normal,
            double elevation)
        {
            var result = new List<Polyline>();
            if (segs.Count == 0) return result;

            var used = new bool[segs.Count];
            used[0] = true;

            var chain = new List<(Point2d Pt, double Bulge)>();
            AppendSegToChain(chain, segs[0], reverse: false);
            var chainStart = segs[0].Start;
            var chainEnd = segs[0].End;

            for (var step = 1; step < segs.Count; step++)
            {
                var (bestIdx, bestRev, bestDist) = FindNearestUnusedSeg(segs, used, chainEnd, double.MaxValue);
                if (bestIdx < 0) break;
                used[bestIdx] = true;
                if (bestDist > 1e-5)
                    chain.Add((chainEnd, 0.0));
                AppendSegToChain(chain, segs[bestIdx], bestRev);
                chainEnd = bestRev ? segs[bestIdx].Start : segs[bestIdx].End;
            }

            if (chainEnd.GetDistanceTo(chainStart) > 1e-5)
                chain.Add((chainEnd, 0.0));

            if (TryBuildPolylineFromChain(chain, normal, elevation, out var poly) && poly != null)
            {
                result.Add(poly);
            }

            return result;
        }

        private static (int Index, bool Reverse, double Distance) FindNearestUnusedSeg(
            List<(Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts)> segs,
            bool[] used,
            Point2d fromPt,
            double maxDist)
        {
            var bestIdx = -1;
            var bestRev = false;
            var bestDist = maxDist;

            for (var j = 0; j < segs.Count; j++)
            {
                if (used[j]) continue;
                var d1 = fromPt.GetDistanceTo(segs[j].Start);
                if (d1 < bestDist)
                {
                    bestDist = d1;
                    bestIdx = j;
                    bestRev = false;
                }
                var d2 = fromPt.GetDistanceTo(segs[j].End);
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    bestIdx = j;
                    bestRev = true;
                }
            }

            return (bestIdx, bestRev, bestDist);
        }

        private static bool TryBuildPolylineFromChain(
            List<(Point2d Pt, double Bulge)> chain,
            Vector3d normal,
            double elevation,
            out Polyline? poly)
        {
            poly = null;
            if (!IsValidLoopChain(chain)) return false;

            var candidate = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            for (var vi = 0; vi < chain.Count; vi++)
            {
                candidate.AddVertexAt(vi, chain[vi].Pt, chain[vi].Bulge, 0.0, 0.0);
            }
            if (!SanitizeAndOrientPolyline2d(candidate, wantCCW: true))
            {
                candidate.Dispose();
                return false;
            }

            poly = candidate;
            return true;
        }

        private static bool IsValidLoopChain(List<(Point2d Pt, double Bulge)> chain)
        {
            if (chain.Count >= 3) return true;
            return chain.Count == 2 && (Math.Abs(chain[0].Bulge) > 1e-5 || Math.Abs(chain[1].Bulge) > 1e-5);
        }

        private static void AppendSegToChain(
            List<(Point2d Pt, double Bulge)> chain,
            (Point2d Start, Point2d End, double Bulge, List<Point2d>? PolyPts) seg,
            bool reverse)
        {
            if (seg.PolyPts != null && seg.PolyPts.Count >= 2)
            {
                if (!reverse)
                {
                    for (var i = 0; i < seg.PolyPts.Count - 1; i++)
                        chain.Add((seg.PolyPts[i], 0.0));
                }
                else
                {
                    for (var i = seg.PolyPts.Count - 1; i > 0; i--)
                        chain.Add((seg.PolyPts[i], 0.0));
                }
                return;
            }

            if (!reverse)
                chain.Add((seg.Start, seg.Bulge));
            else
                chain.Add((seg.End, -seg.Bulge));
        }

        // ============================================================
        // 清理重复顶点 & 共线/毛刺退化点 & 强制统一 CCW 方向
        // ============================================================
        private static bool SanitizeAndOrientPolyline2d(Polyline poly, bool wantCCW)
        {
            if (poly == null || poly.NumberOfVertices < 2) return false;

            const double mergeTol = 1e-3;
            var verts = new List<(Point2d Pt, double Bulge)>();
            for (var i = 0; i < poly.NumberOfVertices; i++)
            {
                var pt = poly.GetPoint2dAt(i);
                var b = poly.GetBulgeAt(i);
                if (verts.Count == 0 || verts[verts.Count - 1].Pt.GetDistanceTo(pt) > mergeTol)
                {
                    verts.Add((pt, b));
                }
                else if (Math.Abs(verts[verts.Count - 1].Bulge) < 1e-6 && Math.Abs(b) > 1e-6)
                {
                    verts[verts.Count - 1] = (verts[verts.Count - 1].Pt, b);
                }
            }

            if (verts.Count > 2 && verts[0].Pt.GetDistanceTo(verts[verts.Count - 1].Pt) <= mergeTol)
            {
                verts.RemoveAt(verts.Count - 1);
            }

            RemoveCollinearAndSpikeVertices2d(verts);

            if (verts.Count < 2) return false;
            if (verts.Count == 2 && Math.Abs(verts[0].Bulge) < 1e-5 && Math.Abs(verts[1].Bulge) < 1e-5)
                return false;

            var signedArea = ComputeSignedAreaWithBulge2d(verts);
            if (Math.Abs(signedArea) < AreaTol) return false;

            if ((wantCCW && signedArea < 0) || (!wantCCW && signedArea > 0))
            {
                var n = verts.Count;
                var rev = new List<(Point2d Pt, double Bulge)>(n);
                for (var k = 0; k < n; k++)
                {
                    var srcIdx = k == 0 ? 0 : n - k;
                    var edgeIdx = (n - k - 1 + n) % n;
                    rev.Add((verts[srcIdx].Pt, -verts[edgeIdx].Bulge));
                }
                verts = rev;
            }

            while (poly.NumberOfVertices > verts.Count)
                poly.RemoveVertexAt(poly.NumberOfVertices - 1);
            while (poly.NumberOfVertices < verts.Count)
                poly.AddVertexAt(poly.NumberOfVertices, Point2d.Origin, 0.0, 0.0, 0.0);

            for (var i = 0; i < verts.Count; i++)
            {
                poly.SetPointAt(i, verts[i].Pt);
                poly.SetBulgeAt(i, verts[i].Bulge);
            }
            poly.Closed = true;
            return true;
        }

        private static void RemoveCollinearAndSpikeVertices2d(List<(Point2d Pt, double Bulge)> verts)
        {
            var changed = true;
            while (changed && verts.Count >= 3)
            {
                changed = false;
                for (var i = 0; i < verts.Count; i++)
                {
                    var n = verts.Count;
                    var prev = (i - 1 + n) % n;
                    var next = (i + 1) % n;

                    // 仅在前后边均为直线时清理，避免改动圆弧顶点或 bulge
                    if (Math.Abs(verts[prev].Bulge) >= 1e-6 || Math.Abs(verts[i].Bulge) >= 1e-6)
                        continue;

                    var a = verts[prev].Pt;
                    var b = verts[i].Pt;
                    var c = verts[next].Pt;
                    var v1 = b - a;
                    var v2 = c - b;
                    var len1 = v1.Length;
                    var len2 = v2.Length;

                    if (len1 < 1e-8 || len2 < 1e-8)
                    {
                        verts.RemoveAt(i);
                        changed = true;
                        break;
                    }

                    var dot = v1.DotProduct(v2);
                    var crossAbs = Math.Abs(v1.X * v2.Y - v1.Y * v2.X);

                    var ac = c - a;
                    var lenAc = ac.Length;
                    if (dot > 0.0 && lenAc > 1e-8)
                    {
                        var perp = Math.Abs((b.X - a.X) * ac.Y - (b.Y - a.Y) * ac.X) / lenAc;
                        if (perp < 1e-5)
                        {
                            verts.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }

                    if (dot < 0.0 && (a.GetDistanceTo(c) < 1e-5 || (crossAbs / len1 < 1e-5 && crossAbs / len2 < 1e-5)))
                    {
                        verts.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
        }

        private static double GetPolylineSignedArea2d(Polyline poly)
        {
            var list = new List<(Point2d Pt, double Bulge)>(poly.NumberOfVertices);
            for (var i = 0; i < poly.NumberOfVertices; i++)
                list.Add((poly.GetPoint2dAt(i), poly.GetBulgeAt(i)));
            return ComputeSignedAreaWithBulge2d(list);
        }

        private static double ComputeSignedAreaWithBulge2d(IReadOnlyList<(Point2d Pt, double Bulge)> verts)
        {
            double sum = 0.0;
            var n = verts.Count;
            for (var i = 0; i < n; i++)
            {
                var p1 = verts[i].Pt;
                var p2 = verts[(i + 1) % n].Pt;
                sum += 0.5 * (p1.X * p2.Y - p2.X * p1.Y);

                var b = verts[i].Bulge;
                if (Math.Abs(b) > 1e-8)
                {
                    var chord = p1.GetDistanceTo(p2);
                    if (chord > 1e-8)
                    {
                        var absB = Math.Abs(b);
                        var theta = 4.0 * Math.Atan(absB);
                        var r = chord * (1.0 + absB * absB) / (4.0 * absB);
                        var segArea = 0.5 * r * r * (theta - Math.Sin(theta));
                        sum += b > 0 ? segArea : -segArea;
                    }
                }
            }
            return sum;
        }

        private static Region? CreateRegionFromCurve(Curve curve)
        {
            using var segs = new DBObjectCollection { curve };
            try
            {
                using var regs = Region.CreateFromCurves(segs);
                if (regs.Count == 0) return null;

                Region? primary = regs[0] as Region;
                for (var i = 1; i < regs.Count; i++)
                {
                    if (regs[i] is Region extra)
                    {
                        if (primary != null)
                        {
                            try { primary.BooleanOperation(BooleanOperationType.BoolUnite, extra); } catch { }
                        }
                        extra.Dispose();
                    }
                }
                return primary;
            }
            catch
            {
                return null;
            }
        }

        // ============================================================
        // 几何小助手（扁平化 / 凸性 / 点内判 / 三点凸度 / 平面投影）
        // ============================================================
        private static List<Point2d> FlattenPolyline2d(Polyline poly, int arcSegs)
        {
            var res = new List<Point2d>();
            var n = poly.NumberOfVertices;
            for (var i = 0; i < n; i++)
            {
                var p1 = poly.GetPoint2dAt(i);
                var p2 = poly.GetPoint2dAt((i + 1) % n);
                var b = poly.GetBulgeAt(i);
                AddUniquePt(res, p1);
                if (Math.Abs(b) > 1e-6 && p1.GetDistanceTo(p2) > PtTol)
                {
                    var arc2d = poly.GetArcSegment2dAt(i);
                    var iv = arc2d.GetInterval();
                    for (var k = 1; k < arcSegs; k++)
                    {
                        var t = iv.LowerBound + (iv.UpperBound - iv.LowerBound) * k / arcSegs;
                        AddUniquePt(res, arc2d.EvaluatePoint(t));
                    }
                }
            }
            if (res.Count > 2 && res[0].GetDistanceTo(res[res.Count - 1]) < PtTol)
                res.RemoveAt(res.Count - 1);
            return res;
        }

        private static double ComputeSignedArea2d(IReadOnlyList<Point2d> pts)
        {
            double s = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                s += 0.5 * (a.X * b.Y - b.X * a.Y);
            }
            return s;
        }

        private static bool IsConvexPolygonCcw(IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 3) return false;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                var c = pts[(i + 2) % pts.Count];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross < -1e-6) return false;
            }
            return true;
        }

        private static Point2d FindPointInsideOrOnPolygon(IReadOnlyList<Point2d> pts)
        {
            double cx = 0, cy = 0;
            foreach (var p in pts) { cx += p.X; cy += p.Y; }
            var cen = new Point2d(cx / pts.Count, cy / pts.Count);
            if (IsPointInPolygon2d(cen, pts)) return cen;
            return new Point2d((pts[0].X + pts[1].X) * 0.5, (pts[0].Y + pts[1].Y) * 0.5);
        }

        private static bool IsPointInPolygon2d(Point2d pt, IReadOnlyList<Point2d> poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var xi = poly[i].X; var yi = poly[i].Y;
                var xj = poly[j].X; var yj = poly[j].Y;
                if (((yi > pt.Y) != (yj > pt.Y)) &&
                    (pt.X < (xj - xi) * (pt.Y - yi) / ((yj - yi) + 1e-18) + xi))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static double ComputeThreePointBulge(Point2d start, Point2d mid, Point2d end)
        {
            var ax = start.X; var ay = start.Y;
            var bx = mid.X; var by = mid.Y;
            var cx = end.X; var cy = end.Y;

            var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-10) return 0.0;

            var aSq = ax * ax + ay * ay;
            var bSq = bx * bx + by * by;
            var cSq = cx * cx + cy * cy;

            var ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / d;
            var uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / d;
            var center = new Point2d(ux, uy);

            var v1 = start - center;
            var vm = mid - center;
            var v2 = end - center;

            var a1 = Math.Atan2(v1.Y, v1.X);
            var am = Math.Atan2(vm.Y, vm.X);
            var a2 = Math.Atan2(v2.Y, v2.X);

            var ccwTotal = PositiveMod(a2 - a1, 2.0 * Math.PI);
            var ccwMid = PositiveMod(am - a1, 2.0 * Math.PI);

            if (ccwMid <= ccwTotal + 1e-6)
                return Math.Tan(ccwTotal / 4.0);

            var cwTotal = 2.0 * Math.PI - ccwTotal;
            return -Math.Tan(cwTotal / 4.0);
        }

        private static double PositiveMod(double val, double mod)
        {
            var r = val % mod;
            return r < 0 ? r + mod : r;
        }

        private static List<Point2d> SampleCurve2d(Curve2d c2, int count)
        {
            var pts = new List<Point2d>(count + 1);
            var iv = c2.GetInterval();
            var t0 = iv.LowerBound;
            var t1 = iv.UpperBound;
            for (var i = 0; i <= count; i++)
            {
                var t = t0 + (t1 - t0) * i / count;
                pts.Add(c2.EvaluatePoint(t));
            }
            return pts;
        }

        private static List<Point2d> SampleCurve3dPoints(Curve curve, Matrix3d w2ocs, int count)
        {
            var pts = new List<Point2d>(count + 1);
            var t0 = curve.StartParam;
            var t1 = curve.EndParam;
            for (var i = 0; i <= count; i++)
            {
                var t = t0 + (t1 - t0) * i / count;
                var p = curve.GetPointAtParameter(t).TransformBy(w2ocs);
                var p2 = new Point2d(p.X, p.Y);
                if (pts.Count == 0 || pts[pts.Count - 1].GetDistanceTo(p2) > PtTol || i == count)
                    pts.Add(p2);
            }
            return pts;
        }

        private static Polyline? SampleCurve3dToPolyline(Curve curve, Matrix3d w2ocs, Vector3d normal, double elevation, bool closed)
        {
            var pts = SampleCurve3dPoints(curve, w2ocs, 48);
            if (closed && pts.Count > 2 && pts[0].GetDistanceTo(pts[pts.Count - 1]) < PtTol)
                pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return null;

            var poly = new Polyline { Normal = normal, Elevation = elevation, Closed = closed };
            for (var i = 0; i < pts.Count; i++)
                poly.AddVertexAt(i, pts[i], 0.0, 0.0, 0.0);
            if (closed && !SanitizeAndOrientPolyline2d(poly, wantCCW: true)) return DisposeNull(poly);
            return poly;
        }

        private static Polyline? ProjectWcsPolylineToHatchPlane(
            Polyline polyWcs,
            Vector3d normal,
            double elevation,
            Point2d localOrigin)
        {
            var w2ocs = Matrix3d.WorldToPlane(new Plane(Point3d.Origin, normal));
            var outPoly = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            var n = polyWcs.NumberOfVertices;
            if (n < 2) return null;

            var sign = polyWcs.Normal.DotProduct(normal) >= 0 ? 1.0 : -1.0;

            for (var i = 0; i < n; i++)
            {
                var ptWcs = polyWcs.GetPoint3dAt(i);
                var ptOcs = ptWcs.TransformBy(w2ocs);
                var bulge = polyWcs.GetBulgeAt(i) * sign;
                outPoly.AddVertexAt(i, new Point2d(ptOcs.X - localOrigin.X, ptOcs.Y - localOrigin.Y), bulge, 0.0, 0.0);
            }

            if (!SanitizeAndOrientPolyline2d(outPoly, wantCCW: true))
                return DisposeNull(outPoly);
            return outPoly;
        }

        private static Polyline BuildOcsRectPoly(Vector3d normal, double elevation, Point2d p0, Point2d p1, Point2d p2, Point2d p3)
        {
            var p = new Polyline { Normal = normal, Elevation = elevation, Closed = true };
            p.AddVertexAt(0, p0, 0.0, 0.0, 0.0);
            p.AddVertexAt(1, p1, 0.0, 0.0, 0.0);
            p.AddVertexAt(2, p2, 0.0, 0.0, 0.0);
            p.AddVertexAt(3, p3, 0.0, 0.0, 0.0);
            SanitizeAndOrientPolyline2d(p, wantCCW: true);
            return p;
        }

        private static Region? BuildSlitRegionFromPolyline(Vector3d normal, double elevation, IReadOnlyList<Point2d> pts)
        {
            if (pts.Count < 2) return null;
            const double halfW = 0.001;
            Region? slit = null;
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var d = b - a;
                if (d.Length < 1e-6) continue;
                var u = d.GetNormal();
                var n = new Vector2d(-u.Y, u.X) * halfW;
                var aExt = a - (i == 0 ? u * 1000.0 : u * halfW);
                var bExt = b + (i == pts.Count - 2 ? u * 1000.0 : u * halfW);
                using var strip = BuildOcsRectPoly(normal, elevation, aExt - n, bExt - n, bExt + n, aExt + n);
                var piece = CreateRegionFromCurve(strip);
                if (piece == null) continue;
                if (slit == null)
                {
                    slit = piece;
                }
                else
                {
                    try { slit.BooleanOperation(BooleanOperationType.BoolUnite, piece); } catch { }
                    piece.Dispose();
                }
            }
            return slit;
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
            var ppo = new PromptPointOptions("\n指定切分线第一点 或 [选现有直线/多段线(E)]：", "E");
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
