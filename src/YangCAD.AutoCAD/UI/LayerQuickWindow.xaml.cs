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
        private sealed class LayerRow
        {
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string Color { get; set; } = "";
            public string StatusBadge { get; set; } = "";
        }

        private LayerQuickService.LayerInfo[] _layers;
        private readonly Func<string, string[], string, bool, LayerQuickService.LayerInfo[]> _apply;
        private bool _ready;
        private bool _syncing;
        public bool PickRequested { get; private set; }
        public string SourceName => SourceLayer.Text.Trim();
        public string TargetName => TargetLayer.Text.Trim();

        public LayerQuickWindow(LayerQuickService.LayerInfo[] layers, string selected,
            Func<string, string[], string, bool, LayerQuickService.LayerInfo[]> apply)
        {
            InitializeComponent();
            ThemeManager.Apply(Resources, ThemeManager.Normalize(UserSettings.Load().Theme));
            _layers = layers;
            _apply = apply;
            var names = layers.Select(x => x.name).ToArray();
            SourceLayer.ItemsSource = names;
            TargetLayer.ItemsSource = names;
            SourceLayer.Text = string.IsNullOrWhiteSpace(selected)
                ? layers.FirstOrDefault(x => x.current)?.name ?? names.FirstOrDefault() ?? "0" : selected;
            TargetLayer.Text = layers.FirstOrDefault(x => !string.Equals(x.name, SourceLayer.Text, StringComparison.OrdinalIgnoreCase) && !x.xref_dependent)?.name ?? "0";
            _ready = true;
            RefreshLayerList();
            Refresh();
        }

        private void RefreshLayerList(string[]? keep = null)
        {
            if (LayerListBox == null) return;
            keep ??= SelectedLayers().Select(layer => layer.name).ToArray();
            var keyword = (FilterBox?.Text ?? "").Trim();
            var rows = _layers
                .Where(x => string.IsNullOrEmpty(keyword) || x.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(x => new LayerRow
                {
                    Name = x.name,
                    Color = x.color_rgb,
                    DisplayName = (x.current ? "★ " : "") + x.name + (x.xref_dependent ? " [外参]" : ""),
                    StatusBadge = $"{(x.off ? "关" : "开")} · {(x.frozen ? "冻结" : "正常")} · {(x.locked ? "锁定" : "未锁")}"
                })
                .ToArray();
            _syncing = true;
            LayerListBox.ItemsSource = rows;
            foreach (var row in rows.Where(row => keep.Contains(row.Name, StringComparer.OrdinalIgnoreCase)))
                LayerListBox.SelectedItems.Add(row);
            _syncing = false;
        }

        private LayerQuickService.LayerInfo[] SelectedLayers()
        {
            var names = LayerListBox.SelectedItems.OfType<LayerRow>().Select(row => row.Name).ToArray();
            if (names.Length == 0 && string.IsNullOrWhiteSpace(FilterBox.Text)) names = new[] { SourceName };
            return _layers.Where(layer => names.Contains(layer.name, StringComparer.OrdinalIgnoreCase)).ToArray();
        }

        private void Refresh()
        {
            if (!_ready) return;
            var selected = SelectedLayers();
            LayerStatus.Text = selected.Length == 0 ? "请选择已有图层。" :
                selected.Length > 1 ? $"已选 {selected.Length} 个图层。" :
                $"已选：{selected[0].name}" + (selected[0].current ? " · 当前层" : "");
            VisibilityButton.Content = selected.All(item => item.off) ? "显示图层" : "关闭图层";
            FreezeButton.Content = selected.All(item => item.frozen) ? "解冻图层" : "冻结图层";
            LockButton.Content = selected.All(item => item.locked) ? "解锁图层" : "锁定图层";
        }

        private void FilterChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshLayerList();
            Refresh();
        }

        private void LayerListSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _syncing) return;
            if (LayerListBox.SelectedItem is LayerRow row)
            {
                _syncing = true;
                SourceLayer.Text = row.Name;
                _syncing = false;
            }
            Refresh();
        }

        private void LayerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _syncing) return;
            if (LayerListBox.ItemsSource is LayerRow[] rows)
            {
                _syncing = true;
                LayerListBox.SelectedItems.Clear();
                LayerListBox.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Name, SourceName, StringComparison.OrdinalIgnoreCase));
                _syncing = false;
            }
            Refresh();
        }

        private void ExecuteBatch(string operation, Func<LayerQuickService.LayerInfo, bool> state)
        {
            var selected = SelectedLayers();
            var value = !selected.All(state);
            var applicable = selected.Where(layer => state(layer) != value &&
                (!layer.current || !value || operation == "lock")).ToArray();
            Apply(operation, applicable, value, selected.Length - applicable.Length);
        }

        private void Apply(string operation, LayerQuickService.LayerInfo[] selected, bool value, int skipped = 0)
        {
            if (selected.Length == 0)
            {
                LayerStatus.Text = skipped == 0 ? "请选择已有图层。" : "当前图层不能关闭或冻结。";
                return;
            }
            try
            {
                var keep = SelectedLayers().Select(layer => layer.name).ToArray();
                var source = SourceName;
                var target = TargetName;
                _layers = _apply(operation, selected.Select(layer => layer.name).ToArray(), target, value);
                var names = _layers.Select(layer => layer.name).ToArray();
                _syncing = true;
                SourceLayer.ItemsSource = names;
                TargetLayer.ItemsSource = names;
                SourceLayer.Text = names.Contains(source, StringComparer.OrdinalIgnoreCase) ? source : target;
                TargetLayer.Text = target;
                _syncing = false;
                RefreshLayerList(keep);
                Refresh();
                LayerStatus.Text = $"已处理 {selected.Length} 个图层" +
                    (skipped == 0 ? "。" : $"，跳过 {skipped} 个当前层或已是所需状态的图层。");
            }
            catch (Exception ex)
            {
                LayerStatus.Text = ex.Message;
                RuntimeLogger.Error("图层处理失败。", ex);
            }
        }

        private void VisibilityClick(object sender, RoutedEventArgs e) => ExecuteBatch("off", item => item.off);
        private void FreezeClick(object sender, RoutedEventArgs e) => ExecuteBatch("freeze", item => item.frozen);
        private void LockClick(object sender, RoutedEventArgs e) => ExecuteBatch("lock", item => item.locked);

        private void MergeClick(object sender, RoutedEventArgs e)
        {
            var selected = SelectedLayers();
            if (selected.Length != 1)
            {
                LayerStatus.Text = "合并时请选择一个来源图层。";
                return;
            }
            if (!_layers.Any(item => string.Equals(item.name, TargetName, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(selected[0].name, TargetName, StringComparison.OrdinalIgnoreCase))
            {
                LayerStatus.Text = "请选择与来源不同的已有目标图层。";
                return;
            }
            Apply("merge", selected, false);
        }

        private void PickClick(object sender, RoutedEventArgs e)
        {
            PickRequested = true;
            Close();
        }

    }
}
