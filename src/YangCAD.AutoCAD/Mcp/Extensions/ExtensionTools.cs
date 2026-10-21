using System;
using System.Collections.Generic;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Extensions
{
    public sealed class CadExtensionListTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.extension.list", Description = "扫描并列出本地 YangCAD 动态扩展 operation。" };
        public object Execute(CadToolContext context)
            => UserSettings.Load().AllowExtensionInvoke ? ExtensionHost.List() : ExtensionHost.ListFilesOnly();
    }

    public sealed class CadExtensionInvokeTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.extension.invoke", Description = "调用本地版本化扩展 operation；默认额外权限关闭。", IsWrite = true, SupportsDryRun = true };
        public object? Execute(CadToolContext context)
        {
            var operation = ToolJson.RequiredString(context.Args, "operation");
            var args = ToolJson.Object(context.Args, "arguments") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            return ExtensionHost.Invoke(context, operation, args);
        }
    }
}
