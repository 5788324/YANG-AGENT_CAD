using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.UI;

namespace YangCAD.AutoCADHost.Commands
{
    public sealed class ToolboxCommands
    {
        [CommandMethod("YANGCAD_TOOLBOX")]
        public void OpenToolbox()
        {
            try
            {
                RuntimeHost.EnsureInitialized();
                ToolboxPaletteManager.Show();
            }
            catch (System.Exception ex)
            {
                RuntimeLogger.Error("YANGCAD_TOOLBOX failed.", ex);
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[YangCAD] 工具箱启动失败：" + ex.Message + "\n");
            }
        }
    }
}
