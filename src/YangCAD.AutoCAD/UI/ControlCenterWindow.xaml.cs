using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.UI
{
    public partial class ControlCenterWindow : Window
    {
        private UserSettings _settings;
        private bool _loading;

        public ControlCenterWindow()
        {
            InitializeComponent();
            _settings = UserSettings.Load();
            _loading = true;
            _settings.Theme = ThemeManager.Normalize(_settings.Theme);
            ThemeManager.Apply(Resources, _settings.Theme);
            foreach (ListBoxItem item in ThemeList.Items)
                if (string.Equals(item.Tag as string, _settings.Theme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeList.SelectedItem = item;
                    break;
                }
            ProductVersionText.Text = "v" + ProductVersion();
            _loading = false;
            RefreshAbout();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshAbout();
        }

        private void RefreshAbout()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var current = Process.GetCurrentProcess();
            AboutEnvironmentText.Text =
                $"宿主：{CadHostInfo.Product} {CadHostInfo.Version}\n" +
                $"进程 PID：{current.Id}\n" +
                $"插件 DLL：{assembly.Location}\n" +
                $"用户设置：{UserSettings.SettingsPath}";
        }

        private void NavigationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsPage == null || ShortcutsPage == null || AboutPage == null) return;
            var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string ?? "Settings";
            SettingsPage.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            ShortcutsPage.Visibility = tag == "Shortcuts" ? Visibility.Visible : Visibility.Collapsed;
            AboutPage.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !(ThemeList.SelectedItem is ListBoxItem item)) return;
            _settings.Theme = ThemeManager.Normalize(item.Tag as string);
            _settings.Save();
            ThemeManager.Apply(Resources, _settings.Theme);
            ToolboxPaletteManager.ApplyTheme(_settings.Theme);
            McpWindowManager.ApplyTheme(_settings.Theme);
        }

        private void CopyAboutClick(object sender, RoutedEventArgs e)
        {
            RefreshAbout();
            Clipboard.SetText(ProductVersionText.Text + "\n" + AboutEnvironmentText.Text);
        }

        private void OpenLogsClick(object sender, RoutedEventArgs e)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YangCADAgent", "logs");
            Directory.CreateDirectory(folder);
            try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "YangCAD", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private static string ProductVersion()
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational)) return informational!;
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.10.0-dev";
        }

        private void OpenProjectClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/5788324/YANG-AGENT_CAD",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "YangCAD", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenShortcutsClick(object sender, RoutedEventArgs e)
        {
            Close();
            Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument?
                .SendStringToExecute("YGKEYS ", true, false, false);
        }

        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
