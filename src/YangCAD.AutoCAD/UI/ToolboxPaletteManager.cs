using System;
using Autodesk.AutoCAD.Windows;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Toolbox;

namespace YangCAD.AutoCADHost.UI
{
    public static class ToolboxPaletteManager
    {
        private static readonly Guid PaletteId = new Guid("4C556D5F-42F1-4B83-95B8-8A0FC71DD786");
        private static PaletteSet? _palette;
        private static ToolboxView? _view;
        private static ToolboxRegistry? _registry;

        public static void Show()
        {
            if (_palette == null)
            {
                _registry = ToolboxRegistry.CreateDefault();
                _view = new ToolboxView(_registry);
                _palette = new PaletteSet("YangCAD 工具箱", PaletteId);
                _palette.AddVisual("工具箱", _view, true);
                RuntimeLogger.Info("Toolbox PaletteSet created.");
            }
            _palette.Visible = true;
        }

        public static void ApplyTheme(string theme)
        {
            try { _view?.ApplyTheme(theme); } catch (Exception ex) { RuntimeLogger.Error("Toolbox theme update failed.", ex); }
        }

        public static void RefreshContext()
        {
            try { _view?.RefreshContext(); } catch (Exception ex) { RuntimeLogger.Error("Toolbox context refresh failed.", ex); }
        }

        public static void Shutdown()
        {
            try { if (_palette != null) _palette.Visible = false; } catch { }
            _view = null; _registry = null; _palette = null;
        }
    }
}
