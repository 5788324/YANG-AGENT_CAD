using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using YangCAD.AutoCADHost.Infrastructure;

namespace YangCAD.AutoCADHost.Services.Cad
{
    public static partial class JianrenTextService
    {
        private static readonly Dictionary<char, char> CnToEnPunctuation = new Dictionary<char, char>
        {
            ['，'] = ',',
            ['。'] = '.',
            ['（'] = '(',
            ['）'] = ')',
            ['【'] = '[',
            ['】'] = ']',
            ['：'] = ':',
            ['；'] = ';',
            ['？'] = '?',
            ['！'] = '!',
            ['“'] = '"',
            ['”'] = '"',
            ['‘'] = '\'',
            ['’'] = '\'',
            ['、'] = ',',
            ['《'] = '<',
            ['》'] = '>',
            ['～'] = '~',
            ['＠'] = '@',
            ['＃'] = '#',
            ['￥'] = '$',
            ['％'] = '%',
            ['＆'] = '&',
            ['＊'] = '*',
            ['＋'] = '+',
            ['－'] = '-',
            ['＝'] = '=',
            ['／'] = '/'
        };

        private static readonly Dictionary<char, char> EnToCnPunctuation = new Dictionary<char, char>
        {
            [','] = '，',
            ['.'] = '。',
            ['('] = '（',
            [')'] = '）',
            ['['] = '【',
            [']'] = '】',
            [':'] = '：',
            [';'] = '；',
            ['?'] = '？',
            ['!'] = '！',
            ['<'] = '《',
            ['>'] = '》'
        };

        public static void RunSpaceClean(Document doc, string? forcedMode = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            var mode = forcedMode;

            if (string.IsNullOrEmpty(mode))
            {
                var pko = new PromptKeywordOptions(
                    $"\n清理方式 [A-全去空格/M-去多余空格/N-去换行/L-去多余换行] <{settings.TextSpaceMode}>：",
                    "A M N L")
                { AllowNone = true };
                pko.Keywords.Default = settings.TextSpaceMode;
                var kr = ed.GetKeywords(pko);
                if (kr.Status == PromptStatus.Cancel) return;
                mode = string.IsNullOrEmpty(kr.StringResult) ? settings.TextSpaceMode : kr.StringResult.ToUpperInvariant();
                settings.TextSpaceMode = mode;
                settings.Save();
            }

            var updated = ApplyTextTransform(ed, doc.Database, "\n选择要清理空格/换行的文字或标注：", raw => CleanSpacesOrLines(raw, mode!));
            ed.WriteMessage($"\n[空格/换行清理:{mode}] 已更新 {updated} 个对象。");
        }

        private static string CleanSpacesOrLines(string input, string mode)
        {
            if (string.IsNullOrEmpty(input)) return input;
            switch (mode)
            {
                case "A": // 全去空格
                    return input.Replace(@"\~", "").Replace(" ", "").Replace("\u3000", "").Replace("\t", "");
                case "M": // 去多余空格（连续空白归为一格并修剪首尾）
                    var norm = input.Replace("\u3000", " ").Replace("\t", " ").Replace(@"\~", " ");
                    norm = Regex.Replace(norm, @"[ ]{2,}", " ");
                    return norm.Trim(' ');
                case "N": // 去换行
                    var s = input.Replace(@"\P", "\n").Replace(@"\p", "\n").Replace("\r\n", "\n");
                    var lines = s.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                    if (lines.Length == 0) return "";
                    var sb = new StringBuilder(lines[0]);
                    for (var i = 1; i < lines.Length; i++)
                    {
                        var prevLast = sb.Length > 0 ? sb[sb.Length - 1] : '\0';
                        var nextFirst = lines[i][0];
                        if (prevLast <= 127 && nextFirst <= 127 && char.IsLetterOrDigit(prevLast) && char.IsLetterOrDigit(nextFirst))
                            sb.Append(' ');
                        sb.Append(lines[i]);
                    }
                    return sb.ToString();
                case "L": // 去多余换行（多空行合并为单换行并去首尾空行）
                    var normalized = input.Replace("\r\n", "\n").Replace(@"\p", @"\P");
                    normalized = Regex.Replace(normalized, @"(\\P[ \t]*){2,}", @"\P");
                    normalized = Regex.Replace(normalized, @"\n[ \t]*\n+", "\n");
                    normalized = Regex.Replace(normalized, @"^(?:\\P|\n)+|(?:\\P|\n)+$", "");
                    return normalized;
                default:
                    return input;
            }
        }

