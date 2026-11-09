using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class BreaklineCommands
    {
        [CommandMethod("YGBREAKLINE", CommandFlags.Modal)]
        public void ConvertLine()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            ed.WriteMessage("\n[YangCAD] 点击现有 LINE，在点击处插入 Z 折断符号。回车或 Esc 退出。\n");
            while (true)
            {
                var opts = new PromptEntityOptions("\n请选择直线：");
                opts.SetRejectMessage("\n只支持普通 LINE 直线；块、外参或其他图元暂不转换。\n");
                opts.AddAllowedClass(typeof(Line), true);
                var selected = ed.GetEntity(opts);
                if (selected.Status != PromptStatus.OK) break;
                try
                {
                    var result = CadTransactionRunner.WriteContext(doc, false, ctx =>
                        BreaklineService.Convert(ctx, new BreaklineService.Request
                        {
                            LineId = selected.ObjectId,
                            PickPoint = selected.PickedPoint,
                            SymbolWidth = 0
                        }, false));
                    ed.WriteMessage("\n[YangCAD] 已转为原生折断多段线（" + result.entity_type +
                        "），可用一次 U 撤销。继续点击下一条直线。\n");
                }
                catch (System.Exception ex)
                {
                    RuntimeLogger.Error("YGBREAKLINE failed.", ex);
                    ed.WriteMessage("\n[YangCAD] 转换失败，该直线未更改：" + ex.Message + "\n");
                }
            }
        }
    }
}
