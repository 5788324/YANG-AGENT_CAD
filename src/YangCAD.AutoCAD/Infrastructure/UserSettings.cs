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
        public string SlopeBasis { get; set; } = "Section";
        public string SlopeFormat { get; set; } = "Percent";
        public double SlopeTextHeightMm { get; set; } = 250;
        public double SlopeOffsetMm { get; set; } = 100;
        public string TextFrameShape { get; set; } = "Rectangle";
        public double TextFrameMarginMm { get; set; } = 50;
        public double DetailScaleFactor { get; set; } = 2.0;
        public string LastScaleMode { get; set; } = "Standard";
        public string LastOldScale { get; set; } = "1:5";
        public string LastNewScale { get; set; } = "1:10";

        // Dual-line drawing tools: real width in millimetres, persisted across drawings/sessions.
        public double DoubleLineWidthMm { get; set; } = 1000;
        public string DoubleLineMode { get; set; } = "Pipe";
        public string DoubleLineAlignment { get; set; } = "Center";
        public string DoubleLineCorner { get; set; } = "Miter";
        public double DoubleLineCenterExtensionMm { get; set; } = 0;

        // 柱尺寸标注 (CMK)
        public double ColTextHeight { get; set; } = 250.0;
        public double ColRoundStep { get; set; } = 1.0;
        public int ColFormat { get; set; } = 1;
        public double ColMinEdge { get; set; } = 100.0;
        public double ColMaxEdge { get; set; } = 3000.0;
        public string ColLayer { get; set; } = "";

        // 梁尺寸标注 (BMK)
        public double BeamTextHeight { get; set; } = 250.0;
        public double BeamRoundStep { get; set; } = 1.0;
        public double BeamMinWidth { get; set; } = 100.0;
        public double BeamMaxWidth { get; set; } = 1500.0;
        public double BeamMinOverlap { get; set; } = 500.0;
        public int BeamFormat { get; set; } = 1;
        public double BeamAngleTolDeg { get; set; } = 2.0;
        public double BeamWidthTol { get; set; } = 1.0;

        // 板受力符号 (SLB)
        public double SlabExtend { get; set; } = 600.0;
        public double SlabOneWayRatio { get; set; } = 3.0;
        public double SlabFactor { get; set; } = 0.23;
        public double SlabMinSize { get; set; } = 550.0;
        public double SlabMaxSize { get; set; } = 1400.0;
        public double SlabMinSpan { get; set; } = 800.0;
        public double SlabMaxSpan { get; set; } = 16000.0;

        // 标注检查取整与文字对齐 (DIMFIX / TXAL)
        public double DimRoundStep { get; set; } = 5.0;
        public double DimRoundTol { get; set; } = 1.0;
        public string DimCheckMode { get; set; } = "2";
        public string UpwardRef { get; set; } = "S";
        public double TextAlignSpacing { get; set; } = 1.5;

        // 贱人文字套件 (排版 / 递增 / 去空格换行 / 大小写 / 标点 / 前后缀)
        public double TextLineAlignOffset { get; set; } = 100.0;
        public double TextArcOffset { get; set; } = 0.0;
        public bool TextArcInward { get; set; } = true;
        public int TextIncStep { get; set; } = 1;
        public bool TextIncSkipZeroFloor { get; set; } = true;
        public string TextSpaceMode { get; set; } = "M";
        public string TextCaseMode { get; set; } = "U";
        public string TextPuncMode { get; set; } = "E";
        public string LastPrefix { get; set; } = "";
        public string LastSuffix { get; set; } = "";
        public string HatchClipMode { get; set; } = "S";
        public bool HatchBoundAssociative { get; set; } = false;

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
            if (SlopeBasis != "Section" && SlopeBasis != "Plan3D") SlopeBasis = "Section";
            if (SlopeFormat != "Percent" && SlopeFormat != "Degrees" &&
                SlopeFormat != "Ratio") SlopeFormat = "Percent";
            if (double.IsNaN(SlopeTextHeightMm) || double.IsInfinity(SlopeTextHeightMm) ||
                SlopeTextHeightMm <= 0 || SlopeTextHeightMm > 1e7) SlopeTextHeightMm = 250;
            if (double.IsNaN(SlopeOffsetMm) || double.IsInfinity(SlopeOffsetMm) ||
                SlopeOffsetMm < 0 || SlopeOffsetMm > 1e7) SlopeOffsetMm = 100;
            if (TextFrameShape != "Circle" && TextFrameShape != "Rectangle")
                TextFrameShape = "Rectangle";
            if (double.IsNaN(TextFrameMarginMm) || double.IsInfinity(TextFrameMarginMm) ||
                TextFrameMarginMm < 0 || TextFrameMarginMm > 1e7) TextFrameMarginMm = 50;
            if (double.IsNaN(DetailScaleFactor) || double.IsInfinity(DetailScaleFactor) ||
                DetailScaleFactor <= 0 || DetailScaleFactor > 1000) DetailScaleFactor = 1.0;
            if (LastScaleMode != "Standard" && LastScaleMode != "Scaled") LastScaleMode = "Standard";
            if (string.IsNullOrWhiteSpace(LastOldScale)) LastOldScale = "1:5";
            if (string.IsNullOrWhiteSpace(LastNewScale)) LastNewScale = "1:10";

            if (double.IsNaN(DoubleLineWidthMm) || double.IsInfinity(DoubleLineWidthMm) ||
                DoubleLineWidthMm <= 0 || DoubleLineWidthMm > 1e7)
                DoubleLineWidthMm = 1000;
            if (DoubleLineMode != "Pipe" && DoubleLineMode != "Double")
                DoubleLineMode = "Pipe";
            if (DoubleLineAlignment != "Center" && DoubleLineAlignment != "Left" &&
                DoubleLineAlignment != "Right") DoubleLineAlignment = "Center";
            if (DoubleLineCorner != "Miter" && DoubleLineCorner != "Bevel" &&
                DoubleLineCorner != "Round") DoubleLineCorner = "Miter";
            if (double.IsNaN(DoubleLineCenterExtensionMm) ||
                double.IsInfinity(DoubleLineCenterExtensionMm) ||
                DoubleLineCenterExtensionMm < 0 || DoubleLineCenterExtensionMm > 1e7)
                DoubleLineCenterExtensionMm = 0;

            ColTextHeight = ClampFinite(ColTextHeight, 1e-3, 1e7, 250.0);
            ColRoundStep = ClampFinite(ColRoundStep, 1e-3, 1e7, 1.0);
            if (ColFormat < 1 || ColFormat > 3) ColFormat = 1;
            ColMinEdge = ClampFinite(ColMinEdge, 1.0, 1e7, 100.0);
            ColMaxEdge = ClampFinite(ColMaxEdge, ColMinEdge, 1e7, 3000.0);
            ColLayer ??= "";

            BeamTextHeight = ClampFinite(BeamTextHeight, 1e-3, 1e7, 250.0);
            BeamRoundStep = ClampFinite(BeamRoundStep, 1e-3, 1e7, 1.0);
            BeamMinWidth = ClampFinite(BeamMinWidth, 1.0, 1e7, 100.0);
            BeamMaxWidth = ClampFinite(BeamMaxWidth, BeamMinWidth, 1e7, 1500.0);
            BeamMinOverlap = ClampFinite(BeamMinOverlap, 1.0, 1e7, 500.0);
            if (BeamFormat < 1 || BeamFormat > 2) BeamFormat = 1;
            BeamAngleTolDeg = ClampFinite(BeamAngleTolDeg, 0.01, 45.0, 2.0);
            BeamWidthTol = ClampFinite(BeamWidthTol, 0.0, 1e6, 1.0);

            SlabExtend = ClampFinite(SlabExtend, 0.0, 1e6, 600.0);
            SlabOneWayRatio = ClampFinite(SlabOneWayRatio, 1.0, 100.0, 3.0);
            SlabFactor = ClampFinite(SlabFactor, 0.05, 1.0, 0.23);
            SlabMinSize = ClampFinite(SlabMinSize, 1.0, 1e6, 550.0);
            SlabMaxSize = ClampFinite(SlabMaxSize, SlabMinSize, 1e6, 1400.0);
            SlabMinSpan = ClampFinite(SlabMinSpan, 10.0, 1e7, 800.0);
            SlabMaxSpan = ClampFinite(SlabMaxSpan, SlabMinSpan, 1e7, 16000.0);

            DimRoundStep = ClampFinite(DimRoundStep, 1e-6, 1e7, 5.0);
            DimRoundTol = ClampFinite(DimRoundTol, 0.0, 1e7, 1.0);
            if (DimCheckMode != "1" && DimCheckMode != "2") DimCheckMode = "2";
            if (UpwardRef != "S" && UpwardRef != "W") UpwardRef = "S";
            TextAlignSpacing = ClampFinite(TextAlignSpacing, 0.1, 20.0, 1.5);

            TextLineAlignOffset = ClampFinite(TextLineAlignOffset, -1e6, 1e6, 100.0);
            TextArcOffset = ClampFinite(TextArcOffset, -1e6, 1e6, 0.0);
            if (TextIncStep == 0 || TextIncStep < -1000 || TextIncStep > 1000) TextIncStep = 1;
            if (TextSpaceMode != "A" && TextSpaceMode != "M" && TextSpaceMode != "N" && TextSpaceMode != "L")
                TextSpaceMode = "M";
            if (TextCaseMode != "U" && TextCaseMode != "L" && TextCaseMode != "T" && TextCaseMode != "C")
                TextCaseMode = "U";
            if (TextPuncMode != "E" && TextPuncMode != "C" && TextPuncMode != "T")
                TextPuncMode = "E";
            HatchClipMode = (HatchClipMode ?? "S").Trim().ToUpperInvariant();
            if (HatchClipMode != "S" && HatchClipMode != "K" && HatchClipMode != "I")
                HatchClipMode = "S";
            LastPrefix ??= "";
            LastSuffix ??= "";

            ToolboxFavorites = NormalizeToolIds(ToolboxFavorites, 200);
            ToolboxRecent = NormalizeToolIds(ToolboxRecent, 10);
        }

        private static double ClampFinite(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max) return fallback;
            return value;
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