        // ---------- 7. 大小写切换 (全大写 / 全小写 / 首字母大写 / 驼峰) ----------
        public static void RunCaseConvert(Document doc, string? forcedMode = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            var mode = forcedMode;

            if (string.IsNullOrEmpty(mode))
            {
                var pko = new PromptKeywordOptions(
                    $"\n大小写方式 [U-全大写/L-全小写/T-首字母大写/C-驼峰] <{settings.TextCaseMode}>：",
                    "U L T C")
                { AllowNone = true };
                pko.Keywords.Default = settings.TextCaseMode;
                var kr = ed.GetKeywords(pko);
                if (kr.Status == PromptStatus.Cancel) return;
                mode = string.IsNullOrEmpty(kr.StringResult) ? settings.TextCaseMode : kr.StringResult.ToUpperInvariant();
                settings.TextCaseMode = mode;
                settings.Save();
            }

            var updated = ApplyTextTransform(ed, doc.Database, "\n选择要切换大小写的文字或标注：", raw => ConvertCase(raw, mode!), preserveParagraphs: true, allowInline: mode != "C");
            ed.WriteMessage($"\n[大小写:{mode}] 已更新 {updated} 个对象。");
        }

        private static string ConvertCase(string input, string mode)
        {
            if (string.IsNullOrEmpty(input)) return input;
            switch (mode)
            {
                case "U":
                    return input.ToUpperInvariant();
                case "L":
                    return input.ToLowerInvariant();
                case "T":
                    return ToTitleCase(input);
                case "C":
                    return ToCamelCase(input);
                default:
                    return input;
            }
        }

        private static string ToTitleCase(string s)
        {
            var sb = new StringBuilder(s.Length);
            var newWord = true;
            foreach (var c in s)
            {
                if (c <= 127 && char.IsLetter(c))
                {
                    sb.Append(newWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    newWord = false;
                }
                else
                {
                    sb.Append(c);
                    newWord = !(c <= 127 && char.IsDigit(c));
                }
            }
            return sb.ToString();
        }

        private static string ToCamelCase(string s)
        {
            // Split ASCII identifier tokens across spaces/underscores/hyphens while preserving non-ASCII spans cleanly
            return Regex.Replace(s, @"[A-Za-z0-9_ \t-]+", m =>
            {
                var seg = m.Value;
                if (!seg.Any(char.IsLetter)) return seg;
                var parts = Regex.Split(seg, @"[_\-\s]+").Where(p => p.Length > 0).ToArray();
                if (parts.Length <= 1)
                {
                    var only = parts.Length == 1 ? parts[0] : seg;
                    return only.Length == 0 ? seg : char.ToLowerInvariant(only[0]) + only.Substring(1);
                }

                var sb = new StringBuilder();
                for (var i = 0; i < parts.Length; i++)
                {
                    var w = parts[i];
                    if (i == 0) sb.Append(w.ToLowerInvariant());
                    else sb.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1).ToLowerInvariant());
                }
                return sb.ToString();
            });
        }

        // ---------- 8. 中英文符号互转 ----------
        public static void RunPunctuationSwitch(Document doc, string? forcedMode = null)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();
            var mode = forcedMode;

            if (string.IsNullOrEmpty(mode))
            {
                var pko = new PromptKeywordOptions(
                    $"\n标点转换 [E-转为英文符号/C-转为中文符号/T-智能互转] <{settings.TextPuncMode}>：",
                    "E C T")
                { AllowNone = true };
                pko.Keywords.Default = settings.TextPuncMode;
                var kr = ed.GetKeywords(pko);
                if (kr.Status == PromptStatus.Cancel) return;
                mode = string.IsNullOrEmpty(kr.StringResult) ? settings.TextPuncMode : kr.StringResult.ToUpperInvariant();
                settings.TextPuncMode = mode;
                settings.Save();
            }

