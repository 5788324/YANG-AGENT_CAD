using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Mcp
{
    /// <summary>
    /// Builds a token-free MCP client handoff for the exact AutoCAD/Civil 3D process
    /// whose Control Center is open. The stdio bridge discovers the localhost Gateway
    /// token from the user-local instance descriptor; the clipboard never receives it.
    /// </summary>
    internal static class McpClientConfigBuilder
    {
        private const string LauncherName = "Start-YangCAD-Mcp.ps1";

        public static string BuildCopyText()
        {
            var snapshot = InstanceRegistry.Snapshot();
            if (snapshot.ProcessId <= 0 || snapshot.Port <= 0 || RuntimeHost.Gateway == null || !RuntimeHost.Gateway.IsRunning)
                throw new InvalidOperationException("YangCAD MCP 尚未就绪。请先打开一个 DWG，并确认 Ribbon → MCP 显示“CAD 网关就绪”。");

            var launcher = ResolveLauncherPath();
            if (string.IsNullOrWhiteSpace(launcher) || !File.Exists(launcher))
                throw new FileNotFoundException(
                    "未找到 YangCAD MCP 启动脚本。请执行最新 YangCAD 安装包的更新/修复后重试。",
                    launcher);

            var server = new Dictionary<string, object>
            {
                ["command"] = "powershell.exe",
                ["args"] = new[]
                {
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    launcher
                },
                ["env"] = new Dictionary<string, string>
                {
                    ["YANGCAD_INSTANCE_PID"] = snapshot.ProcessId.ToString(CultureInfo.InvariantCulture)
                }
            };
            var config = new Dictionary<string, object>
            {
                ["mcpServers"] = new Dictionary<string, object>
                {
                    ["yangcad"] = server
                }
            };

            var json = JsonCodec.Serialize(config, indented: true);
            var tools = RuntimeHost.Tools.Describe().Length;
            return new StringBuilder()
                .AppendLine("YangCAD MCP 本机连接配置")
                .AppendLine("请把下面整段交给本机 AI。让它把 yangcad 添加为 MCP Server，然后调用 cad.status 验证连接。")
                .AppendLine()
                .AppendLine("当前会话：")
                .AppendLine("PID: " + snapshot.ProcessId.ToString(CultureInfo.InvariantCulture))
                .AppendLine("Product: " + (snapshot.Product ?? ""))
                .AppendLine("Version: " + (snapshot.Version ?? ""))
                .AppendLine("Document: " + (snapshot.Document ?? ""))
                .AppendLine("Live Tools: " + tools.ToString(CultureInfo.InvariantCulture))
                .AppendLine("Instance: " + InstanceRegistry.CurrentPath)
                .AppendLine()
                .AppendLine("标准 MCP JSON：")
                .AppendLine(json)
                .AppendLine()
                .AppendLine("验证步骤：")
                .AppendLine("1. 添加/更新上面的 MCP Server 配置。不同 AI 客户端格式若不同，请按相同 command / args / env 转换。")
                .AppendLine("2. 启动 yangcad MCP Server。")
                .AppendLine("3. 调用 cad.status；写操作前先调用读取 Tool 建立当前 DWG 绑定。")
                .AppendLine()
                .AppendLine("注意：此配置只绑定当前 CAD 进程 PID，不包含 Gateway Token。CAD/Civil 3D 重启后 PID 会变化，请重新点击“复制 MCP 配置”。")
                .ToString();
        }

        internal static string ResolveLauncherPath()
        {
            var installedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "ApplicationPlugins", "YangCADAgent.bundle",
                "Contents", "Mcp");
            if (RuntimeAssetMaterializer.IsMcpRuntimeHealthy(installedRoot))
                return Path.Combine(installedRoot, LauncherName);
            if (Directory.Exists(installedRoot))
                RuntimeLogger.Info("Installed MCP runtime is incomplete or stale; falling back to a healthy runtime copy.");

            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var cursor = string.IsNullOrWhiteSpace(assemblyLocation)
                ? null
                : new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

            for (var depth = 0; cursor != null && depth < 8; depth++, cursor = cursor.Parent)
            {
                var roots = new[]
                {
                    Path.Combine(cursor.FullName, "Mcp"),
                    Path.Combine(cursor.FullName, "mcp"),
                    Path.Combine(cursor.FullName, "Contents", "Mcp")
                };
                foreach (var root in roots)
                    if (RuntimeAssetMaterializer.IsMcpRuntimeHealthy(root))
                        return Path.Combine(root, LauncherName);
            }

            // Full self-heal: any missing/stale runtime asset causes the embedded, version-matched
            // runtime to be materialized into %LOCALAPPDATA%. This covers DLL-only deployments and
            // partial bundle damage where the launcher still exists but Python modules do not.
            try
            {
                var launcher = RuntimeAssetMaterializer.EnsureMcpLauncher();
                var root = Path.GetDirectoryName(launcher) ?? "";
                if (!RuntimeAssetMaterializer.IsMcpRuntimeHealthy(root))
                    throw new InvalidDataException("嵌入式 MCP runtime materialize 后完整性校验失败。");
                return launcher;
            }
            catch (Exception ex)
            {
                throw new FileNotFoundException(
                    "未找到完整可用的 YangCAD MCP runtime，且嵌入式自动修复失败。请执行 YangCAD 更新/修复后重试。",
                    Path.Combine(installedRoot, LauncherName), ex);
            }
        }
    }
}
