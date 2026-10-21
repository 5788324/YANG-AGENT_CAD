using System;
using System.Linq;
using System.Windows;

namespace YangCAD.AutoCADHost.UI
{
    public static class ThemeManager
    {
        public static readonly string[] Themes = { "Obsidian", "Porcelain", "Mist" };

        public static string Normalize(string? theme)
            => !string.IsNullOrWhiteSpace(theme) && Themes.Contains(theme, StringComparer.OrdinalIgnoreCase) ? Themes.First(x => string.Equals(x, theme, StringComparison.OrdinalIgnoreCase)) : "Obsidian";

        public static void Apply(ResourceDictionary target, string theme)
        {
            theme = Normalize(theme);
            var old = target.MergedDictionaries
                .Where(x => IsThemeDictionary(x.Source))
                .ToList();
            foreach (var dictionary in old) target.MergedDictionaries.Remove(dictionary);
            target.MergedDictionaries.Insert(0, new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/YangCAD.AutoCAD;component/UI/Themes/{theme}.xaml", UriKind.Absolute)
            });
        }

        private static bool IsThemeDictionary(Uri? source)
        {
            if (source == null) return false;
            var value = source.OriginalString.Replace('\\', '/');
            return Themes.Any(theme =>
                value.EndsWith($"/Themes/{theme}.xaml", StringComparison.OrdinalIgnoreCase) ||
                value.Equals($"Themes/{theme}.xaml", StringComparison.OrdinalIgnoreCase));
        }
    }
}
