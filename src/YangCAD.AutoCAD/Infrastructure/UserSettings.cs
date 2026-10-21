using System;
using System.IO;
using System.Collections.Generic;

namespace YangCAD.AutoCADHost.Infrastructure
{
    public sealed class UserSettings
    {
        public string Theme { get; set; } = "Obsidian";
        public bool AllowWrites { get; set; } = true;
        public bool AllowErase { get; set; } = false;
        public bool AllowCommandExecution { get; set; } = false;
        public bool AllowExtensionInvoke { get; set; } = false;
        public int RequestTimeoutSeconds { get; set; } = 30;
        public string[] ToolboxFavorites { get; set; } = Array.Empty<string>();
        public string[] ToolboxRecent { get; set; } = Array.Empty<string>();

        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YangCADAgent", "settings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new UserSettings();
                var settings = JsonCodec.Deserialize<UserSettings>(File.ReadAllText(SettingsPath));
                if (settings == null) throw new InvalidDataException("settings.json 为空或无效。");
                settings.Normalize();
                return settings;
            }
            catch
            {
                // An existing but unreadable/corrupt settings file must not silently re-enable writes.
                return new UserSettings { AllowWrites = false, AllowErase = false, AllowCommandExecution = false, AllowExtensionInvoke = false };
            }
        }

        public void Save()
        {
            Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonCodec.Serialize(this, indented: true);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            try
            {
                if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null);
                else File.Move(temp, SettingsPath);
            }
            catch
            {
                File.Copy(temp, SettingsPath, true);
                try { File.Delete(temp); } catch { }
            }
        }

        private void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Theme)) Theme = "Obsidian";
            if (!AllowWrites)
            {
                AllowErase = false;
                AllowCommandExecution = false;
                AllowExtensionInvoke = false;
            }
            if (RequestTimeoutSeconds < 5) RequestTimeoutSeconds = 5;
            if (RequestTimeoutSeconds > 300) RequestTimeoutSeconds = 300;
            ToolboxFavorites = NormalizeToolIds(ToolboxFavorites, 200);
            ToolboxRecent = NormalizeToolIds(ToolboxRecent, 10);
        }

        private static string[] NormalizeToolIds(string[]? ids, int limit)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in ids ?? Array.Empty<string>())
            {
                var id = (raw ?? "").Trim();
                if (id.Length == 0 || !seen.Add(id)) continue;
                result.Add(id);
                if (result.Count >= limit) break;
            }
            return result.ToArray();
        }
    }
}
