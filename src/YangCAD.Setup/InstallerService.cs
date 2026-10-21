using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;
using System.Xml;
using Microsoft.Win32;

namespace YangCAD.Setup
{
    internal enum SetupAction
    {
        Install,
        Update,
        Repair,
        Uninstall
    }

    internal sealed class SetupStatus
    {
        public bool PackageReady { get; set; }
        public string PackageVersion { get; set; }
        public bool Installed { get; set; }
        public string InstalledVersion { get; set; }
        public bool TrustedPathConfigured { get; set; }
        public bool AutoLoadConfigured { get; set; }
        public int AutodeskProfilesFound { get; set; }
        public bool SignedPackage { get; set; }
        public string InstallPath { get; set; }
        public string Message { get; set; }
    }

    internal sealed class AutodeskProfileSummary
    {
        public int Found { get; set; }
        public int Trusted { get; set; }
        public int AutoLoadReady { get; set; }
    }

    internal sealed class InstallerService
    {
        private const string BundleName = "YangCADAgent.bundle";
        private const string PublisherCertificateName = "YangCADAgent_Publisher.cer";
        private const string ReleaseManifestName = "release-manifest.json";
        private const string AutoCadUserRoot = @"Software\Autodesk\AutoCAD";
        private const int AppAutoLoadStartupBit = 2;
        private const int AppAutoLoadDetectedBit = 8;
        private const int AppAutoLoadDefault = 14;

        private static readonly Dictionary<int, string> ReleaseKeyByYear = new Dictionary<int, string>
        {
            { 2021, "R24.0" }, { 2022, "R24.1" }, { 2023, "R24.2" }, { 2024, "R24.3" },
            { 2025, "R25.0" }, { 2026, "R25.1" }, { 2027, "R26.0" }
        };

        private static readonly HashSet<string> SupportedReleaseKeys =
            new HashSet<string>(ReleaseKeyByYear.Values, StringComparer.OrdinalIgnoreCase);

        private readonly string _packageRoot;
        private readonly string _sourceBundle;
        private readonly string _installBase;
        private readonly string _installBundle;
        private readonly string _manifestPath;
        private readonly string _publisherCertificatePath;
        private ReleaseManifest _manifest;

        public InstallerService(string packageRoot)
        {
            _packageRoot = Path.GetFullPath(packageRoot);
            _sourceBundle = Path.Combine(_packageRoot, BundleName);
            _installBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins");
            _installBundle = Path.Combine(_installBase, BundleName);
            _manifestPath = Path.Combine(_packageRoot, ReleaseManifestName);
            _publisherCertificatePath = Path.Combine(_packageRoot, PublisherCertificateName);
        }

        public string InstallPath { get { return _installBundle; } }

        public SetupStatus GetStatus()
        {
            var status = new SetupStatus
            {
                InstallPath = _installBundle,
                Installed = Directory.Exists(_installBundle),
                InstalledVersion = ReadBundleVersion(_installBundle)
            };

            try
            {
                _manifest = LoadManifest();
                var profileSummary = ReadAutodeskProfileSummary();
                status.AutodeskProfilesFound = profileSummary.Found;
                status.TrustedPathConfigured = profileSummary.Found > 0 && profileSummary.Trusted == profileSummary.Found;
                status.AutoLoadConfigured = profileSummary.Found > 0 && profileSummary.AutoLoadReady == profileSummary.Found;
                status.PackageVersion = _manifest.version;
                status.SignedPackage = IsSignedManifest(_manifest);
                status.PackageReady = Directory.Exists(_sourceBundle) && RequiredRuntimeAssetsPresent(_sourceBundle) && (IsQuickManifest(_manifest) || File.Exists(_publisherCertificatePath));
                status.Message = status.PackageReady ? "快速安装包已就绪" : "发布包不完整（请检查 MCP / ImageEmbed runtime）";
            }
            catch (Exception ex)
            {
                status.PackageReady = false;
                status.Message = ex.Message;
            }
            return status;
        }

