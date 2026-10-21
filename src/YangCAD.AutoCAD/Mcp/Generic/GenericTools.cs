using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Mcp.Tools;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp.Generic
{
    public sealed class CadObjectDescribeTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.object.describe", Description = "通用对象反射：查看运行时类型、属性和可调用方法。" };
        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var includeValues = ToolJson.Bool(context.Args, "include_values") ?? true;
            var includeMethods = ToolJson.Bool(context.Args, "include_methods") ?? true;
            var maxMembers = ToolJson.Int(context.Args, "max_members") ?? 120;
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var target = GenericAccess.OpenByHandle(tr, context.Database, handle, OpenMode.ForRead);
                return GenericAccess.DescribeObject(target, includeValues, includeMethods, maxMembers);
            });
        }
    }

    public sealed class CadPropertyGetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.property.get", Description = "读取任意 Autodesk 对象的 JSON-safe 公共属性。" };
        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var propertyName = ToolJson.RequiredString(context.Args, "property");
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var target = GenericAccess.OpenByHandle(tr, context.Database, handle, OpenMode.ForRead);
                var property = GenericAccess.GetPropertyForRead(target.GetType(), propertyName);
                return new { handle, runtime_type = target.GetType().FullName, property = property.Name, type = property.PropertyType.FullName, value = GenericAccess.ReadProperty(target, property) };
            });
        }
    }

    public sealed class CadPropertySetTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.property.set", Description = "受控通用属性写入；只允许公共、JSON 可转换的 Autodesk setter。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var propertyName = ToolJson.RequiredString(context.Args, "property");
            if (!context.Args.TryGetValue("value", out var rawValue)) throw new ArgumentException("缺少参数：value");
            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var target = GenericAccess.OpenByHandle(tr, context.Database, handle, OpenMode.ForWrite);
                var property = GenericAccess.GetPropertyForWrite(target.GetType(), propertyName);
                object? before = null;
                try { if (property.CanRead) before = GenericAccess.ToSerializable(property.GetValue(target, null)); } catch { }
                var converted = GenericAccess.ConvertFor(property.PropertyType, rawValue, context.Database);
                try { property.SetValue(target, converted, null); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                object? after = null;
                try { if (property.CanRead) after = GenericAccess.ToSerializable(property.GetValue(target, null)); } catch { }
                return new { handle, runtime_type = target.GetType().FullName, property = property.Name, type = property.PropertyType.FullName, dry_run = context.DryRun, before, after };
            });
        }
    }

    public sealed class CadApiDescribeTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.api.describe", Description = "查看对象上可由通用 API 层调用的公共 Autodesk 方法及精确 signature。" };
        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var method = ToolJson.String(context.Args, "method");
            var maxMembers = ToolJson.Int(context.Args, "max_members") ?? 120;
            return CadTransactionRunner.Read(context.Document, tr =>
            {
                var target = GenericAccess.OpenByHandle(tr, context.Database, handle, OpenMode.ForRead);
                var methods = GenericAccess.DescribeMethods(target.GetType(), method, maxMembers);
                return new { handle, runtime_type = target.GetType().FullName, filter = method, count = methods.Count, methods };
            });
        }
    }

    public sealed class CadApiInvokeTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.api.invoke", Description = "受控调用对象公共 Autodesk API；拒绝 ref/out、基础设施危险方法和不支持的复杂类型。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext context)
        {
            var handle = ToolJson.RequiredString(context.Args, "handle");
            var methodName = ToolJson.RequiredString(context.Args, "method");
            var signature = ToolJson.String(context.Args, "signature");
            var rawArgs = ToolJson.List(context.Args, "arguments") ?? new List<object?>();
            return CadTransactionRunner.Write(context.Document, context.DryRun, tr =>
            {
                var target = GenericAccess.OpenByHandle(tr, context.Database, handle, OpenMode.ForWrite);
                var method = GenericAccess.ResolveMethod(target.GetType(), methodName, signature, rawArgs, context.Database, out var converted);
                object? result;
                try { result = method.Invoke(target, converted); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                return new
                {
                    handle,
                    runtime_type = target.GetType().FullName,
                    method = method.Name,
                    signature = method.Name + "(" + string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name)) + ")",
                    dry_run = context.DryRun,
                    result = GenericAccess.ToSerializable(result)
                };
            });
        }
    }

    public sealed class CadCommandRunTool : ICadTool
    {
        public ToolDescriptor Descriptor { get; } = new ToolDescriptor { Name = "cad.command.run", Description = "实验性同步原生命令 fallback；默认禁用，不允许交互暂停。", IsWrite = true, SupportsDryRun = true };
        public object Execute(CadToolContext context)
        {
            var command = ToolJson.RequiredString(context.Args, "command");
            if (!System.Text.RegularExpressions.Regex.IsMatch(command, @"^[A-Za-z0-9_.-]+$"))
                throw new ArgumentException("command 只能包含字母、数字、下划线、点和连字符；禁止拼接脚本/换行。");
            var rawArgs = ToolJson.List(context.Args, "arguments") ?? new List<object?>();
            var tokens = new List<object> { command };
            foreach (var raw in rawArgs) tokens.Add(ConvertCommandArgument(raw));
            if (context.DryRun) return new { dry_run = true, command, arguments = rawArgs, executed = false };

            // Editor.Command is synchronous. It may still reject a command that cannot run from the
            // current modeless command context; such failures are returned instead of falling back to
            // an asynchronous command queue, whose completion would violate MCP result semantics.
            context.Document.Editor.Command(tokens.ToArray());
            return new { dry_run = false, command, arguments = rawArgs, executed = true, note = "Undo 粒度由该原生命令决定；TASK-002 必须逐命令实机验证。" };
        }

        private static object ConvertCommandArgument(object? raw)
        {
            if (raw == null) return "";
            if (raw is string text)
            {
                if (text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0 || text == "\\" || text.IndexOf("^C", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new ArgumentException("command arguments 禁止换行、PauseToken 和 ^C 控制序列。");
                return text;
            }
            if (raw is int || raw is short || raw is long || raw is byte || raw is double || raw is float || raw is decimal) return raw;
            if (raw is IEnumerable sequence)
            {
                var values = new List<double>();
                foreach (var item in sequence)
                {
                    if (item == null) throw new ArgumentException("坐标参数不能包含 null。");
                    values.Add(Convert.ToDouble(item, System.Globalization.CultureInfo.InvariantCulture));
                }
                if (values.Count == 2) return new Autodesk.AutoCAD.Geometry.Point3d(values[0], values[1], 0);
                if (values.Count == 3) return new Autodesk.AutoCAD.Geometry.Point3d(values[0], values[1], values[2]);
            }
            throw new ArgumentException("command arguments 仅支持字符串、数值和 [X,Y]/[X,Y,Z] 坐标。");
        }
    }
}
