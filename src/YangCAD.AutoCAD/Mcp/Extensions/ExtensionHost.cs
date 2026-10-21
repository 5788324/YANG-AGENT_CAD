using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Mcp.Extensions
{
    internal static class ExtensionHost
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, LoadedOperation> Operations = new Dictionary<string, LoadedOperation>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LoadErrors = new List<string>();

        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YangCADAgent", "Extensions");

        public static object ListFilesOnly()
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Root);
                var files = Directory.GetFiles(Root, "*.dll")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new
                    {
                        name = Path.GetFileName(x),
                        path = x,
                        managed_manifest = File.Exists(ManifestPath(x))
                    })
                    .ToArray();
                return new
                {
                    folder = Root,
                    enabled = false,
                    count = files.Length,
                    files,
                    note = "动态扩展执行权限关闭：仅列出 DLL 文件，不加载程序集、不实例化扩展。"
                };
            }
        }

        public static object List()
        {
            lock (Sync)
            {
                Refresh();
                return new
                {
                    folder = Root,
                    enabled = true,
                    count = Operations.Count,
                    operations = Operations.Values.OrderBy(x => x.Operation.Descriptor.Name).Select(x => new
                    {
                        name = x.Operation.Descriptor.Name,
                        version = x.Operation.Descriptor.Version,
                        description = x.Operation.Descriptor.Description,
                        is_write = x.Operation.Descriptor.IsWrite,
                        supports_dry_run = x.Operation.Descriptor.SupportsDryRun,
                        requires_erase = x.Operation.Descriptor.RequiresErase,
                        assembly = x.AssemblyName,
                        path = x.Path,
                        managed = x.Manifest != null,
                        target_product = x.Manifest?.TargetProduct,
                        source_sha256 = x.Manifest?.SourceSha256,
                        dll_sha256 = x.DllSha256,
                        build_id = x.Manifest?.BuildId,
                    }).ToArray(),
                    errors = LoadErrors.ToArray()
                };
            }
        }

        public static object? Invoke(CadToolContext context, string operationName, IReadOnlyDictionary<string, object?> args)
        {
            LoadedOperation loaded;
            lock (Sync)
            {
                Refresh();
                if (!Operations.TryGetValue(operationName, out loaded!))
                    throw new InvalidOperationException("未找到动态扩展 operation：" + operationName + "。请先调用 cad.extension.list。 ");
            }

            VerifyLoadedArtifact(loaded);
            var descriptor = loaded.Operation.Descriptor;
            if (context.DryRun && descriptor.IsWrite && !descriptor.SupportsDryRun)
                throw new InvalidOperationException("该扩展未声明 dry_run 支持，拒绝模拟执行。");
            if (descriptor.RequiresErase && !UserSettings.Load().AllowErase)
                throw new InvalidOperationException("该扩展声明 RequiresErase=true，但当前删除权限未开启。");

            var unauthorizedEraseDetected = false;
            var readMutationDetected = false;
            ObjectErasedEventHandler? erasedHandler = null;
            ObjectEventHandler? appendedHandler = null;
            ObjectEventHandler? modifiedHandler = null;

            if (!descriptor.RequiresErase || !descriptor.IsWrite)
            {
                erasedHandler = (sender, eventArgs) =>
                {
                    if (!eventArgs.Erased) return;
                    if (!descriptor.RequiresErase) unauthorizedEraseDetected = true;
                    if (!descriptor.IsWrite) readMutationDetected = true;
                };
                context.Database.ObjectErased += erasedHandler;
            }
            if (!descriptor.IsWrite)
            {
                appendedHandler = (sender, eventArgs) => readMutationDetected = true;
                modifiedHandler = (sender, eventArgs) => readMutationDetected = true;
                context.Database.ObjectAppended += appendedHandler;
                context.Database.ObjectModified += modifiedHandler;
            }

            InvocationPayload payload;
            try
            {
                Func<Transaction, InvocationPayload> invokeWithinTransaction = tr =>
                {
                    var extensionContext = new YangCadExtensionContext(context.Document, tr, context.DryRun, args);
                    var result = loaded.Operation.Execute(extensionContext);

                    if (!descriptor.IsWrite && readMutationDetected)
                        throw new InvalidOperationException("只读 Extension 尝试修改 DWG；Host 已拒绝并回滚。请将 Descriptor.IsWrite=true 后重新 build/install。");
                    if (!descriptor.RequiresErase && unauthorizedEraseDetected)
                        throw new InvalidOperationException("Extension 实际执行了 Erase，但未声明 RequiresErase=true；Host 已拒绝并回滚。");
                    if (descriptor.IsWrite && extensionContext.Changes.Count == 0)
                        throw new InvalidOperationException("写 Extension 未产生任何 RecordChange 证据；Host 拒绝提交。请至少记录一条真实 change ledger。");

                    return new InvocationPayload(result, extensionContext.Changes.ToArray());
                };

                if (descriptor.IsWrite)
                {
                    payload = CadTransactionRunner.Write(context.Document, context.DryRun, invokeWithinTransaction);
                }
                else
                {
                    // Read extensions are always executed in a host-owned transaction that aborts,
                    // so a source-policy bypass cannot persist DWG changes even if it acquires write access.
                    payload = CadTransactionRunner.Write(context.Document, true, invokeWithinTransaction);
                }
            }
            finally
            {
                if (erasedHandler != null) context.Database.ObjectErased -= erasedHandler;
                if (appendedHandler != null) context.Database.ObjectAppended -= appendedHandler;
                if (modifiedHandler != null) context.Database.ObjectModified -= modifiedHandler;
            }

            return new
            {
                operation = descriptor.Name,
                version = descriptor.Version,
                assembly = loaded.AssemblyName,
                managed = loaded.Manifest != null,
                build_id = loaded.Manifest?.BuildId,
                dll_sha256 = loaded.DllSha256,
                is_write = descriptor.IsWrite,
                requires_erase = descriptor.RequiresErase,
                requested_dry_run = context.DryRun,
                preview = descriptor.IsWrite && context.DryRun,
                committed = descriptor.IsWrite && !context.DryRun,
                rolled_back = descriptor.IsWrite && context.DryRun,
                rollback_reason = descriptor.IsWrite && context.DryRun ? "dry_run_host_transaction_abort" : null,
                undo_scope = descriptor.IsWrite && !context.DryRun ? "single_host_transaction" : null,
                read_isolation = descriptor.IsWrite ? null : "host_forced_transaction_abort",
                change_ledger_semantics = "extension_reported_json_safe_evidence_not_independent_host_snapshot",
                change_count = payload.Changes.Length,
                changes = payload.Changes,
                result = payload.Result
            };
        }

        private static void Refresh()
        {
            Directory.CreateDirectory(Root);
            foreach (var path in Directory.GetFiles(Root, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var full = Path.GetFullPath(path);
                if (LoadedAssemblies.Contains(full)) continue;
                LoadedAssemblies.Add(full);
                try
                {
                    var manifest = TryReadManifest(full);
                    if (manifest != null)
                    {
                        ValidateManifest(manifest, full);
                        if (!YearMatchesHost(manifest.Year)) continue;
                        if (!TargetMatchesHost(manifest.TargetProduct)) continue;
                        VerifyHostContractSha(manifest);
                    }

                    var dllSha = ComputeSha256(full);
                    if (manifest != null && !string.Equals(dllSha, manifest.DllSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Extension DLL SHA-256 与 manifest 不一致，拒绝加载。");

                    var assembly = Assembly.LoadFrom(full);
                    foreach (var type in SafeTypes(assembly))
                    {
                        if (type.IsAbstract || type.IsInterface || !typeof(IYangCadExtensionOperation).IsAssignableFrom(type)) continue;
                        if (!(Activator.CreateInstance(type) is IYangCadExtensionOperation operation)) continue;
                        Validate(operation.Descriptor);
                        ValidateManifestAgainstDescriptor(manifest, operation.Descriptor, assembly.GetName().Name ?? "");
                        if (Operations.ContainsKey(operation.Descriptor.Name))
                            throw new InvalidOperationException("重复 extension operation：" + operation.Descriptor.Name + "。请使用唯一 operation/版本化 DLL。");
                        Operations[operation.Descriptor.Name] = new LoadedOperation(operation, assembly.GetName().Name ?? "", full, dllSha, manifest);
                    }
                }
                catch (Exception ex)
                {
                    LoadErrors.Add(Path.GetFileName(full) + ": " + Unwrap(ex).Message);
                }
            }
        }

        private static string ManifestPath(string dllPath) => dllPath + ".yangcad.json";

        private static YangCadExtensionArtifactManifest? TryReadManifest(string dllPath)
        {
            var path = ManifestPath(dllPath);
            if (!File.Exists(path)) return null;
            var manifest = JsonCodec.Deserialize<YangCadExtensionArtifactManifest>(File.ReadAllText(path));
            if (manifest == null) throw new InvalidOperationException("Extension manifest 为空或无法解析。");
            return manifest;
        }

        private static void ValidateManifest(YangCadExtensionArtifactManifest manifest, string dllPath)
        {
            if (manifest.SchemaVersion != 1) throw new InvalidOperationException("不支持的 Extension manifest schema：" + manifest.SchemaVersion);
            if (string.IsNullOrWhiteSpace(manifest.BuildId) || string.IsNullOrWhiteSpace(manifest.Operation) || string.IsNullOrWhiteSpace(manifest.DllSha256))
                throw new InvalidOperationException("Extension manifest 缺少 BuildId/Operation/DllSha256。");
            if (!string.Equals(Path.GetFileName(dllPath), manifest.DllFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extension manifest DllFile 与实际文件名不一致。");
            if (!Regex.IsMatch(manifest.DllSha256, "^[0-9A-Fa-f]{64}$"))
                throw new InvalidOperationException("Extension manifest DllSha256 格式无效。");
            if (!string.Equals(manifest.TargetProduct, "AutoCAD", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(manifest.TargetProduct, "Civil3D", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extension manifest TargetProduct 仅支持 AutoCAD/Civil3D。");
            if (manifest.Year < 2021 || manifest.Year > 2027)
                throw new InvalidOperationException("Extension manifest Year 仅支持 2021~2027。");
            if (string.IsNullOrWhiteSpace(manifest.HostDllSha256) || !Regex.IsMatch(manifest.HostDllSha256, "^[0-9A-Fa-f]{64}$"))
                throw new InvalidOperationException("Extension manifest HostDllSha256 缺失或格式无效。");
        }

        private static void ValidateManifestAgainstDescriptor(YangCadExtensionArtifactManifest? manifest, YangCadExtensionDescriptor descriptor, string assemblyName)
        {
            if (manifest == null) return; // legacy/manual extension; still behind the existing explicit permission gate.
            if (!string.Equals(manifest.Operation, descriptor.Name, StringComparison.Ordinal))
                throw new InvalidOperationException("Extension manifest Operation 与 Descriptor.Name 不一致。");
            if (!string.Equals(manifest.Version ?? "", descriptor.Version ?? "", StringComparison.Ordinal))
                throw new InvalidOperationException("Extension manifest Version 与 Descriptor.Version 不一致。");
            if (!string.Equals(manifest.AssemblyName ?? "", assemblyName, StringComparison.Ordinal))
                throw new InvalidOperationException("Extension manifest AssemblyName 与 DLL 不一致。");
            if (manifest.IsWrite != descriptor.IsWrite || manifest.SupportsDryRun != descriptor.SupportsDryRun || manifest.RequiresErase != descriptor.RequiresErase)
                throw new InvalidOperationException("Extension manifest capability 与 Descriptor 不一致。");
        }

        private static bool TargetMatchesHost(string targetProduct)
        {
            if (string.Equals(targetProduct, "AutoCAD", StringComparison.OrdinalIgnoreCase)) return true;
            return CadHostInfo.Product.IndexOf("Civil 3D", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool YearMatchesHost(int year)
        {
            var version = CadHostInfo.Version ?? "";
            var expected = year == 2021 ? "24.0" :
                           year == 2022 ? "24.1" :
                           year == 2023 ? "24.2" :
                           year == 2024 ? "24.3" :
                           year == 2025 ? "25.0" :
                           year == 2026 ? "25.1" :
                           year == 2027 ? "26.0" : "";
            return !string.IsNullOrWhiteSpace(expected) && version.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void VerifyHostContractSha(YangCadExtensionArtifactManifest manifest)
        {
            var hostPath = typeof(IYangCadExtensionOperation).Assembly.Location;
            if (string.IsNullOrWhiteSpace(hostPath) || !File.Exists(hostPath))
                throw new InvalidOperationException("无法定位当前 YangCAD.AutoCAD Host DLL，拒绝加载 managed Extension。");
            var currentHostSha = ComputeSha256(hostPath);
            if (!string.Equals(currentHostSha, manifest.HostDllSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extension 编译时 Host DLL SHA 与当前运行 Host 不一致；请为当前部署重新 build/install 版本化 Extension。");
        }

        private static void VerifyLoadedArtifact(LoadedOperation loaded)
        {
            if (loaded.Manifest == null) return;
            if (!File.Exists(loaded.Path)) throw new InvalidOperationException("已加载 Extension DLL 文件已不存在；为避免来源漂移，本次拒绝 invoke。请重启 CAD 后重新安装/加载版本化 DLL。");
            var currentSha = ComputeSha256(loaded.Path);
            if (!string.Equals(currentSha, loaded.DllSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("已加载 Extension DLL 文件内容发生变化；为避免内存程序集与磁盘来源不一致，本次拒绝 invoke。请使用新版本文件名并重启/重新加载。");
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).Cast<Type>(); }
        }

        private static void Validate(YangCadExtensionDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Name)) throw new InvalidOperationException("Extension Descriptor.Name 不能为空。");
            if (!Regex.IsMatch(descriptor.Name, @"^[a-zA-Z0-9_.-]+$")) throw new InvalidOperationException("Extension operation 名称包含非法字符：" + descriptor.Name);
            if (descriptor.RequiresErase && !descriptor.IsWrite) throw new InvalidOperationException("RequiresErase=true 的 Extension 必须 IsWrite=true。");
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static Exception Unwrap(Exception exception)
            => exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;

        private sealed class InvocationPayload
        {
            public object? Result { get; }
            public YangCadExtensionChange[] Changes { get; }
            public InvocationPayload(object? result, YangCadExtensionChange[] changes)
            { Result = result; Changes = changes; }
        }

        private sealed class LoadedOperation
        {
            public IYangCadExtensionOperation Operation { get; }
            public string AssemblyName { get; }
            public string Path { get; }
            public string DllSha256 { get; }
            public YangCadExtensionArtifactManifest? Manifest { get; }
            public LoadedOperation(IYangCadExtensionOperation operation, string assemblyName, string path, string dllSha256, YangCadExtensionArtifactManifest? manifest)
            { Operation = operation; AssemblyName = assemblyName; Path = path; DllSha256 = dllSha256; Manifest = manifest; }
        }
    }
}
