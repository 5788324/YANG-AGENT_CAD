using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;

namespace YangCAD.AutoCADHost.Services.Cad
{
    /// <summary>
    /// Command aliases are native acad.pgp entries, NOT keyboard hooks.  We own one delimited
    /// block at the end of PGP; external definitions remain byte-equivalent apart from encoding
    /// normalisation when an actual edit is requested. No global keyboard interception.
    /// </summary>
    public static class ShortcutAliasService
    {
        private const string Begin = "; === YangCAD managed aliases BEGIN ===";
        private const string End = "; === YangCAD managed aliases END ===";
        private static readonly Regex AliasPattern = new Regex(
            @"^\s*([A-Za-z][A-Za-z0-9_]{0,15})\s*,\s*\*\s*([A-Za-z_][A-Za-z0-9_.-]*)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex AliasInput = new Regex(@"^[A-Za-z][A-Za-z0-9_]{0,15}$", RegexOptions.CultureInvariant);

        public sealed class Feature
        {
            public string Name { get; }
            public string Command { get; }
            public Feature(string name, string command) { Name = name; Command = command; }
            public override string ToString() => Name + "  (" + Command + ")";
        }

        public static IReadOnlyList<Feature> Features { get; } = new[]
        {
            new Feature("工具箱", "YANGCAD_TOOLBOX"),
            new Feature("工具箱快捷呼出", "YY"),
            new Feature("图层工具", "YGLAYER"),
            new Feature("合并图层", "YGLMERGE"),
            new Feature("图层开关", "YGLONOFF"),
            new Feature("图层冻结", "YGLFREEZE"),
            new Feature("图层锁定", "YGLLOCK"),
            new Feature("切割提取大样", "YGDETAIL"),
            new Feature("选改过文字的标注", "YGDIMTXT"),
            new Feature("标注坡度", "YGSLOPE"),
            new Feature("文字加矩形圆框", "YGTBOX"),
            new Feature("图纸比例转换", "YGSCALE"),
            new Feature("引线修复及关联文字", "YGLEADERFIX"),
            new Feature("智能双线/管道", "YGDL"),
            new Feature("修正双线宽度", "YGDLW"),
            new Feature("梁尺寸批量标注", "BMK"),
            new Feature("文字倒置翻正(梁)", "BMKFIX"),
            new Feature("柱截面批量标注", "CMK"),
            new Feature("点板格放受力符号", "SLB"),
            new Feature("板受力符号预览", "SLBP"),
            new Feature("板受力符号修正", "SLBFIX"),
            new Feature("设置板梁边界图层", "SLBLAYER"),
            new Feature("选尾数非0/5标注", "DIMCHK"),
            new Feature("标注尾数取整", "DIMFIX"),
            new Feature("标注恢复原值", "DIMRE"),
            new Feature("翻转标注文字", "DIMFZ"),
            new Feature("文字/属性保持向上", "TXTUP"),
            new Feature("文字按段落对齐", "TXAL"),
            new Feature("文字左对齐", "YGTL"),
            new Feature("文字居中对齐", "YGTC"),
            new Feature("文字右对齐", "YGTR"),
            new Feature("文字顶对齐", "YGTT"),
            new Feature("文字底对齐", "YGTB"),
            new Feature("文字中线对齐", "YGTV"),
            new Feature("文字按线对齐", "YGTLINE"),
            new Feature("文字按弧环绕", "YGTARC"),
            new Feature("递增复制", "YGINC"),
            new Feature("连接文字", "YGTJOIN"),
            new Feature("单行转多行", "YGTS2M"),
            new Feature("多行转单行", "YGTM2S"),
            new Feature("文字炸碎为单字", "YGTEXP"),
            new Feature("去空格/去换行", "YGTSPACE"),
            new Feature("大小写/驼峰切换", "YGTCASE"),
            new Feature("中英文标点切换", "YGTPUNC"),
            new Feature("文字加删前后缀", "YGTAFFIX"),
            new Feature("刷文字内容", "YGTBRUSH"),
            new Feature("文字内容互换", "YGTSWAP"),
            new Feature("文字改特性刷", "YGTPROP"),
            new Feature("文字加圆角跑道框", "YGTBOXSLOT"),
            new Feature("文字加遮罩外框", "YGTBOXMASK"),
            new Feature("文字排版参数设置", "YGTSET"),
            new Feature("剪裁填充图案", "YGHCLIP"),
            new Feature("拆分挖出填充(不删)", "YGHCLIPSPLIT"),
            new Feature("裁留框内填充", "YGHCLIPINT"),
            new Feature("拆分填充图案", "YGHSPLIT"),
            new Feature("合并填充图案", "YGHUNION"),
            new Feature("去除填充关联", "YGHDIS"),
            new Feature("重建填充边界", "YGHBOUND"),
            new Feature("重设填充基点", "YGHBASE"),
            new Feature("Revit 图纸预处理", "YGREVIT"),
            new Feature("当前图纸图片内嵌", "YGEMBED"),
            new Feature("文件夹批量图片内嵌", "YGEMBEDBATCH"),
            new Feature("MCP", "YANGCAD_MCP"),
            new Feature("控制中心", "YANGCAD"),
            new Feature("快捷键设置", "YGKEYS")
        };

        public static string FindActivePgpPath(Database database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var path = HostApplicationServices.Current.FindFile("acad.pgp", database, FindFileHint.Default);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("未找到当前 AutoCAD 配置使用的 acad.pgp，请检查 OPTIONS → 支持文件搜索路径。");
            return Path.GetFullPath(path);
        }

        public static string? CurrentAlias(string pgpPath, string command)
        {
            var data = ReadDocument(pgpPath);
            var managed = ReadManaged(data.Text);
            return managed.Where(x => string.Equals(x.Value, command, StringComparison.OrdinalIgnoreCase))
                          .Select(x => x.Key).FirstOrDefault();
        }

        public static string CollisionMessage(string pgpPath, string alias, string command)
        {
            alias = NormalizeAlias(alias);
            var data = ReadDocument(pgpPath);
            var outside = StripManaged(data.Text);
            var aliases = ParseAliases(outside);
            var managed = ReadManaged(data.Text);
            if (managed.TryGetValue(alias, out var owned) && !owned.Equals(command, StringComparison.OrdinalIgnoreCase))
                return "快捷键 " + alias + " 已分配给 YangCAD 的 " + owned + "，请先换一个字母组合。";
            if (aliases.TryGetValue(alias, out var target) && !target.Equals(command, StringComparison.OrdinalIgnoreCase))
                return "快捷键 " + alias + " 当前属于 CAD/其他插件命令 " + target + "，请选择其他快捷键。";
            return "";
        }

        public static string Save(string pgpPath, string command, string alias, bool allowExistingOverride = false)
        {
            if (!Features.Any(x => x.Command.Equals(command, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("不允许给未知命令生成快捷键。", nameof(command));
            alias = NormalizeAlias(alias);
            if (Features.Any(x => x.Command.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("不能使用已存在的 YangCAD 完整命令名作为快捷键。");
            var data = ReadDocument(pgpPath);
            var collision = CollisionMessage(pgpPath, alias, command);
            if (!string.IsNullOrEmpty(collision))
            {
                var owned = ReadManaged(data.Text);
                if (owned.TryGetValue(alias, out var ownedCommand) && !ownedCommand.Equals(command, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(collision);
                if (!allowExistingOverride) throw new InvalidOperationException(collision);
            }
            var aliases = ReadManaged(data.Text);
            foreach (var key in aliases.Where(x => x.Value.Equals(command, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToArray())
                aliases.Remove(key);
            aliases[alias] = command;
            ReplaceManaged(pgpPath, data, aliases);
            return alias;
        }

        public static bool Remove(string pgpPath, string command)
        {
            var data = ReadDocument(pgpPath);
            var aliases = ReadManaged(data.Text);
            var changed = false;
            foreach (var key in aliases.Where(x => x.Value.Equals(command, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToArray())
            { aliases.Remove(key); changed = true; }
            if (changed) ReplaceManaged(pgpPath, data, aliases);
            return changed;
        }

        private static string NormalizeAlias(string alias)
        {
            var value = (alias ?? "").Trim().ToUpperInvariant();
            if (!AliasInput.IsMatch(value))
                throw new ArgumentException("请使用 1～16 位英文字母、数字或下划线，首位必须是英文字母。Ctrl/Shift 组合键请使用 CUI 设置。");
            return value;
        }

        private static Dictionary<string, string> ParseAliases(string source)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in Regex.Split(source, @"\r\n|\n|\r"))
            {
                if (raw.TrimStart().StartsWith(";", StringComparison.Ordinal)) continue;
                var match = AliasPattern.Match(raw);
                if (match.Success) result[match.Groups[1].Value.ToUpperInvariant()] = match.Groups[2].Value.ToUpperInvariant();
            }
            return result;
        }

        private static Dictionary<string, string> ReadManaged(string source)
        {
            var start = source.IndexOf(Begin, StringComparison.Ordinal);
            var end = source.IndexOf(End, StringComparison.Ordinal);
            if (start < 0 && end < 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (start < 0 || end < start || source.IndexOf(Begin, start + Begin.Length, StringComparison.Ordinal) >= 0 ||
                source.IndexOf(End, end + End.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("检测到 YangCAD 快捷键区块不完整或重复。请手动检查 acad.pgp，避免覆盖其他配置。");
            return ParseAliases(source.Substring(start + Begin.Length, end - start - Begin.Length));
        }

        private static string StripManaged(string source)
        {
            var start = source.IndexOf(Begin, StringComparison.Ordinal);
            if (start < 0) return source;
            var end = source.IndexOf(End, start, StringComparison.Ordinal);
            if (end < 0) throw new InvalidDataException("YangCAD 快捷键区块缺少结束标记。");
            end += End.Length;
            // Leave surrounding user content entirely untouched.
            return source.Remove(start, end - start);
        }

        private sealed class PgpDocument
        {
            public string Text = "";
            public Encoding Encoding = new UTF8Encoding(false);
            public string Newline = "\r\n";
        }

        private static PgpDocument ReadDocument(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Encoding encoding;
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { encoding = new UTF8Encoding(true); offset = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = Encoding.Unicode; offset = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = Encoding.BigEndianUnicode; offset = 2; }
            else
            {
                try { new UTF8Encoding(false, true).GetString(bytes); encoding = new UTF8Encoding(false); }
                catch (DecoderFallbackException)
                {
                    // Byte-for-byte reversible fallback for legacy ANSI/GBK PGP files.
                    // .NET 8 Encoding.Default is UTF-8, which would corrupt Chinese comments.
                    // ISO-8859-1 maps each byte 1:1, preserving untouched content on write.
                    encoding = Encoding.GetEncoding(28591);
                }
            }
            var text = encoding.GetString(bytes, offset, bytes.Length - offset);
            return new PgpDocument { Text = text, Encoding = encoding, Newline = text.Contains("\r\n") ? "\r\n" : "\n" };
        }

        private static void ReplaceManaged(string path, PgpDocument data, Dictionary<string, string> aliases)
        {
            // Avoid modifying the active PGP when a read-only deployment profile is in use.
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                throw new UnauthorizedAccessException("当前 acad.pgp 是只读文件。请在 CAD 用户配置中使用可写的 PGP 文件。");
            var original = StripManaged(data.Text).TrimEnd('\r', '\n');
            var text = original + data.Newline;
            if (aliases.Count > 0)
            {
                text += data.Newline + Begin + data.Newline;
                foreach (var row in aliases.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    text += row.Key + ", *" + row.Value + data.Newline;
                text += End + data.Newline;
            }
            // Keep a recoverable copy of the user's original PGP on every change.
            var backup = path + ".yangcad." + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".bak";
            var temp = path + ".yangcad." + Guid.NewGuid().ToString("N") + ".tmp";
            File.Copy(path, backup, false);
            try
            {
                using (var writer = new StreamWriter(temp, false, data.Encoding)) writer.Write(text);
                // Atomic replacement when supported; original remains in the backup.
                File.Replace(temp, path, null);
            }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        }
    }
}
