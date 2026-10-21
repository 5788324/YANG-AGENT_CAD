using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace YangCAD.AutoCADHost.Infrastructure
{
    internal static class RuntimeAssetMaterializer
    {
        private sealed class Asset
        {
            public string ResourceName { get; set; } = "";
            public string RelativePath { get; set; } = "";
        }

        private static readonly Asset[] McpAssets =
        {
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.Start-YangCAD-Mcp.ps1", RelativePath = "Start-YangCAD-Mcp.ps1" },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.pyproject.toml", RelativePath = "pyproject.toml" },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.yangcad_mcp.__init__.py", RelativePath = Path.Combine("yangcad_mcp", "__init__.py") },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.yangcad_mcp.batch.py", RelativePath = Path.Combine("yangcad_mcp", "batch.py") },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.yangcad_mcp.extensions.py", RelativePath = Path.Combine("yangcad_mcp", "extensions.py") },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.yangcad_mcp.gateway.py", RelativePath = Path.Combine("yangcad_mcp", "gateway.py") },
            new Asset { ResourceName = "YangCAD.Runtime.Mcp.yangcad_mcp.server.py", RelativePath = Path.Combine("yangcad_mcp", "server.py") }
        };

        private static readonly Asset[] ImageEmbedAssets =
        {
            new Asset { ResourceName = "YangCAD.Runtime.ImageEmbed.Start-YangCAD-ImageEmbed.ps1", RelativePath = "Start-YangCAD-ImageEmbed.ps1" }
        };

        public static string EnsureMcpLauncher()
        {
            var root = EnsureGroup("Mcp", McpAssets);
            return Path.Combine(root, "Start-YangCAD-Mcp.ps1");
        }

        internal static bool IsMcpRuntimeHealthy(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var asset in McpAssets)
                {
                    var target = Path.Combine(root, asset.RelativePath);
                    if (!File.Exists(target)) return false;
                    using (var stream = assembly.GetManifestResourceStream(asset.ResourceName))
                    {
                        if (stream == null) return false;
                        using (var memory = new MemoryStream())
                        {
                            stream.CopyTo(memory);
                            if (!SameContent(target, memory.ToArray())) return false;
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string EnsureImageEmbedWorker()
        {
            var root = EnsureGroup("ImageEmbed", ImageEmbedAssets);
            return Path.Combine(root, "Start-YangCAD-ImageEmbed.ps1");
        }

        private static string EnsureGroup(string group, IReadOnlyList<Asset> assets)
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YangCADAgent", "runtime", group);
            Directory.CreateDirectory(root);

            var assembly = Assembly.GetExecutingAssembly();
            foreach (var asset in assets)
            {
                var target = Path.Combine(root, asset.RelativePath);
                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

                using (var stream = assembly.GetManifestResourceStream(asset.ResourceName))
                {
                    if (stream == null)
                        throw new InvalidOperationException("YangCAD 嵌入式运行资源缺失：" + asset.ResourceName);
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        var bytes = memory.ToArray();
                        if (File.Exists(target) && SameContent(target, bytes)) continue;

                        var temp = target + ".tmp." + Guid.NewGuid().ToString("N");
                        File.WriteAllBytes(temp, bytes);
                        try
                        {
                            if (File.Exists(target))
                                File.Replace(temp, target, null);
                            else
                                File.Move(temp, target);
                        }
                        catch (IOException)
                        {
                            // Another CAD process may materialize the same shared runtime concurrently.
                            // Accept the race only when the winning file is byte-identical.
                            if (File.Exists(target) && SameContent(target, bytes))
                            {
                                if (File.Exists(temp)) File.Delete(temp);
                                continue;
                            }
                            throw;
                        }
                        finally
                        {
                            if (File.Exists(temp)) File.Delete(temp);
                        }
                    }
                }
            }
            return root;
        }

        private static bool SameContent(string path, byte[] expected)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length != expected.LongLength) return false;
                using (var sha = SHA256.Create())
                using (var file = File.OpenRead(path))
                {
                    var actualHash = sha.ComputeHash(file);
                    var expectedHash = sha.ComputeHash(expected);
                    if (actualHash.Length != expectedHash.Length) return false;
                    for (var i = 0; i < actualHash.Length; i++)
                        if (actualHash[i] != expectedHash[i]) return false;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
