using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class DualLineCommands
    {
        private static string AskKeyword(Editor ed, string title,
                                         string[] choices, string fallback)
        {
            var opts = new PromptKeywordOptions("\n" + title + " <" + fallback + ">：") {
                AllowNone = true
            };
            foreach (var item in choices) opts.Keywords.Add(item);
            opts.Keywords.Default = fallback;
            var result = ed.GetKeywords(opts);
            if (result.Status == PromptStatus.None) return fallback;
            return result.Status == PromptStatus.OK ? result.StringResult : "";
        }

        private static double? AskWidth(Editor ed, double priorMm, string title)
        {
            var opts = new PromptDoubleOptions(
                "\n" + title + " <" + priorMm.ToString("0.###") + " mm>：") {
                AllowNone = true, UseDefaultValue = true, DefaultValue = priorMm,
                AllowZero = false, AllowNegative = false
            };
            var result = ed.GetDouble(opts);
            if (result.Status == PromptStatus.None) return priorMm;
            return result.Status == PromptStatus.OK ? result.Value : (double?)null;
        }

        private static double? AskExtension(Editor ed, double priorMm)
        {
            var opts = new PromptDoubleOptions(
                "\n中心线两头延长（mm） <" + priorMm.ToString("0.###") + ">：") {
                AllowNone = true, UseDefaultValue = true, DefaultValue = priorMm,
                AllowZero = true, AllowNegative = false
            };
            var result = ed.GetDouble(opts);
            if (result.Status == PromptStatus.None) return priorMm;
            return result.Status == PromptStatus.OK ? result.Value : (double?)null;
        }

        [CommandMethod("YGDL", CommandFlags.Modal)]
        public void DrawDoubleLine()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var settings = UserSettings.Load();
                var multiplier = DualLineService.MillimetersPerDrawingUnit(doc.Database);
                if (Convert.ToInt32(doc.Database.Insunits) == 0)
                    ed.WriteMessage("\n[YangCAD] 图纸 INSUNITS=0，暂按毫米单位解释；请确保模型单位一致。\n");
                var mode = AskKeyword(ed, "绘制类型 [Double(双线)/Pipe(管道三线)]",
                    new[] { "Double", "Pipe" }, settings.DoubleLineMode);
                if (mode.Length == 0) return;
                var width = AskWidth(ed, settings.DoubleLineWidthMm, "实际双线宽度 / 管径");
                if (!width.HasValue) return;
                var alignment = AskKeyword(ed,
                    "轴线对齐 [Center(中心)/Left(左侧)/Right(右侧)]",
                    new[] { "Center", "Left", "Right" }, settings.DoubleLineAlignment);
                if (alignment.Length == 0) return;
                var corner = AskKeyword(ed,
                    "转角处理 [Miter(斜接)/Bevel(倒角)/Round(圆弧弯头)]",
                    new[] { "Miter", "Bevel", "Round" }, settings.DoubleLineCorner);
                if (corner.Length == 0) return;
                var extension = mode == "Pipe"
                    ? AskExtension(ed, settings.DoubleLineCenterExtensionMm) : 0;
                if (!extension.HasValue) return;

                var total = 0;
                while (true)
                {
                    var firstOpt = new PromptPointOptions(
                        "\n起点 [Width(改变下段宽度)/Exit(完成命令)]，回车退出：") {
                        AllowNone = true
                    };
                    firstOpt.Keywords.Add("Width");
                    firstOpt.Keywords.Add("Exit");
                    var first = ed.GetPoint(firstOpt);
                    if (first.Status == PromptStatus.Keyword)
                    {
                        if (string.Equals(first.StringResult, "Width", StringComparison.OrdinalIgnoreCase))
                        {
                            var changed = AskWidth(ed, width.Value, "下一段双线宽度");
                            if (changed.HasValue) width = changed.Value;
                            continue;
                        }
                        break;
                    }
                    if (first.Status != PromptStatus.OK) break;

                    var verts = new List<Point2d> {
                        new Point2d(first.Value.X, first.Value.Y)
                    };
                    var last = first.Value;
                    var cancelled = false;
                    while (verts.Count < 96)
                    {
                        var pointOpt = new PromptPointOptions(
                            "\n下一顶点 [Undo(撤销上一点)]，回车结束当前段，Esc 取消当前段：") {
                            UseBasePoint = true, BasePoint = last, AllowNone = true
                        };
                        pointOpt.Keywords.Add("Undo");
                        var p = ed.GetPoint(pointOpt);
                        if (p.Status == PromptStatus.Keyword)
                        {
                            if (verts.Count > 1) verts.RemoveAt(verts.Count - 1);
                            last = new Point3d(verts[verts.Count - 1].X,
                                               verts[verts.Count - 1].Y, first.Value.Z);
                            continue;
                        }
                        if (p.Status == PromptStatus.None) break;
                        if (p.Status != PromptStatus.OK) { cancelled = true; break; }
                        if (Math.Abs(p.Value.Z - first.Value.Z) > 1e-7)
                        {
                            ed.WriteMessage("\n仅支持同一 XY 平面的路径，本段已取消。\n");
                            cancelled = true; break;
                        }
                        verts.Add(new Point2d(p.Value.X, p.Value.Y));
                        last = p.Value;
                    }
                    if (cancelled) continue;
                    if (verts.Count < 2)
                    {
                        ed.WriteMessage("\n至少选择两个顶点才绘制一组双线。\n");
                        continue;
                    }
                    var req = new DualLineService.Request {
                        Points = verts, Width = width.Value / multiplier,
                        Mode = mode, Alignment = alignment, Corner = corner,
                        CenterExtension = extension.Value / multiplier,
                        Elevation = first.Value.Z
                    };
                    try
                    {
                        var result = CadTransactionRunner.WriteContext(doc, false,
                            ctx => DualLineService.Draw(ctx, req));
                        total++;
                        ed.WriteMessage("\n[YGDL] 完成 " + total +
                            " 组，宽度 " + width.Value.ToString("0.###") +
                            " mm；图元为原生可编辑对象，后续可用 YGDLW 对称修改。\n");
                        settings.DoubleLineWidthMm = width.Value;
                        settings.DoubleLineMode = mode;
                        settings.DoubleLineAlignment = alignment;
                        settings.DoubleLineCorner = corner;
                        settings.DoubleLineCenterExtensionMm = extension.Value;
                        try { settings.Save(); }
                        catch (System.Exception saveEx)
                        {
                            RuntimeLogger.Error("YGDL settings persistence failed.", saveEx);
                            ed.WriteMessage("\n[YGDL] 图元已创建，但上次宽度设置保存失败：" +
                                            saveEx.Message + "\n");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        RuntimeLogger.Error("YGDL draw segment failed.", ex);
                        ed.WriteMessage("\n[YGDL] 本段未创建：" + ex.Message + "\n");
                    }
                }
                ed.WriteMessage("\n[YGDL] 已结束，创建 " + total + " 组双线。\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGDL command failed.", ex);
                ed.WriteMessage("\n[YGDL] 未完成：" + ex.Message + "\n");
            }
        }

        [CommandMethod("YGDLW", CommandFlags.Modal)]
        public void ChangeDoubleLineWidth()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var opts = new PromptEntityOptions(
                    "\n选择一条 YangCAD 双线边线/中心线，或旧插件的一条 LINE：");
                opts.SetRejectMessage("\n当前仅支持 YangCAD 原生双线组或两条普通 LINE。");
                opts.AddAllowedClass(typeof(Polyline), true);
                opts.AddAllowedClass(typeof(Line), true);
                var first = ed.GetEntity(opts);
                if (first.Status != PromptStatus.OK) return;

                var settings = UserSettings.Load();
                var multiplier = DualLineService.MillimetersPerDrawingUnit(doc.Database);
                var stored = CadTransactionRunner.ReadContext(doc,
                    ctx => DualLineService.StoredWidth(ctx, first.ObjectId));

                ObjectId secondId = ObjectId.Null;
                double previous;
                if (stored.HasValue)
                    previous = stored.Value;
                else
                {
                    var other = new PromptEntityOptions(
                        "\n未找到 YangCAD 成组信息，请选择同一根梁/墙/管的另一条平行 LINE：");
                    other.SetRejectMessage("\n旧图元配对暂只支持两条普通 LINE。");
                    other.AddAllowedClass(typeof(Line), true);
                    var second = ed.GetEntity(other);
                    if (second.Status != PromptStatus.OK) return;
                    secondId = second.ObjectId;
                    previous = CadTransactionRunner.ReadContext(doc,
                        ctx => DualLineService.MeasureLegacyLinePair(
                            ctx, first.ObjectId, secondId));
                }
                ed.WriteMessage("\n[YGDLW] 当前宽度：" +
                    (previous * multiplier).ToString("0.###") +
                    " mm；中轴固定，左右各改变宽差的一半。\n");
                var updated = AskWidth(ed, settings.DoubleLineWidthMm, "调整后的宽度");
                if (!updated.HasValue) return;
                if (Math.Abs(updated.Value - previous * multiplier) < 1e-7)
                {
                    ed.WriteMessage("\n宽度未改变，未修改图纸。\n");
                    return;
                }
                var newWidth = updated.Value / multiplier;
                DualLineService.Result result;
                if (stored.HasValue)
                    result = CadTransactionRunner.WriteContext(doc, false,
                        ctx => DualLineService.ChangeWidth(ctx, first.ObjectId, newWidth));
                else
                    result = CadTransactionRunner.WriteContext(doc, false,
                        ctx => DualLineService.ChangeLegacyLinePair(
                            ctx, first.ObjectId, secondId, newWidth));
                settings.DoubleLineWidthMm = updated.Value;
                try { settings.Save(); }
                catch (System.Exception saveEx)
                {
                    RuntimeLogger.Error("YGDLW settings persistence failed.", saveEx);
                    ed.WriteMessage("\n[YGDLW] 改宽已提交，但上次宽度保存失败：" +
                                    saveEx.Message + "\n");
                }
                ed.WriteMessage("\n[YGDLW] 修改成功：" +
                    (result.old_width * multiplier).ToString("0.###") + " mm → " +
                    updated.Value.ToString("0.###") +
                    " mm；中心线保持不变，两侧对称调整。原生 Undo 尚需实机验证。\n");
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YGDLW failed.", ex);
                ed.WriteMessage("\n[YGDLW] 已安全取消：" + ex.Message + "\n");
            }
        }
    }
}
