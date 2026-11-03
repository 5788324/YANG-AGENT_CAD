using System;
using System.Windows;
using System.Windows.Controls;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ShortcutManagerWindow : Window
    {
        private string _pgpPath = "";
        private bool _loading;
        public ShortcutManagerWindow()
        {
            InitializeComponent();
            ThemeManager.Apply(Resources, ThemeManager.Normalize(UserSettings.Load().Theme));
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) throw new InvalidOperationException("请先打开一张 CAD 图纸。");
                _pgpPath = ShortcutAliasService.FindActivePgpPath(doc.Database);
                _loading = true;
                FeatureBox.ItemsSource = ShortcutAliasService.Features;
                FeatureBox.SelectedIndex = 0;
                _loading = false;
                RefreshFeature();
            }
            catch (Exception ex)
            {
                FeatureBox.IsEnabled = false;
                AliasBox.IsEnabled = false;
                StatusText.Text = "读取 acad.pgp 失败：" + ex.Message;
                RuntimeLogger.Error("Shortcut manager PGP preflight failed.", ex);
            }
        }

        private ShortcutAliasService.Feature? Selected => FeatureBox.SelectedItem as ShortcutAliasService.Feature;
        private void FeatureChanged(object sender, SelectionChangedEventArgs e) { if (!_loading && AliasBox != null) RefreshFeature(); }
        private void RefreshFeature()
        {
            if (Selected == null || string.IsNullOrEmpty(_pgpPath)) return;
            _loading = true;
            try
            {
                var existing = ShortcutAliasService.CurrentAlias(_pgpPath, Selected.Command);
                AliasBox.Text = existing ?? "";
                CurrentText.Text = existing == null ? "当前：未设置" : "当前：" + existing + " → " + Selected.Command;
                OverrideBox.IsChecked = false;
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
            finally { _loading = false; }
            UpdateCollision();
        }
        private void AliasChanged(object sender, TextChangedEventArgs e) { if (!_loading) UpdateCollision(); }
        private void UpdateCollision()
        {
            if (Selected == null || string.IsNullOrEmpty(_pgpPath) || CollisionText == null) return;
            try
            {
                var alias = AliasBox.Text.Trim();
                var info = alias.Length == 0 ? "" : ShortcutAliasService.CollisionMessage(_pgpPath, alias, Selected.Command);
                CollisionText.Text = info;
                OverrideBox.Visibility = info.Contains("CAD/其他插件命令") ? Visibility.Visible : Visibility.Collapsed;
                if (OverrideBox.Visibility != Visibility.Visible) OverrideBox.IsChecked = false;
            }
            catch (Exception ex)
            {
                CollisionText.Text = ex.Message;
                OverrideBox.Visibility = Visibility.Collapsed;
            }
        }
        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (Selected == null || string.IsNullOrEmpty(_pgpPath)) return;
            try
            {
                var name = ShortcutAliasService.Save(_pgpPath, Selected.Command, AliasBox.Text, OverrideBox.IsChecked == true);
                CurrentText.Text = "已保存：" + name + " → " + Selected.Command;
                StatusText.Text = "保存成功（已有原 PGP 备份）。点击“应用到 CAD”即可在当前会话使用。";
                UpdateCollision();
            }
            catch (Exception ex) { StatusText.Text = "保存失败：" + ex.Message; }
        }
        private void RemoveClick(object sender, RoutedEventArgs e)
        {
            if (Selected == null || string.IsNullOrEmpty(_pgpPath)) return;
            try
            {
                var removed = ShortcutAliasService.Remove(_pgpPath, Selected.Command);
                StatusText.Text = removed ? "已移除 YangCAD 管理的快捷键。点击“应用到 CAD”重新加载。" : "这个功能没有 YangCAD 管理的快捷键。";
                RefreshFeature();
            }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }
        private void ReloadClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("接下来会打开 CAD 的“重新初始化”窗口，勾选“PGP 文件”后点击确定。", "应用快捷键", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            // Let native AutoCAD reload the active profile's PGP. Never simulate clicks into the dialog.
            AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_.REINIT ", true, false, false);
        }
        private void OpenCuiClick(object sender, RoutedEventArgs e)
        {
            Close();
            AcApp.DocumentManager.MdiActiveDocument?.SendStringToExecute("_.CUI ", true, false, false);
        }
        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
