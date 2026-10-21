using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp;

namespace YangCAD.AutoCADHost.UI
{
    public partial class McpWindow : Window
    {
        private UserSettings _settings;
        private bool _loading;

        public McpWindow()
        {
            InitializeComponent();
            _settings = UserSettings.Load();
            _loading = true;
            _settings.Theme = ThemeManager.Normalize(_settings.Theme);
            ThemeManager.Apply(Resources, _settings.Theme);
            AllowWritesCheck.IsChecked = _settings.AllowWrites;
            AllowEraseCheck.IsChecked = _settings.AllowErase;
            AllowCommandCheck.IsChecked = _settings.AllowCommandExecution;
            AllowExtensionCheck.IsChecked = _settings.AllowExtensionInvoke;
            UpdateAdvancedControlState(_settings.AllowWrites);
            _loading = false;
            RefreshStatus();
        }

        public void ApplyTheme(string theme) => ThemeManager.Apply(Resources, theme);

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            RuntimeHost.EnsureInitialized();
            var snapshot = InstanceRegistry.Snapshot();
            var tools = RuntimeHost.Tools.Describe();
            var gateway = RuntimeHost.Gateway;
            var running = gateway != null && gateway.IsRunning;
            var isCivil = IsCivilHost();
            var expected = isCivil ? 61 : 31;

            HostText.Text = CadHostInfo.Product;
            ProcessText.Text = snapshot.ProcessId > 0 ? $"PID {snapshot.ProcessId} · {CadHostInfo.Version}" : CadHostInfo.Version;
            DocumentText.Text = snapshot.Document ?? "-";
            DocumentIdText.Text = Short(snapshot.DocumentId, 12);
            ToolCountText.Text = $"{tools.Length} live tools";
            ExpectedToolCountText.Text = isCivil ? "Civil 3D 期望 61 Tools" : "AutoCAD 期望 31 Tools";
            ToolSummaryText.Text = $"{tools.Length} Tools";
            GatewayEndpointText.Text = running ? $"http://127.0.0.1:{gateway.Port}/rpc" : "未就绪";
            InstancePathText.Text = InstanceRegistry.CurrentPath;
            SessionText.Text = snapshot.ProcessId > 0 ? $"PID {snapshot.ProcessId} · Document {Short(snapshot.DocumentId, 12)}" : "尚未生成实例会话";
            StatusText.Text = running ? "CAD 网关就绪" : "CAD 网关异常";
            GatewayStateText.Text = running ? "CAD 网关正在监听" : "CAD 网关未运行";
            var brush = TryFindResource(running ? "SuccessBrush" : "DangerBrush") as Brush ?? (running ? Brushes.SeaGreen : Brushes.IndianRed);
            StatusDot.Fill = brush;
            GatewayDot.Fill = brush;
        }

        private void SettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var allow = AllowWritesCheck.IsChecked == true;
            if (!allow)
            {
                AllowEraseCheck.IsChecked = false;
                AllowCommandCheck.IsChecked = false;
                AllowExtensionCheck.IsChecked = false;
            }
            UpdateAdvancedControlState(allow);
            _settings.AllowWrites = allow;
            _settings.AllowErase = allow && AllowEraseCheck.IsChecked == true;
            _settings.AllowCommandExecution = allow && AllowCommandCheck.IsChecked == true;
            _settings.AllowExtensionInvoke = allow && AllowExtensionCheck.IsChecked == true;
            _settings.Save();
        }

        private void UpdateAdvancedControlState(bool enabled)
        {
            AllowEraseCheck.IsEnabled = enabled;
            AllowCommandCheck.IsEnabled = enabled;
            AllowExtensionCheck.IsEnabled = enabled;
        }

        private void CopyMcpConfigClick(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            try
            {
                Clipboard.SetText(McpClientConfigBuilder.BuildCopyText());
                MessageBox.Show(
                    "已复制当前 CAD 会话的 MCP 配置。\n\n把剪贴板内容直接发给本机 AI，让它添加 yangcad MCP Server 并调用 cad.status 验证即可。\n\nCAD/Civil 3D 重启后请重新复制。",
                    "YangCAD MCP",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "复制 MCP 配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyDiagnosticsClick(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            var s = InstanceRegistry.Snapshot();
            var gateway = RuntimeHost.Gateway;
            Clipboard.SetText(new StringBuilder()
                .AppendLine("YangCAD MCP")
                .AppendLine("GatewayRunning: " + (gateway != null && gateway.IsRunning))
                .AppendLine("Gateway: " + GatewayEndpointText.Text)
                .AppendLine("Product: " + CadHostInfo.Product)
                .AppendLine("Version: " + CadHostInfo.Version)
                .AppendLine("PID: " + s.ProcessId)
                .AppendLine("Document: " + (s.Document ?? ""))
                .AppendLine("DocumentId: " + (s.DocumentId ?? ""))
                .AppendLine("Tools: " + RuntimeHost.Tools.Describe().Length)
                .ToString());
        }

        private void SelfCheckClick(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            var gateway = RuntimeHost.Gateway != null && RuntimeHost.Gateway.IsRunning;
            var instance = File.Exists(InstanceRegistry.CurrentPath);
            var descriptors = RuntimeHost.Tools.Describe();
            var names = new HashSet<string>(descriptors.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var isCivil = IsCivilHost();
            var expected = isCivil ? 61 : 31;
            var required = new List<string> { "cad.status", "cad.entity.get", "cad.entity.update", "cad.drawing.audit", "project.revit.prepare", "project.image.embed", "cad.scale.convert" };
            if (isCivil) required.AddRange(new[] { "civil.status", "civil.surface.list", "civil.pipe.get", "civil.corridor.list" });
            var missing = required.Where(x => !names.Contains(x)).ToArray();
            var countOk = descriptors.Length >= expected;
            var ok = gateway && instance && countOk && missing.Length == 0;
            var missingText = missing.Length == 0 ? "无" : string.Join(", ", missing);
            MessageBox.Show(
                $"CAD Gateway: {(gateway ? "OK" : "FAIL")}\nInstance: {(instance ? "OK" : "FAIL")}\nLive Tools: {descriptors.Length} / expected {expected}\n关键 Tool 缺失: {missingText}\n\n结论：{(ok ? "基础运行正常" : "请查看 runtime.log")}",
                "YangCAD MCP 自检",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void OpenLogsClick(object sender, RoutedEventArgs e)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YangCADAgent", "logs");
            Directory.CreateDirectory(folder);
            try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "YangCAD", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private static bool IsCivilHost()
            => CadHostInfo.Product.IndexOf("Civil", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Short(string? value, int length)
            => string.IsNullOrWhiteSpace(value) ? "-" : value!.Length <= length ? value! : value!.Substring(0, length) + "…";

        private void RefreshClick(object sender, RoutedEventArgs e) => RefreshStatus();
        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
