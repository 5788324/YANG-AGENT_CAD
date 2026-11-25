using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class AnnotationUtilitiesCommands
    {
        private static string Keyword(Editor ed, string prompt, string fallback, params string[] values)
        {
            var opt = new PromptKeywordOptions("\n" + prompt + " <" + fallback + ">：")
            {
                AllowNone = true
            };
            foreach (var v in values) opt.Keywords.Add(v);
            opt.Keywords.Default = fallback;
            var result = ed.GetKeywords(opt);
            if (result.Status == PromptStatus.None) return fallback;
            return result.Status == PromptStatus.OK ? result.StringResult : "";
        }

        private static double? Numeric(Editor ed, string label, double fallback, bool allowZero)
        {
            var opt = new PromptDoubleOptions(
                "\n" + label + " <" + fallback.ToString("0.###") + " mm>：")
            {
                AllowNone = true,
                UseDefaultValue = true,
                DefaultValue = fallback,
                AllowNegative = false,
                AllowZero = allowZero
            };
            var result = ed.GetDouble(opt);
            if (result.Status == PromptStatus.None) return fallback;
            return result.Status == PromptStatus.OK ? result.Value : (double?)null;
        }

        private static void Persist(UserSettings settings, Editor ed)
        {
            try { settings.Save(); }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("Annotation settings persistence failed.", e);
                ed.WriteMessage("\n[YangCAD] 图纸已处理，但个人参数保存失败：" + e.Message + "\n");
            }
        }

        [CommandMethod("YGDIMTXT", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void SelectOverriddenDimensions()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                HashSet<ObjectId>? allowed = null;
                var implied = ed.SelectImplied();
                var scope = "All";
                if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    allowed = new HashSet<ObjectId>(implied.Value.GetObjectIds());
                    scope = "Selection";
                }
                else
                {
                    scope = Keyword(ed, "筛选范围 [All(当前空间全部)/Selection(框选范围)]",
                        "All", "All", "Selection");
                    if (scope.Length == 0) return;
                    if (scope == "Selection")
                    {
                        var options = new PromptSelectionOptions
                        {
                            MessageForAdding = "\n框选要检查的尺寸标注："
                        };
                        var filter = new SelectionFilter(new[] {
                            new TypedValue((int)DxfCode.Start, "DIMENSION")
                        });
                        var selection = ed.GetSelection(options, filter);
                        if (selection.Status != PromptStatus.OK || selection.Value == null)
                            return;
                        allowed = new HashSet<ObjectId>(selection.Value.GetObjectIds());
                    }
                }

                var result = CadTransactionRunner.ReadContext(doc,
                    ctx => AnnotationUtilitiesService.FindEditedDimensionText(ctx));
                var ids = allowed == null ? result.Ids
                    : result.Ids.Where(allowed.Contains).ToArray();
                ed.SetImpliedSelection(ids);
                ed.WriteMessage("\n[YGDIMTXT] 已高亮选中 " + ids.Length + " 个改过显示文字的尺寸标注。\n");
            }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("YGDIMTXT failed.", e);
                ed.WriteMessage("\n[YGDIMTXT] 筛选失败：" + e.Message + "\n");
            }
        }

        [CommandMethod("YGSLOPE", CommandFlags.Modal)]
        public void AnnotateSlope()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var settings = UserSettings.Load();
                var mm = AnnotationUtilitiesService.MillimetersPerUnit(doc.Database);
                if (Convert.ToInt32(doc.Database.Insunits) == 0)
                    ed.WriteMessage("\n[YangCAD] INSUNITS=0，文字高度和偏移按毫米计算。\n");

                while (true)
                {
                    var basisLabel = settings.SlopeBasis == "Plan3D" ? "三维Z高差" : "剖面XY";
                    var fmtLabel = settings.SlopeFormat == "Degrees" ? "角度" :
                                   settings.SlopeFormat == "Ratio" ? "1:X" : "百分比";
                    ed.WriteMessage(
                        $"\n[YGSLOPE] 当前默认：{basisLabel} | {fmtLabel} | 字高 {settings.SlopeTextHeightMm:0.##}mm | 偏移 {settings.SlopeOffsetMm:0.##}mm\n");

                    var opts = new PromptEntityOptions("\n请直接点选斜坡参考直线 [两点标坡(P)/参数设置(S)]：");
                    opts.SetRejectMessage("\n请选择独立的原生直线（LINE），或输入 P 拾取两点。");
                    opts.AddAllowedClass(typeof(Line), true);
                    opts.Keywords.Add("Points", "P", "两点标坡(P)");
                    opts.Keywords.Add("Settings", "S", "参数设置(S)");

                    var picked = ed.GetEntity(opts);
                    if (picked.Status == PromptStatus.Keyword)
                    {
                        if (string.Equals(picked.StringResult, "Settings", StringComparison.OrdinalIgnoreCase))
                        {
                            var basis = Keyword(ed, "坡度基准 [Section(剖面 XY)/Plan3D(三维 Z)]",
                                settings.SlopeBasis, "Section", "Plan3D");
                            if (basis.Length == 0) return;
                            var format = Keyword(ed, "文字格式 [Percent(百分比)/Ratio(1:X)/Degrees(角度)]",
                                settings.SlopeFormat, "Percent", "Ratio", "Degrees");
                            if (format.Length == 0) return;
                            var height = Numeric(ed, "文字高度", settings.SlopeTextHeightMm, false);
                            if (!height.HasValue) return;
                            var offset = Numeric(ed, "坡度指示线偏移", settings.SlopeOffsetMm, true);
                            if (!offset.HasValue) return;
                            settings.SlopeBasis = basis;
                            settings.SlopeFormat = format;
                            settings.SlopeTextHeightMm = height.Value;
                            settings.SlopeOffsetMm = offset.Value;
                            Persist(settings, ed);
                            continue;
                        }

                        var first = ed.GetPoint("\n拾取坡度起点：");
                        if (first.Status != PromptStatus.OK) return;
                        var p2Opts = new PromptPointOptions("\n拾取坡度终点：")
                        {
                            BasePoint = first.Value,
                            UseBasePoint = true
                        };
                        var second = ed.GetPoint(p2Opts);
                        if (second.Status != PromptStatus.OK) return;

                        WriteSlopeMark(doc, ed, settings, mm, first.Value, second.Value);
                        return;
                    }

                    if (picked.Status != PromptStatus.OK) return;

                    var pair = CadTransactionRunner.ReadContext(doc, ctx =>
                    {
                        var line = ctx.Transaction.GetObject(
                            picked.ObjectId, OpenMode.ForRead, false) as Line;
                        if (line == null || line.OwnerId != ctx.Database.CurrentSpaceId)
                            throw new InvalidOperationException("参考线必须在当前空间。");
                        return (A: line.StartPoint, B: line.EndPoint);
                    });

                    WriteSlopeMark(doc, ed, settings, mm, pair.A, pair.B);
                    return;
                }
            }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("YGSLOPE failed.", e);
                ed.WriteMessage("\n[YGSLOPE] 已取消：" + e.Message + "\n");
            }
        }

        private static void WriteSlopeMark(
            Document doc,
            Editor ed,
            UserSettings settings,
            double mm,
            Point3d a,
            Point3d b)
        {
            var result = CadTransactionRunner.WriteContext(doc, false,
                ctx => AnnotationUtilitiesService.MarkSlope(ctx,
                    new AnnotationUtilitiesService.SlopeRequest
                    {
                        First = a,
                        Second = b,
                        Basis = settings.SlopeBasis,
                        Format = settings.SlopeFormat,
                        TextHeight = settings.SlopeTextHeightMm / mm,
                        Offset = settings.SlopeOffsetMm / mm
                    }));
            Persist(settings, ed);
            ed.Regen();
            ed.WriteMessage($"\n[YGSLOPE] 已生成坡度标注 {result.label}。\n");
        }

        [CommandMethod("YGTBOX", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void FrameText()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var settings = UserSettings.Load();
                var mm = AnnotationUtilitiesService.MillimetersPerUnit(doc.Database);
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "TEXT,MTEXT")
                });

                ObjectId[] ids;
                var implied = ed.SelectImplied();
                if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
                {
                    ids = CadTransactionRunner.Read(doc, tr =>
                        implied.Value.GetObjectIds()
                            .Where(id =>
                            {
                                var ent = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                                return ent is DBText || ent is MText;
                            })
                            .ToArray());
                    if (ids.Length == 0)
                    {
                        ed.WriteMessage("\n[YGTBOX] 预选对象中没有单行或多行文字。\n");
                        return;
                    }
                }
                else
                {
                    while (true)
                    {
                        var shapeLabel = settings.TextFrameShape == "Circle" ? "圆形" : settings.TextFrameShape == "Slot" ? "跑道形" : "矩形";
                        ed.WriteMessage(
                            $"\n[YGTBOX] 当前默认：{shapeLabel}框 | 留白偏移 {settings.TextFrameMarginMm:0.##}mm | 遮罩 {(settings.TextFrameMask ? "开" : "关")}\n");

                        var pendingKey = "";
                        var opts = new PromptSelectionOptions
                        {
                            MessageForAdding = "\n请框选需要加框的文字 [矩形(R)/圆形(C)/跑道(S)/遮罩(M)/改留白偏移(O)]："
                        };
                        opts.Keywords.Add("R", "R", "矩形(R)");
                        opts.Keywords.Add("C", "C", "圆形(C)");
                        opts.Keywords.Add("S", "S", "跑道(S)");
                        opts.Keywords.Add("M", "M", "遮罩(M)");
                        opts.Keywords.Add("O", "O", "偏移(O)");
                        opts.KeywordInput += (_, args) =>
                        {
                            pendingKey = args.Input ?? "";
                            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.OK);
                        };

                        PromptSelectionResult selected;
                        try
                        {
                            selected = ed.GetSelection(opts, filter);
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) when (!string.IsNullOrEmpty(pendingKey))
                        {
                            if (string.Equals(pendingKey, "R", StringComparison.OrdinalIgnoreCase))
                            {
                                settings.TextFrameShape = "Rectangle";
                                Persist(settings, ed);
                                continue;
                            }
                            if (string.Equals(pendingKey, "C", StringComparison.OrdinalIgnoreCase))
                            {
                                settings.TextFrameShape = "Circle";
                                Persist(settings, ed);
                                continue;
                            }
                            if (string.Equals(pendingKey, "S", StringComparison.OrdinalIgnoreCase))
                            {
                                settings.TextFrameShape = "Slot";
                                Persist(settings, ed);
                                continue;
                            }
                            if (string.Equals(pendingKey, "M", StringComparison.OrdinalIgnoreCase))
                            {
                                settings.TextFrameMask = !settings.TextFrameMask;
                                Persist(settings, ed);
                                continue;
                            }
                            if (string.Equals(pendingKey, "O", StringComparison.OrdinalIgnoreCase))
                            {
                                var margin = Numeric(ed, "文字外框留白偏移", settings.TextFrameMarginMm, true);
                                if (!margin.HasValue) return;
                                settings.TextFrameMarginMm = margin.Value;
                                Persist(settings, ed);
                                continue;
                            }
                            continue;
                        }

                        if (selected.Status != PromptStatus.OK || selected.Value == null)
                            return;
                        ids = selected.Value.GetObjectIds();
                        break;
                    }
                }

                if (ids.Length > 2000)
                    throw new InvalidOperationException("一次最多给 2000 个文字加框，请分批选择。");

                if (settings.TextFrameMask || settings.TextFrameShape == "Slot")
                {
                    ed.SetImpliedSelection(Array.Empty<ObjectId>());
                    JianrenTextService.RunTextBoxAdvanced(doc, settings.TextFrameShape == "Slot",
                        settings.TextFrameMask, ids, circle: settings.TextFrameShape == "Circle");
                    return;
                }

                var skipped = 0;
                var frames = CadTransactionRunner.WriteContext(doc, false, ctx =>
                {
                    var output = new List<AnnotationUtilitiesService.FrameResult>();
                    foreach (var id in ids)
                    {
                        try
                        {
                            output.Add(AnnotationUtilitiesService.FrameText(ctx,
                                new AnnotationUtilitiesService.FrameRequest
                                {
                                    TextId = id,
                                    Shape = settings.TextFrameShape,
                                    Margin = settings.TextFrameMarginMm / mm
                                }));
                        }
                        catch (System.Exception itemEx)
                        {
                            skipped++;
                            RuntimeLogger.Info("YGTBOX skipped one text entity: " + itemEx.Message);
                        }
                    }
                    return output;
                });

                ed.SetImpliedSelection(Array.Empty<ObjectId>());
                Persist(settings, ed);
                ed.Regen();
                ed.WriteMessage($"\n[YGTBOX] 已为 {frames.Count} 个文字生成" +
                    (settings.TextFrameShape == "Circle" ? "圆形" : "矩形") +
                    $"边框（留白 {settings.TextFrameMarginMm:0.##}mm）" +
                    (skipped > 0 ? $"，跳过 {skipped} 个异常或空白文字。" : "。") + "\n");
            }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("YGTBOX failed.", e);
                ed.WriteMessage("\n[YGTBOX] 未完成加框：" + e.Message + "\n");
            }
        }
    }
}