        public string Execute(SetupAction action, bool purgeUserData, Action<string> log)
        {
            if (log == null) log = delegate { };
            _manifest = LoadManifest();
            if (action == SetupAction.Uninstall)
                return Uninstall(purgeUserData, log);

            AssertAutodeskHostsStopped();
            VerifyManifestHashes(log);
            VerifyRequiredRuntimeAssets(_sourceBundle, "发布包");
            VerifyPackageTrustMode(log);

            var sourceVersion = ParseVersion(_manifest.version, "发布包版本");
            var installedVersionText = ReadBundleVersion(_installBundle);
            Version installedVersion = null;
            if (!string.IsNullOrWhiteSpace(installedVersionText))
                installedVersion = ParseVersion(installedVersionText, "已安装版本");

            if (action == SetupAction.Install && installedVersion != null)
                throw new InvalidOperationException("YangCAD Agent 已安装。请使用“更新”或“修复”。");
            if (action == SetupAction.Update)
            {
                if (installedVersion == null) throw new InvalidOperationException("当前未安装 YangCAD Agent，请使用“安装”。");
                if (sourceVersion <= installedVersion)
                    throw new InvalidOperationException("当前发布包版本不高于已安装版本；不执行降级/同版本更新。需要同版本恢复请使用“修复”。");
            }
            if (action == SetupAction.Repair)
            {
                if (installedVersion == null) throw new InvalidOperationException("当前未安装 YangCAD Agent，请使用“安装”。");
                if (sourceVersion != installedVersion)
                    throw new InvalidOperationException("“修复”要求发布包版本与已安装版本一致；版本不同请使用“更新”。");
            }

            AtomicInstall(action, log);
            var installedAfter = ReadBundleVersion(_installBundle);
            if (!string.Equals(installedAfter, _manifest.version, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("安装后版本校验失败。expected=" + _manifest.version + " actual=" + installedAfter);

            VerifyInstalledBundleMatches(log);
            ConfigureAutodeskProfiles(log);
            return ActionName(action) + "完成：YangCAD Agent " + _manifest.version + "（快速安装）";
        }

        private string Uninstall(bool purgeUserData, Action<string> log)
        {
            AssertAutodeskHostsStopped();
            if (Directory.Exists(_installBundle))
            {
                var trash = _installBundle + ".__remove." + Guid.NewGuid().ToString("N");
                Directory.Move(_installBundle, trash);
                Directory.Delete(trash, true);
                log("已删除 User-scope bundle：" + _installBundle);
            }
            else
            {
                log("当前没有已安装的 YangCADAgent.bundle。");
            }

            RemoveYangCadTrustedPath(log);

            if (purgeUserData)
            {
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YangCADAgent");
                if (Directory.Exists(data))
                {
                    Directory.Delete(data, true);
                    log("已删除用户数据：" + data);
                }
                RemovePublisherTrustIfPresent(log);
            }
            else
            {
                log("已保留 %LOCALAPPDATA%\\YangCADAgent 用户数据。\r\n不会修改 SECURELOAD，也不会删除其他 TRUSTEDPATHS。");
            }
            return "卸载完成";
        }

        private void AtomicInstall(SetupAction action, Action<string> log)
        {
            Directory.CreateDirectory(_installBase);
            var stage = Path.Combine(_installBase, BundleName + ".__stage." + Guid.NewGuid().ToString("N"));
            string backup = null;
            bool installedNew = false;
            try
            {
                CopyDirectory(_sourceBundle, stage);
                RemoveZoneIdentifiers(stage);
                if (Directory.Exists(_installBundle))
                {
                    backup = Path.Combine(_installBase, BundleName + ".__backup." + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
                    Directory.Move(_installBundle, backup);
                }
                Directory.Move(stage, _installBundle);
                installedNew = true;
                VerifyDirectoryMatches(_sourceBundle, _installBundle);
                if (backup != null && Directory.Exists(backup)) Directory.Delete(backup, true);
                log(ActionName(action) + "：bundle 原子替换完成。");
            }
            catch
            {
                SafeDeleteDirectory(stage);
                if (backup != null && Directory.Exists(backup))
                {
                    SafeDeleteDirectory(_installBundle);
                    Directory.Move(backup, _installBundle);
                }
                else if (installedNew)
                {
                    SafeDeleteDirectory(_installBundle);
                }
                throw;
            }
        }

        private ReleaseManifest LoadManifest()
        {
            if (!File.Exists(_manifestPath)) throw new FileNotFoundException("缺少 release-manifest.json。请从完整 Release ZIP 中运行 Setup。", _manifestPath);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var manifest = serializer.Deserialize<ReleaseManifest>(File.ReadAllText(_manifestPath));
            if (manifest == null || manifest.files == null || string.IsNullOrWhiteSpace(manifest.version))
                throw new InvalidDataException("release-manifest.json 结构无效。");
            if (manifest.years == null || manifest.years.Length == 0 ||
                manifest.years.Distinct().Any(year => !ReleaseKeyByYear.ContainsKey(year)))
                throw new InvalidDataException("release-manifest.json years 无效；必须是 2021–2027 的非空目标年份集合。");
            if (!string.Equals(manifest.product, "YangCAD Agent", StringComparison.Ordinal))
                throw new InvalidDataException("release-manifest.json product 不匹配。");
            if (!IsQuickManifest(manifest) && !IsSignedManifest(manifest))
                throw new InvalidDataException("不支持的安装模式。当前 Setup 仅接受 quick-trusted-path 或 signed 发布包。");
            return manifest;
        }

        private static bool IsQuickManifest(ReleaseManifest manifest)
        {
            return manifest != null && manifest.installer != null &&
                   string.Equals(manifest.installer.mode, "quick-trusted-path", StringComparison.OrdinalIgnoreCase) &&
                   manifest.signing != null &&
                   string.Equals(manifest.signing.mode, "unsigned-quick", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSignedManifest(ReleaseManifest manifest)
        {
            return manifest != null && manifest.signing != null &&
                   string.Equals(manifest.signing.mode, "signed", StringComparison.OrdinalIgnoreCase);
        }

        private void VerifyPackageTrustMode(Action<string> log)
        {
            if (IsQuickManifest(_manifest))
            {
                log("快速安装模式：使用 YangCAD 专用 TRUSTEDPATHS；不要求数字签名/证书弹窗。");
                return;
            }

            if (!IsSignedManifest(_manifest))
                throw new InvalidDataException("release manifest signing mode 无效。");
            EnsurePublisherTrust(log);
            VerifySignedPayload(log);
        }

        private void VerifyManifestHashes(Action<string> log)
        {
            foreach (var item in _manifest.files)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.path) || string.IsNullOrWhiteSpace(item.sha256))
                    throw new InvalidDataException("release manifest 存在无效 file entry。");
                var path = SafePackagePath(item.path);
                if (!File.Exists(path)) throw new FileNotFoundException("发布包缺少文件：" + item.path, path);
                var info = new FileInfo(path);
                if (item.size > 0 && info.Length != item.size)
                    throw new InvalidDataException("文件大小不一致：" + item.path);
                var actual = Sha256(path);
                if (!string.Equals(actual, item.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256 不一致：" + item.path);
            }
            log("发布包 SHA-256 校验通过：" + _manifest.files.Length + " 个文件。");
        }

        private string SafePackagePath(string relativePath)
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized)) throw new InvalidDataException("release manifest 含绝对路径：" + relativePath);
            var full = Path.GetFullPath(Path.Combine(_packageRoot, normalized));
            var prefix = _packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("release manifest 路径越界：" + relativePath);
            return full;
        }

        private void ConfigureAutodeskProfiles(Action<string> log)
        {
            var found = 0;
            var updatedTrust = 0;
            var updatedAutoLoad = 0;
            var trustedEntry = TrustedPathEntry();

            using (var root = Registry.CurrentUser.OpenSubKey(AutoCadUserRoot, false))
            {
                if (root == null)
                    throw new InvalidOperationException("未发现当前用户 AutoCAD 注册表配置。请至少启动一次 AutoCAD/Civil 3D 后再执行“修复”。");

                foreach (var releaseName in root.GetSubKeyNames().Where(IsTargetReleaseKey))
                using (var release = root.OpenSubKey(releaseName, false))
                {
                    if (release == null) continue;
                    foreach (var productName in release.GetSubKeyNames().Where(n => n.StartsWith("ACAD-", StringComparison.OrdinalIgnoreCase)))
                    using (var product = release.OpenSubKey(productName, false))
                    using (var profiles = product == null ? null : product.OpenSubKey("Profiles", false))
                    {
                        if (profiles == null) continue;
                        foreach (var profileName in profiles.GetSubKeyNames())
                        using (var profile = profiles.OpenSubKey(profileName, true))
                        {
                            if (profile == null) continue;
                            using (var variables = profile.CreateSubKey("Variables"))
                            {
                                found++;
                                if (EnsureTrustedPath(variables, trustedEntry)) updatedTrust++;
                                if (EnsureAppAutoLoad(variables)) updatedAutoLoad++;
                            }
                        }
                    }
                }
            }

            if (found == 0)
                throw new InvalidOperationException("未发现当前 Release 目标年份的 AutoCAD/Civil 3D 当前用户 Profile。请至少启动一次目标版本后再执行“修复”。");

            log("TRUSTEDPATHS 已确保仅信任 YangCAD bundle：" + trustedEntry);
            log("目标年份 AutoCAD Profiles：" + found + "；新增/修复 trusted=" + updatedTrust + "，APPAUTOLOAD=" + updatedAutoLoad + "。");
            log("SECURELOAD 未修改；其他 TRUSTEDPATHS 未修改。");
        }

        private AutodeskProfileSummary ReadAutodeskProfileSummary()
        {
            var result = new AutodeskProfileSummary();
            var trustedEntry = TrustedPathEntry();
            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(AutoCadUserRoot, false))
                {
                    if (root == null) return result;
                    foreach (var releaseName in root.GetSubKeyNames().Where(IsTargetReleaseKey))
                    using (var release = root.OpenSubKey(releaseName, false))
                    {
                        if (release == null) continue;
                        foreach (var productName in release.GetSubKeyNames().Where(n => n.StartsWith("ACAD-", StringComparison.OrdinalIgnoreCase)))
                        using (var product = release.OpenSubKey(productName, false))
                        using (var profiles = product == null ? null : product.OpenSubKey("Profiles", false))
                        {
                            if (profiles == null) continue;
                            foreach (var profileName in profiles.GetSubKeyNames())
                            using (var profile = profiles.OpenSubKey(profileName, false))
                            using (var variables = profile == null ? null : profile.OpenSubKey("Variables", false))
                            {
                                if (variables == null) continue;
                                result.Found++;
                                if (ContainsTrustedPath(Convert.ToString(variables.GetValue("TRUSTEDPATHS", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames), CultureInfo.InvariantCulture), trustedEntry))
                                    result.Trusted++;
                                if (AppAutoLoadReady(variables.GetValue("APPAUTOLOAD", null, RegistryValueOptions.DoNotExpandEnvironmentNames)))
                                    result.AutoLoadReady++;
                            }
                        }
                    }
                }
            }
            catch
            {
                return result;
            }
            return result;
        }

