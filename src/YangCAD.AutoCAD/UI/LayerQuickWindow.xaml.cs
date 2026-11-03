using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.UI
{
    public partial class LayerQuickWindow : Window
    {
        private readonly LayerQuickService.LayerInfo[] _layers;
        private bool _ready;
        public string Operation { get; private set; } = "";
        public string SourceName => SourceLayer.Text.Trim();
        public string TargetName => TargetLayer.Text.Trim();
        public bool Value { get; private set; }

        public LayerQuickWindow(LayerQuickService.LayerInfo[] layers, string selected = "")
        {
            InitializeComponent();
            ThemeManager.Apply(Resources, ThemeManager.Normalize(UserSettings.Load().Theme));
            _layers = layers;
            var names = layers.Select(x => x.name).ToArray();
            SourceLayer.ItemsSource = names;
            TargetLayer.ItemsSource = names;
            SourceLayer.Text = string.IsNullOrWhiteSpace(selected)
                ? layers.FirstOrDefault(x => x.current)?.name ?? names.FirstOrDefault() ?? "0" : selected;
            TargetLayer.Text = names.FirstOrDefault(x => !string.Equals(x, SourceLayer.Text, StringComparison.OrdinalIgnoreCase) && !x.xref_dependent) ?? "0";
            _ready = true;
            Refresh();
        }

        private void Refresh()
        {
            if (!_ready) return;
            var item = _layers.FirstOrDefault(x => string.Equals(x.name, SourceName, StringComparison.OrdinalIgnoreCase));
            LayerStatus.Text = item == null ? "图层不存在。可从下拉列表选择。" :
                $"{(item.off ? "已关闭" : "已显示")} · {(item.frozen ? "已冻结" : "已解冻")} · {(item.locked ? "已锁定" : "未锁定")}" +
                (item.xref_dependent ? " · 外参图层（当前文件内设置）" : "") +
                (item.current ? " · 当前图层" : "");
            VisibilityButton.Content = item?.off == true ? "显示图层" : "关闭图层";
            FreezeButton.Content = item?.frozen == true ? "解冻图层" : "冻结图层";
            LockButton.Content = item?.locked == true ? "解锁图层" : "锁定图层";
        }

        private void LayerChanged(object sender, SelectionChangedEventArgs e) => Refresh();
        private bool EnsureLayer() => _layers.Any(x => string.Equals(x.name, SourceName, StringComparison.OrdinalIgnoreCase))
            ? true : ShowError("请从列表中选择已有图层。");
        private bool ShowError(string message)
        {
            MessageBox.Show(this, message, "YangCAD 图层工具", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        private void Commit(string operation, bool value = false)
        {
            if (!EnsureLayer()) return;
            Operation = operation;
            Value = value;
            DialogResult = true;
            Close();
        }
        private void VisibilityClick(object sender, RoutedEventArgs e)
        {
            var item = _layers.FirstOrDefault(x => string.Equals(x.name, SourceName, StringComparison.OrdinalIgnoreCase));
            Commit("off", item?.off != true);
        }
        private void FreezeClick(object sender, RoutedEventArgs e)
        {
            var item = _layers.FirstOrDefault(x => string.Equals(x.name, SourceName, StringComparison.OrdinalIgnoreCase));
            Commit("freeze", item?.frozen != true);
        }
        private void LockClick(object sender, RoutedEventArgs e)
        {
            var item = _layers.FirstOrDefault(x => string.Equals(x.name, SourceName, StringComparison.OrdinalIgnoreCase));
            Commit("lock", item?.locked != true);
        }
        private void MergeClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureLayer()) return;
            if (!_layers.Any(x => string.Equals(x.name, TargetName, StringComparison.OrdinalIgnoreCase)))
            { ShowError("请选择已有的目标图层。"); return; }
            if (string.Equals(SourceName, TargetName, StringComparison.OrdinalIgnoreCase))
            { ShowError("来源图层和目标图层不能相同。"); return; }
            Commit("merge");
        }
        private void PickClick(object sender, RoutedEventArgs e)
        {
            Operation = "pick";
            DialogResult = true;
            Close();
        }
    }
}
