using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ScaleConvertWindow : Window
    {
        private static readonly string[] Presets =
        {
            "1:1", "1:2", "1:5", "1:10", "1:20", "1:25", "1:50", "1:100", "1:200", "1:250", "1:500"
        };

        private bool _loaded;

        public ScaleConvertWindow(int selectedCount)
        {
            InitializeComponent();
            var settings = UserSettings.Load();
            ThemeManager.Apply(Resources, ThemeManager.Normalize(settings.Theme));
            OldScaleCombo.ItemsSource = Presets;
            NewScaleCombo.ItemsSource = Presets;
            OldScaleCombo.Text = "1:5";
            NewScaleCombo.Text = "1:10";
            SelectionSummaryText.Text = $"已选择 {selectedCount} 个对象";
            _loaded = true;
            RefreshModeAndFactor();
        }

        public ScaleConversionService.DrawingMode SelectedMode =>
            ScaledModeRadio.IsChecked == true
                ? ScaleConversionService.DrawingMode.ScaledDrawing
                : ScaleConversionService.DrawingMode.Standard1To1;

        public double OldScale { get; private set; } = 5.0;
        public double NewScale { get; private set; } = 10.0;
        public bool PickBasePoint => PickBasePointRadio.IsChecked == true;
        public bool PreserveTextPaperSize => TextCheck.IsChecked == true;
        public bool PreserveDimensionPaperSize => DimensionCheck.IsChecked == true;
        public bool PreserveLeaderPaperSize => LeaderCheck.IsChecked == true;
        public bool PreserveHatchPaperScale => HatchCheck.IsChecked == true;
        public bool PreserveLinetypePaperScale => LinetypeCheck.IsChecked == true;

        private void ModeChanged(object sender, RoutedEventArgs e)
        {
            if (_loaded) RefreshModeAndFactor();
        }

        private void ScaleInputChanged(object sender, RoutedEventArgs e)
        {
            if (_loaded) RefreshModeAndFactor(showValidation: false);
        }

        private void ScaleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loaded) RefreshModeAndFactor(showValidation: false);
        }

        private void StartClick(object sender, RoutedEventArgs e)
        {
            if (!TryReadScale(OldScaleCombo.Text, out var oldScale) || !TryReadScale(NewScaleCombo.Text, out var newScale))
            {
                MessageBox.Show(this, "比例格式无效。请输入例如 1:5、1:10，或直接输入分母 5、10。", "YangCAD 比例转换", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            OldScale = oldScale;
            NewScale = newScale;

            if (Math.Abs(OldScale - NewScale) < 1e-12)
            {
                MessageBox.Show(this, "当前比例与目标比例相同，不需要转换。", "YangCAD 比例转换", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SelectedMode == ScaleConversionService.DrawingMode.ScaledDrawing)
            {
                var factor = OldScale / NewScale;
                var answer = MessageBox.Show(
                    this,
                    $"缩尺绘图模式会实际修改几何尺寸。\n\n1:{OldScale:0.####} → 1:{NewScale:0.####}\n几何将缩放为原来的 {factor:P2}。\n\n继续吗？",
                    "确认缩尺转换",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (answer != MessageBoxResult.OK) return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RefreshModeAndFactor(bool showValidation = false)
        {
            BasePointPanel.IsEnabled = SelectedMode == ScaleConversionService.DrawingMode.ScaledDrawing;
            if (!TryReadScale(OldScaleCombo.Text, out var oldScale) || !TryReadScale(NewScaleCombo.Text, out var newScale))
            {
                FactorText.Text = "请输入有效比例";
                ModeHintText.Text = "支持 1:5、1:10，也可直接输入 5、10。";
                return;
            }

            if (SelectedMode == ScaleConversionService.DrawingMode.Standard1To1)
            {
                var annotation = newScale / oldScale;
                FactorText.Text = $"几何倍率 1.0000 · 注释倍率 {annotation:0.0000}";
                ModeHintText.Text = "标准模式不改变真实几何；文字、标注、引线、Hatch、线型按目标比例调整模型空间显示尺度。";
            }
            else
            {
                var geometry = oldScale / newScale;
                FactorText.Text = $"几何倍率 {geometry:0.0000} · 注释倍率 1.0000";
                ModeHintText.Text = "缩尺绘图模式会按基点实际缩放几何，同时尽量恢复文字、标注、引线、Hatch、线型的纸面视觉大小。";
            }
        }

        private static bool TryReadScale(string? text, out double denominator)
        {
            denominator = 0;
            var value = (text ?? "").Trim().Replace('：', ':');
            if (string.IsNullOrWhiteSpace(value)) return false;

            string token = value;
            if (value.Contains(":"))
            {
                var parts = value.Split(':');
                if (parts.Length != 2 || !TryDouble(parts[0], out var numerator) || Math.Abs(numerator - 1.0) > 1e-9)
                    return false;
                token = parts[1];
            }
            else if (value.Contains("/"))
            {
                var parts = value.Split('/');
                if (parts.Length != 2 || !TryDouble(parts[0], out var numerator) || Math.Abs(numerator - 1.0) > 1e-9)
                    return false;
                token = parts[1];
            }

            return TryDouble(token, out denominator) && denominator > 0 && !double.IsNaN(denominator) && !double.IsInfinity(denominator);
        }

        private static bool TryDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
