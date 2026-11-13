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
            var opt = new PromptKeywordOptions("\n" + prompt + " <" + fallback + ">：") {
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
                "\n" + label + " <" + fallback.ToString("0.###") + " mm>：") {
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
                var scope = Keyword(ed, "筛选范围 [All(当前空间全部)/Selection(用户选择)]",
                    "All", "All", "Selection");
                if (scope.Length == 0) return;
                HashSet<ObjectId>? allowed = null;
                if (scope == "Selection")
                {
                    var options = new PromptSelectionOptions {
                        MessageForAdding = "\n选择要检查的尺寸标注："
                    };
                    var filter = new SelectionFilter(new[] {
                        new TypedValue((int)DxfCode.Start, "DIMENSION")
                    });
                    var selection = ed.GetSelection(options, filter);
                    if (selection.Status != PromptStatus.OK || selection.Value == null)
                        return;
                    allowed = new HashSet<ObjectId>(selection.Value.GetObjectIds());
                }
                var result = CadTransactionRunner.ReadContext(doc,
                    ctx => AnnotationUtilitiesService.FindEditedDimensionText(ctx));
                var ids = allowed == null ? result.Ids
                    : result.Ids.Where(allowed.Contains).ToArray();
                ed.SetImpliedSelection(ids);
                ed.WriteMessage("\n[YGDIMTXT] 已选中 " + ids.Length +
                    " 个修改过显示文字的尺寸标注（当前空间）。\n" +
                    "仅选择，不修改标注的真实测量值或文字。\n");
                if (allowed == null)
                    ed.WriteMessage("包含：纯数字替代、带前后缀的文字覆盖、隐藏文字等；" +
                                    "无覆盖与仅 <> 的标注不选。\n");
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
                    ed.WriteMessage("\n[YangCAD] INSUNITS=0，文字高度和偏移按毫米理解，请检查图纸单位。\n");

                var basis = Keyword(ed,
                    "坡度基准 [Section(剖面 XY 高差)/Plan3D(三维 Z 高差)]",
                    settings.SlopeBasis, "Section", "Plan3D");
                if (basis.Length == 0) return;
                var format = Keyword(ed, "文字格式 [Percent(百分比)/Ratio(1:X)/Degrees(角度)]",
                    settings.SlopeFormat, "Percent", "Ratio", "Degrees");
                if (format.Length == 0) return;
                var height = Numeric(ed, "文字高度", settings.SlopeTextHeightMm, false);
                if (!height.HasValue) return;
                var offset = Numeric(ed, "坡度指示线离源线的距离", settings.SlopeOffsetMm, true);
                if (!offset.HasValue) return;

                var source = Keyword(ed, "确定斜率 [Line(选择直线)/Points(拾取两点)]",
                    "Line", "Line", "Points");
                if (source.Length == 0) return;
                Point3d a, b;
                if (source == "Line")
                {
                    var opts = new PromptEntityOptions("\n选择坡度参考 LINE（不修改原线）：");
                    opts.SetRejectMessage("\n请选择独立的原生 LINE。");
                    opts.AddAllowedClass(typeof(Line), true);
                    var picked = ed.GetEntity(opts);
                    if (picked.Status != PromptStatus.OK) return;
                    var pair = CadTransactionRunner.ReadContext(doc, ctx =>
                    {
                        var line = ctx.Transaction.GetObject(
                            picked.ObjectId, OpenMode.ForRead, false) as Line;
                        if (line == null || line.OwnerId != ctx.Database.CurrentSpaceId)
                            throw new InvalidOperationException("参考线必须在当前空间。");
                        return (A:line.StartPoint,B:line.EndPoint);
                    });
                    a = pair.A; b = pair.B;
                }
                else
                {
                    var first = ed.GetPoint("\n选择坡度起点：");
                    if (first.Status != PromptStatus.OK) return;
                    var opts = new PromptPointOptions("\n选择坡度终点：") {
                        BasePoint = first.Value, UseBasePoint = true
                    };
                    var second = ed.GetPoint(opts);
                    if (second.Status != PromptStatus.OK) return;
                    a = first.Value; b = second.Value;
                }

                var result = CadTransactionRunner.WriteContext(doc, false,
                    ctx => AnnotationUtilitiesService.MarkSlope(ctx,
                        new AnnotationUtilitiesService.SlopeRequest {
                            First = a, Second = b, Basis = basis, Format = format,
                            TextHeight = height.Value / mm, Offset = offset.Value / mm
                        }));
                settings.SlopeBasis = basis;
                settings.SlopeFormat = format;
                settings.SlopeTextHeightMm = height.Value;
                settings.SlopeOffsetMm = offset.Value;
                Persist(settings, ed);
                ed.Regen();
                ed.WriteMessage("\n[YGSLOPE] 已生成坡度符号 " + result.label +
                    "，实际高差 " + result.rise.ToString("0.####") +
                    " / 水平距离 " + result.run.ToString("0.####") +
                    "（图纸单位）。原始线未修改。\n");
            }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("YGSLOPE failed.", e);
                ed.WriteMessage("\n[YGSLOPE] 已取消：" + e.Message + "\n");
            }
        }

        [CommandMethod("YGTBOX", CommandFlags.Modal)]
        public void FrameText()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            try
            {
                var settings = UserSettings.Load();
                var mm = AnnotationUtilitiesService.MillimetersPerUnit(doc.Database);
                var shape = Keyword(ed, "文字框类型 [Rectangle(矩形)/Circle(圆形)]",
                    settings.TextFrameShape, "Rectangle", "Circle");
                if (shape.Length == 0) return;
                var margin = Numeric(ed, "文字外框净偏移距离",
                    settings.TextFrameMarginMm, true);
                if (!margin.HasValue) return;

                var opts = new PromptSelectionOptions {
                    MessageForAdding = "\n选择需要加框的单行或多行文字，回车执行："
                };
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "TEXT,MTEXT")
                });
                var selected = ed.GetSelection(opts, filter);
                if (selected.Status != PromptStatus.OK || selected.Value == null)
                    return;
                var ids = selected.Value.GetObjectIds();
                if (ids.Length > 2000)
                    throw new InvalidOperationException("一次最多给 2000 个文字加框，请分批选择。");

                var frames = CadTransactionRunner.WriteContext(doc, false, ctx =>
                {
                    var output = new List<AnnotationUtilitiesService.FrameResult>();
                    foreach (var id in ids)
                    {
                        output.Add(AnnotationUtilitiesService.FrameText(ctx,
                            new AnnotationUtilitiesService.FrameRequest {
                                TextId = id,
                                Shape = shape,
                                Margin = margin.Value / mm
                            }));
                    }
                    return output;
                });
                settings.TextFrameShape = shape;
                settings.TextFrameMarginMm = margin.Value;
                Persist(settings, ed);
                ed.Regen();
                ed.WriteMessage("\n[YGTBOX] 已创建 " + frames.Count +
                    " 个独立原生" + (shape == "Circle" ? "圆形" : "矩形") +
                    "文字框，偏移 " + margin.Value.ToString("0.###") +
                    " mm；源文字未改变。\n");
            }
            catch (System.Exception e)
            {
                RuntimeLogger.Error("YGTBOX failed.", e);
                ed.WriteMessage("\n[YGTBOX] 未完成加框：" + e.Message + "\n");
            }
        }
    }
}