        private void RemoveYangCadTrustedPath(Action<string> log)
        {
            var trustedEntry = TrustedPathEntry();
            var touched = 0;
            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(AutoCadUserRoot, false))
                {
                    if (root == null) return;
                    foreach (var releaseName in root.GetSubKeyNames().Where(IsTargetReleaseKey))
                    using (var release = root.OpenSubKey(releaseName, false))
                    {
                        if (release == null) continue;
                        foreach (var productName in release.GetSubKeyNames().Where(n => n.StartsWith("ACAD-", StringComparison.OrdinalIgnoreCase)))
                        using (var product = release.OpenSubKey(productName, false))
                        using (var profiles = product == null ? null : product.OpenSubKey("Profiles", false))
                        {
                            if (profiles == null) continue;
                            foreach (var profileName in profiles.GetSubKeyNames())
                            using (var profile = profiles.OpenSubKey(profileName, true))
                            using (var variables = profile == null ? null : profile.OpenSubKey("Variables", true))
                            {
                                if (variables == null) continue;
                                var raw = Convert.ToString(variables.GetValue("TRUSTEDPATHS", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames), CultureInfo.InvariantCulture);
                                var next = RemoveTrustedPath(raw, trustedEntry);
                                if (!string.Equals(raw ?? string.Empty, next, StringComparison.Ordinal))
                                {
                                    variables.SetValue("TRUSTEDPATHS", next, RegistryValueKind.ExpandString);
                                    touched++;
                                }
                            }
                        }
                    }
                }
                log("已从 " + touched + " 个 AutoCAD Profile 移除 YangCAD 专用 TRUSTEDPATHS；其他路径保留。");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("无法移除 TRUSTEDPATHS，可能被 CAD 管理策略锁定。", ex);
            }
        }

        private string TrustedPathEntry()
        {
            return _installBundle.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + @"\...";
        }

        private static bool IsSupportedReleaseKey(string value)
        {
            return SupportedReleaseKeys.Contains(value ?? string.Empty);
        }

        private bool IsTargetReleaseKey(string value)
        {
            if (_manifest == null || _manifest.years == null) return false;
            var key = value ?? string.Empty;
            return _manifest.years
                .Distinct()
                .Where(ReleaseKeyByYear.ContainsKey)
                .Select(year => ReleaseKeyByYear[year])
                .Any(target => string.Equals(target, key, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EnsureTrustedPath(RegistryKey variables, string trustedEntry)
        {
            var raw = Convert.ToString(variables.GetValue("TRUSTEDPATHS", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames), CultureInfo.InvariantCulture) ?? string.Empty;
            if (ContainsTrustedPath(raw, trustedEntry)) return false;
            var parts = SplitTrustedPaths(raw).ToList();
            parts.Add(QuoteTrustedPath(trustedEntry));
            variables.SetValue("TRUSTEDPATHS", string.Join(";", parts), RegistryValueKind.ExpandString);
            return true;
        }

        private static string RemoveTrustedPath(string raw, string trustedEntry)
        {
            var expected = NormalizeTrustedPath(trustedEntry);
            var kept = SplitTrustedPaths(raw).Where(p => !string.Equals(NormalizeTrustedPath(p), expected, StringComparison.OrdinalIgnoreCase)).ToArray();
            return string.Join(";", kept);
        }

        private static bool ContainsTrustedPath(string raw, string trustedEntry)
        {
            var expected = NormalizeTrustedPath(trustedEntry);
            return SplitTrustedPaths(raw).Any(p => string.Equals(NormalizeTrustedPath(p), expected, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SplitTrustedPaths(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;
            foreach (var piece in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = piece.Trim();
                if (trimmed.Length > 0) yield return trimmed;
            }
        }

        private static string QuoteTrustedPath(string path)
        {
            return (path ?? string.Empty).Trim().Trim('"');
        }

        private static string NormalizeTrustedPath(string path)
        {
            return (path ?? string.Empty)
                .Trim()
                .Trim('"')
                .Replace('/', '\\')
                .TrimEnd('\\')
                .ToUpperInvariant();
        }

        private static bool EnsureAppAutoLoad(RegistryKey variables)
        {
            var raw = variables.GetValue("APPAUTOLOAD", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            int current;
            if (!TryToInt32(raw, out current)) current = AppAutoLoadDefault;
            var next = current | AppAutoLoadStartupBit | AppAutoLoadDetectedBit;
            if (raw != null && current == next) return false;
            variables.SetValue("APPAUTOLOAD", next, RegistryValueKind.DWord);
            return true;
        }

        private static bool AppAutoLoadReady(object raw)
        {
            int value;
            if (!TryToInt32(raw, out value)) return false;
            return (value & AppAutoLoadStartupBit) != 0 && (value & AppAutoLoadDetectedBit) != 0;
        }

        private static bool TryToInt32(object value, out int result)
        {
            if (value is int)
            {
                result = (int)value;
                return true;
            }
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private void EnsurePublisherTrust(Action<string> log)
        {
            if (!File.Exists(_publisherCertificatePath))
                throw new FileNotFoundException("缺少公开发布者证书 " + PublisherCertificateName + "。", _publisherCertificatePath);
            var cert = new X509Certificate2(_publisherCertificatePath);
            if (_manifest.signing != null && !string.IsNullOrWhiteSpace(_manifest.signing.publisher_thumbprint))
            {
                var expected = NormalizeThumbprint(_manifest.signing.publisher_thumbprint);
                if (!string.Equals(NormalizeThumbprint(cert.Thumbprint), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("公开证书 Thumbprint 与 release manifest 不一致。");
            }
            AddCertificateIfMissing(StoreName.TrustedPublisher, cert);
            if (string.Equals(cert.Subject, cert.Issuer, StringComparison.OrdinalIgnoreCase))
                AddCertificateIfMissing(StoreName.Root, cert);
            log("Signed 模式：当前用户发布者信任已就绪：" + cert.Subject);
        }

        private void VerifySignedPayload(Action<string> log)
        {
            var cert = new X509Certificate2(_publisherCertificatePath);
            var expectedThumbprint = NormalizeThumbprint(cert.Thumbprint);
            var signed = _manifest.files
                .Where(f => f != null && (f.path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f.path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (signed.Length == 0) throw new InvalidDataException("release manifest 中没有签名 DLL/EXE。");

            foreach (var item in signed)
            {
                var path = SafePackagePath(item.path);
                string signerThumbprint;
                try
                {
                    var raw = X509Certificate.CreateFromSignedFile(path);
                    using (var signer = new X509Certificate2(raw)) signerThumbprint = NormalizeThumbprint(signer.Thumbprint);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("无法读取 Authenticode 发布者：" + item.path, ex);
                }
                if (!string.Equals(signerThumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("签名发布者不匹配：" + item.path);
                int trustCode;
                if (!WinTrust.IsTrusted(path, out trustCode))
                    throw new InvalidDataException("Authenticode 验证失败：" + item.path + " WinVerifyTrust=0x" + trustCode.ToString("X8", CultureInfo.InvariantCulture));
            }
            log("DLL/EXE Authenticode 签名验证通过：" + signed.Length + " 个文件。");
        }

        private void VerifyInstalledBundleMatches(Action<string> log)
        {
            VerifyDirectoryMatches(_sourceBundle, _installBundle);
            VerifyRequiredRuntimeAssets(_installBundle, "安装目录");
            log("安装目录逐文件 SHA 校验通过，MCP / ImageEmbed runtime 已就位。");
        }

        private static readonly string[] RequiredRuntimeAssets =
        {
            Path.Combine("Contents", "Mcp", "Start-YangCAD-Mcp.ps1"),
            Path.Combine("Contents", "Mcp", "pyproject.toml"),
            Path.Combine("Contents", "Mcp", "yangcad_mcp", "__init__.py"),
            Path.Combine("Contents", "Mcp", "yangcad_mcp", "batch.py"),
            Path.Combine("Contents", "Mcp", "yangcad_mcp", "extensions.py"),
            Path.Combine("Contents", "Mcp", "yangcad_mcp", "gateway.py"),
            Path.Combine("Contents", "Mcp", "yangcad_mcp", "server.py"),
            Path.Combine("Contents", "ImageEmbed", "Start-YangCAD-ImageEmbed.ps1")
        };

        private static bool RequiredRuntimeAssetsPresent(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath)) return false;
            return RequiredRuntimeAssets.All(relative => File.Exists(Path.Combine(bundlePath, relative)));
        }

        private static void VerifyRequiredRuntimeAssets(string bundlePath, string label)
        {
            if (!RequiredRuntimeAssetsPresent(bundlePath))
                throw new InvalidDataException(label + "缺少 YangCAD runtime：Contents\Mcp Python bridge 或 Contents\ImageEmbed worker 不完整。");
        }

        private static void VerifyDirectoryMatches(string source, string installed)
        {
            var sourceFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var installedFiles = Directory.GetFiles(installed, "*", SearchOption.AllDirectories);
            if (sourceFiles.Length != installedFiles.Length)
                throw new InvalidDataException("安装后文件数量不一致。");
            foreach (var sf in sourceFiles)
            {
                var rel = sf.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var df = Path.Combine(installed, rel);
                if (!File.Exists(df)) throw new FileNotFoundException("安装后缺少文件：" + rel, df);
                if (!string.Equals(Sha256(sf), Sha256(df), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("安装后 SHA 不一致：" + rel);
            }
        }

        private void AssertAutodeskHostsStopped()
        {
            if (_manifest == null || _manifest.years == null || _manifest.years.Length == 0)
                throw new InvalidOperationException("无法确定当前 Release 的目标 AutoCAD 年份；拒绝替换 Autodesk 插件文件。");

            var processes = new List<Process>();
            foreach (var process in Process.GetProcessesByName("acad").Concat(Process.GetProcessesByName("AcCoreConsole")))
            {
                var shouldBlock = true; // 进程路径无法识别时 fail-closed。
                try
                {
                    var path = process.MainModule != null ? process.MainModule.FileName : string.Empty;
                    var knownYear = DetectSupportedYear(path);
                    shouldBlock = !knownYear.HasValue || _manifest.years.Contains(knownYear.Value);
                }
                catch
                {
                    shouldBlock = true;
                }

                if (shouldBlock)
                    processes.Add(process);
                else
                    process.Dispose();
            }

            if (processes.Count > 0)
            {
                var pids = string.Join(", ", processes.Select(p => p.Id.ToString(CultureInfo.InvariantCulture)).ToArray());
                foreach (var process in processes) process.Dispose();
                throw new InvalidOperationException(
                    "检测到当前 Release 目标年份的 AutoCAD/Civil 3D/AcCoreConsole 正在运行，或无法安全识别宿主年份。" +
                    "为避免替换已加载 DLL，请先关闭目标测试宿主。非目标年份宿主不会被修改。PID: " + pids);
            }
        }

        private static int? DetectSupportedYear(string executablePath)
        {
            var path = executablePath ?? string.Empty;
            foreach (var year in ReleaseKeyByYear.Keys.OrderByDescending(y => y))
            {
                if (path.IndexOf(year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) >= 0)
                    return year;
            }
            return null;
        }

        private static string ActionName(SetupAction action)
        {
            switch (action)
            {
                case SetupAction.Install: return "安装";
                case SetupAction.Update: return "更新";
                case SetupAction.Repair: return "修复";
                case SetupAction.Uninstall: return "卸载";
                default: return action.ToString();
            }
        }

        private static string ReadBundleVersion(string bundlePath)
        {
            if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath)) return null;
            var xmlPath = Path.Combine(bundlePath, "PackageContents.xml");
            if (!File.Exists(xmlPath)) return null;
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(xmlPath);
            var root = doc.DocumentElement;
            return root == null ? null : root.GetAttribute("AppVersion");
        }

        private static Version ParseVersion(string value, string label)
        {
            Version version;
            if (!Version.TryParse(value, out version)) throw new InvalidDataException(label + "无效：" + value);
            return version;
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException("缺少 YangCADAgent.bundle：" + source);
            Directory.CreateDirectory(destination);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var rel = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, rel));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(destination, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void RemoveZoneIdentifiers(string root)
        {
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file + ":Zone.Identifier"); } catch { }
            }
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static void AddCertificateIfMissing(StoreName storeName, X509Certificate2 cert)
        {
            using (var store = new X509Store(storeName, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                if (store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count == 0)
                    store.Add(cert);
            }
        }

        private void RemovePublisherTrustIfPresent(Action<string> log)
        {
            if (!File.Exists(_publisherCertificatePath)) return;
            var cert = new X509Certificate2(_publisherCertificatePath);
            RemoveFromStore(StoreName.TrustedPublisher, cert.Thumbprint);
            if (string.Equals(cert.Subject, cert.Issuer, StringComparison.OrdinalIgnoreCase))
                RemoveFromStore(StoreName.Root, cert.Thumbprint);
            log("已移除当前用户 YangCAD 发布者证书信任：" + cert.Thumbprint);
        }

        private static void RemoveFromStore(StoreName storeName, string thumbprint)
        {
            using (var store = new X509Store(storeName, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                foreach (var match in matches) store.Remove(match);
            }
        }

        private static string NormalizeThumbprint(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        }

        private static void SafeDeleteDirectory(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
