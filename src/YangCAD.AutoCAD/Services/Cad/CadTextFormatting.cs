using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace YangCAD.AutoCADHost.Services.Cad
{
    internal static class CadTextFormatting
    {
        private const string PercentCodes = @"%%(?:[%cCdDpPoOuU]|[0-9]{3})|%%";
        private static readonly Regex SingleLineCodes = new Regex(PercentCodes, RegexOptions.Compiled);
        private static readonly Regex FormattedCodes = new Regex(
            @"\\(?:[ACcFfHQSTWp][^;\\{}\r\n]*;|U\+[0-9A-Fa-f]{4}|[\\{}~PLlOoKkX])|[\\{}]|<>|" + PercentCodes,
            RegexOptions.Compiled);

        internal static bool TryTransform(string source, bool formatted, bool preserveParagraphs,
            Func<string, string> transform, out string result, bool allowInline = true)
        {
            result = source;
            if (source.IndexOf("%<", StringComparison.Ordinal) >= 0) return false;

            var codes = formatted ? FormattedCodes : SingleLineCodes;
            var tokens = codes.Matches(source).Cast<Match>().ToArray();
            if (tokens.All(token => token.Value == @"\P"))
            {
                result = formatted && preserveParagraphs
                    ? string.Join(@"\P", source.Split(new[] { @"\P" }, StringSplitOptions.None).Select(transform))
                    : transform(source);
                if (result.IndexOf("%<", StringComparison.Ordinal) < 0 &&
                    codes.Matches(result).Cast<Match>().All(token => token.Value == @"\P")) return true;
                result = source;
                return false;
            }

            // Operations which move or remove characters cannot be mapped back across inline controls.
            if (!preserveParagraphs || !allowInline || tokens.Any(token => token.Value == @"\" || token.Value == "%%"))
                return false;

            var visible = new StringBuilder(source.Length);
            var sourceIndices = new List<int>(source.Length);
            var position = 0;
            foreach (var token in tokens)
            {
                while (position < token.Index)
                {
                    visible.Append(source[position]);
                    sourceIndices.Add(position++);
                }

                var value = token.Value;
                if (value == @"\P" || value == @"\X")
                    AddProtected('\n');
                else if (value == @"\~")
                    AddProtected(' ');
                else if (value == "<>" || value.StartsWith(@"\S", StringComparison.Ordinal) ||
                         value.StartsWith(@"\U+", StringComparison.Ordinal) || value == @"\\" ||
                         value == @"\{" || value == @"\}" ||
                         (value.StartsWith("%%", StringComparison.Ordinal) &&
                          !"oOuU".Contains(value[value.Length - 1])))
                    AddProtected('\uFFFC');
                position = token.Index + token.Length;
            }
            while (position < source.Length)
            {
                visible.Append(source[position]);
                sourceIndices.Add(position++);
            }

            var changed = string.Join("\n", visible.ToString().Split('\n').Select(transform));
            if (changed.Length != sourceIndices.Count) return false;
            var output = source.ToCharArray();
            for (var i = 0; i < changed.Length; i++)
            {
                if (sourceIndices[i] >= 0) output[sourceIndices[i]] = changed[i];
            }

            var candidate = new string(output);
            var newTokens = codes.Matches(candidate).Cast<Match>().ToArray();
            if (candidate.IndexOf("%<", StringComparison.Ordinal) >= 0 || newTokens.Length != tokens.Length ||
                tokens.Where((token, i) => token.Index != newTokens[i].Index || token.Value != newTokens[i].Value).Any())
                return false;

            result = candidate;
            return true;

            void AddProtected(char value)
            {
                visible.Append(value);
                sourceIndices.Add(-1);
            }
        }
    }
}