            var updated = ApplyTextTransform(ed, doc.Database, "\n选择要切换中英文标点的文字/属性/标注：", raw => SwitchPunctuation(raw, mode!), preserveParagraphs: true);
            ed.WriteMessage($"\n[中英标点:{mode}] 已更新 {updated} 个对象。");
        }

        private static string SwitchPunctuation(string input, string mode)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var effMode = mode;
            if (effMode == "T")
            {
                var cnCount = input.Count(c => CnToEnPunctuation.ContainsKey(c));
                var enCount = input.Count(c => EnToCnPunctuation.ContainsKey(c) || c == '"' || c == '\'');
                effMode = cnCount >= enCount ? "E" : "C";
            }

            if (effMode == "E")
            {
                var s = input.Replace("——", "--");
                var sb = new StringBuilder(s.Length);
                foreach (var c in s)
                    sb.Append(CnToEnPunctuation.TryGetValue(c, out var en) ? en : c);
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder(input.Length);
                var openDbl = true;
                var openSgl = true;
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    var prev = i > 0 ? input[i - 1] : '\0';
                    var next = i + 1 < input.Length ? input[i + 1] : '\0';

                    // Keep decimal points (3.14), scale ratios (1:100), negative numbers (-5) intact
                    if ((c == '.' || c == ':') && char.IsDigit(prev) && char.IsDigit(next))
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (c == '"')
                    {
                        sb.Append(openDbl ? '“' : '”');
                        openDbl = !openDbl;
                        continue;
                    }
                    if (c == '\'')
                    {
                        sb.Append(openSgl ? '‘' : '’');
                        openSgl = !openSgl;
                        continue;
                    }
                    sb.Append(EnToCnPunctuation.TryGetValue(c, out var cn) ? cn : c);
                }
                return sb.ToString();
            }
        }

        // ---------- 9. 前后缀批量增删 ----------
        public static void RunAffix(Document doc)
        {
            if (doc == null) return;
            var ed = doc.Editor;
            var settings = UserSettings.Load();

            var pko = new PromptKeywordOptions("\n操作类型 [P-加前缀/S-加后缀/RP-删前缀/RS-删后缀] <P>：", "P S RP RS") { AllowNone = true };
            pko.Keywords.Default = "P";
            var kr = ed.GetKeywords(pko);
            if (kr.Status == PromptStatus.Cancel) return;
            var op = string.IsNullOrEmpty(kr.StringResult) ? "P" : kr.StringResult.ToUpperInvariant();

            var isPrefix = op == "P" || op == "RP";
            var defStr = isPrefix ? settings.LastPrefix : settings.LastSuffix;
            var pso = new PromptStringOptions($"\n输入要{(op.StartsWith("R") ? "删除" : "添加")}的{(isPrefix ? "前缀" : "后缀")} <{defStr}>：")
            {
                AllowSpaces = true,
                DefaultValue = defStr,
                UseDefaultValue = !string.IsNullOrEmpty(defStr)
            };
            var pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK) return;
            var token = string.IsNullOrEmpty(pr.StringResult) ? defStr : pr.StringResult;
            if (string.IsNullOrEmpty(token)) return;

            if (isPrefix) settings.LastPrefix = token;
            else settings.LastSuffix = token;
            settings.Save();

            var updated = ApplyTextTransform(ed, doc.Database, "\n选择需要批量修改的文字/标注/属性：", raw =>
            {
                if (op == "P") return token + raw;
                if (op == "S") return raw + token;
                if (op == "RP") return raw.StartsWith(token, StringComparison.Ordinal) ? raw.Substring(token.Length) : raw;
                return raw.EndsWith(token, StringComparison.Ordinal) ? raw.Substring(0, raw.Length - token.Length) : raw;
            });

            ed.WriteMessage($"\n[YGTAFFIX] 已修改 {updated} 个对象。");
        }

        private static int ApplyTextTransform(Editor ed, Database db, string prompt, Func<string, string> transform,
            bool preserveParagraphs = false, bool allowInline = true)
        {
            var ids = GetOrPromptTextEntities(ed, "TEXT,MTEXT,DIMENSION,MULTILEADER,INSERT", prompt);
            if (ids == null || ids.Length == 0) return 0;

            var count = 0;
            var skippedFormatting = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent)) continue;
                    if (IsLayerLocked(tr, lt, ent.Layer)) continue;

                    if (ent is BlockReference br)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att && !IsOnLockedLayer(tr, att))
                                TransformEntity(att);
                        }
                    }
                    else
                    {
                        TransformEntity(ent);
                    }
                }
                tr.Commit();
            }
            if (skippedFormatting > 0)
                ed.WriteMessage($"\n已跳过 {skippedFormatting} 个含字段或当前模式无法保留格式的文字。");
            return count;

            void TransformEntity(Entity entity)
            {
                using var embedded = entity switch
                {
                    AttributeReference attribute when attribute.IsMTextAttribute => attribute.MTextAttribute,
                    MLeader leader when leader.ContentType == ContentType.MTextContent => leader.MText,
                    _ => null
                };
                var source = embedded != null ? embedded.Contents : ReadAnyTextContentForTransform(entity);
                if (source == null) return;
                if (entity.HasFields || (embedded?.HasFields ?? false) ||
                    !CadTextFormatting.TryTransform(source, embedded != null || entity is MText || entity is Dimension,
                        preserveParagraphs, transform, out var result, allowInline))
                {
                    skippedFormatting++;
                    return;
                }
                if (string.Equals(source, result, StringComparison.Ordinal)) return;

                entity.UpgradeOpen();
                switch (entity)
                {
                    case AttributeReference attribute when embedded != null:
                        embedded.Contents = result;
                        attribute.MTextAttribute = embedded;
                        break;
                    case MLeader leader when embedded != null:
                        embedded.Contents = result;
                        leader.MText = embedded;
                        break;
                    case MText multi:
                        multi.Contents = result;
                        break;
                    case Dimension dimension:
                        dimension.DimensionText = result;
                        break;
                    case DBText single:
                        single.TextString = result;
                        break;
                }
                count++;
            }

            string? ReadAnyTextContentForTransform(Entity entity) => entity switch
            {
                DBText single => single.TextString,
                MText multi => multi.Contents,
                Dimension dim when !string.IsNullOrEmpty(dim.DimensionText) && dim.DimensionText != "<>" => dim.DimensionText,
                _ => null
            };
        }

    }
}
