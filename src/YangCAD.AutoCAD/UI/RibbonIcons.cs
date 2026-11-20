using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YangCAD.AutoCADHost.UI
{
    /// <summary>
    /// Renders AutoCAD ribbon vector icons to frozen bitmaps at 16x16 and 32x32.
    /// </summary>
    public static class RibbonIcons
    {
        private static readonly Color WhiteColor = Color.FromRgb(0xE6, 0xED, 0xF3);
        private static readonly Color MutedColor = Color.FromRgb(0x94, 0xA3, 0xB8);
        private static readonly Color CyanColor = Color.FromRgb(0x38, 0xBD, 0xF8);
        private static readonly Color CyanSoftColor = Color.FromArgb(0x55, 0x38, 0xBD, 0xF8);
        private static readonly Color AmberColor = Color.FromRgb(0xFB, 0xBF, 0x24);
        private static readonly Color AmberSoftColor = Color.FromArgb(0x44, 0xFB, 0xBF, 0x24);
        private static readonly Color GreenColor = Color.FromRgb(0x4A, 0xDE, 0x80);
        private static readonly Color GreenSoftColor = Color.FromArgb(0x44, 0x4A, 0xDE, 0x80);

        internal static bool UseLightTheme { get; set; }
        private static readonly SolidColorBrush DarkOutlineBrush = FreezeBrush(Color.FromRgb(0x24, 0x33, 0x47));
        private static readonly SolidColorBrush LightOutlineBrush = FreezeBrush(WhiteColor);
        private static SolidColorBrush WhiteBrush => UseLightTheme ? DarkOutlineBrush : LightOutlineBrush;
        private static readonly SolidColorBrush MutedBrush = FreezeBrush(MutedColor);
        private static readonly SolidColorBrush CyanBrush = FreezeBrush(CyanColor);
        private static readonly SolidColorBrush CyanSoftBrush = FreezeBrush(CyanSoftColor);
        private static readonly SolidColorBrush AmberBrush = FreezeBrush(AmberColor);
        private static readonly SolidColorBrush AmberSoftBrush = FreezeBrush(AmberSoftColor);
        private static readonly SolidColorBrush GreenBrush = FreezeBrush(GreenColor);
        private static readonly SolidColorBrush GreenSoftBrush = FreezeBrush(GreenSoftColor);

        private static readonly Dictionary<string, string> CommandToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["YANGCAD_TOOLBOX"] = "toolbox_grid",
            ["YY"] = "toolbox_grid",
            ["YGSCALE"] = "scale_convert",
            ["YGDL"] = "double_line",
            ["BMK"] = "beam_mark",
            ["BMKFIX"] = "text_up",
            ["CMK"] = "col_mark",
            ["SLB"] = "slab_sym",
            ["SLBP"] = "slab_sym",
            ["SLBFIX"] = "slab_fix",
            ["SLBLAYER"] = "slab_layer",
            ["DIMCHK"] = "dim_check",
            ["DIMFIX"] = "dim_fix",
            ["DIMRE"] = "dim_restore",
            ["DIMFZ"] = "dim_flip",
            ["TXTUP"] = "text_up",
            ["TXAL"] = "text_align",
            ["YGLAYER"] = "layer_panel",
            ["YGLONOFF"] = "layer_off",
            ["YGLMERGE"] = "layer_merge",
            ["YGLFREEZE"] = "layer_freeze",
            ["YGLLOCK"] = "layer_lock",
            ["YGDETAIL"] = "detail_extract",
            ["YGLEADERFIX"] = "leader_fix",
            ["YGDIMTXT"] = "dim_override",
            ["YGSLOPE"] = "slope_mark",
            ["YGTBOX"] = "text_frame",
            ["YGREVIT"] = "revit_prep",
            ["YGEMBED"] = "image_embed",
            ["YGEMBEDBATCH"] = "folder_embed",
            ["YGKEYS"] = "shortcut_keys",
            ["YGTL"] = "text_align_lr",
            ["YGTC"] = "text_align_lr",
            ["YGTR"] = "text_align_lr",
            ["YGTT"] = "text_align_lr",
            ["YGTB"] = "text_align_lr",
            ["YGTV"] = "text_align_lr",
            ["YGTLINE"] = "text_align_lr",
            ["YGTARC"] = "text_arc",
            ["YGINC"] = "text_increment",
            ["YGTJOIN"] = "text_join",
            ["YGTS2M"] = "text_join",
            ["YGTM2S"] = "text_join",
            ["YGTEXP"] = "text_join",
            ["YGTSPACE"] = "text_space",
            ["YGTSPACEM"] = "text_space",
            ["YGTNOLINE"] = "text_space",
            ["YGTTRIMLINE"] = "text_space",
            ["YGTCASE"] = "text_case",
            ["YGTUPPER"] = "text_case",
            ["YGTLOWER"] = "text_case",
            ["YGTTITLE"] = "text_case",
            ["YGTCAMEL"] = "text_case",
            ["YGTPUNC"] = "text_punc",
            ["YGTPUNCE"] = "text_punc",
            ["YGTPUNCC"] = "text_punc",
            ["YGTAFFIX"] = "text_affix",
            ["YGTBRUSH"] = "text_brush",
            ["YGTSWAP"] = "text_swap",
            ["YGTPROP"] = "text_prop",
            ["YGTBOXSLOT"] = "text_box_slot",
            ["YGTBOXMASK"] = "text_box_mask",
            ["YGTSET"] = "text_settings",
            ["YGHCLIP"] = "hatch_clip",
            ["HHJ"] = "hatch_clip",
            ["YGHCLIPSPLIT"] = "hatch_clip_split",
            ["HHJK"] = "hatch_clip_split",
            ["YGHCLIPINT"] = "hatch_clip",
            ["HHJI"] = "hatch_clip",
            ["YGHSPLIT"] = "hatch_split",
            ["HHX"] = "hatch_split",
            ["YGHUNION"] = "hatch_union",
            ["HHU"] = "hatch_union",
            ["YGHDIS"] = "hatch_disasso",
            ["HHGL"] = "hatch_disasso",
            ["YGHBOUND"] = "hatch_bound",
            ["HHB"] = "hatch_bound",
            ["YGHBASE"] = "hatch_base",
            ["HHI"] = "hatch_base",
            ["YANGCAD"] = "control_center",
            ["YANGCAD_MCP"] = "mcp_core"
        };

        public static ImageSource? Create(string keyOrCommand, int size)
        {
            if (size <= 0) size = 32;
            var key = NormalizeKey(keyOrCommand);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var scale = size / 32.0;
                dc.PushTransform(new ScaleTransform(scale, scale));
                var strokeWidth = size <= 16 ? 2.2 : 1.75;
                var whitePen = FreezePen(WhiteBrush, strokeWidth);
                var cyanPen = FreezePen(CyanBrush, strokeWidth);
                var amberPen = FreezePen(AmberBrush, strokeWidth);
                var greenPen = FreezePen(GreenBrush, strokeWidth);
                var mutedPen = FreezePen(MutedBrush, strokeWidth * 0.9);
                DrawIcon(dc, key, whitePen, cyanPen, amberPen, greenPen, mutedPen);
                dc.Pop();
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }

        private static string NormalizeKey(string keyOrCommand)
        {
            var token = (keyOrCommand ?? "").Trim();
            if (CommandToKey.TryGetValue(token, out var mapped)) return mapped;
            return token.ToLowerInvariant();
        }

        private static void DrawIcon(DrawingContext dc, string key, Pen white, Pen cyan, Pen amber, Pen green, Pen muted)
        {
            switch (key)
            {
                case "layer_panel":
                    DrawDiamond(dc, 16, 9, 11, 4.5, CyanSoftBrush, cyan);
                    DrawDiamond(dc, 16, 16, 11, 4.5, null, white);
                    DrawDiamond(dc, 16, 23, 11, 4.5, null, muted);
                    break;

                case "layer_off":
                    // Lightbulb
                    dc.DrawEllipse(AmberSoftBrush, amber, new Point(16, 13), 6.5, 6.5);
                    dc.DrawLine(amber, new Point(13, 18.5), new Point(19, 18.5));
                    dc.DrawLine(white, new Point(13.5, 22), new Point(18.5, 22));
                    dc.DrawLine(white, new Point(14.5, 25.5), new Point(17.5, 25.5));
                    dc.DrawLine(amber, new Point(16, 3), new Point(16, 5));
                    dc.DrawLine(amber, new Point(6, 13), new Point(8, 13));
                    dc.DrawLine(amber, new Point(24, 13), new Point(26, 13));
                    break;

                case "layer_merge":
                    // Two lines converging to one cyan layer
                    dc.DrawLine(muted, new Point(4, 9), new Point(14, 9));
                    dc.DrawLine(muted, new Point(4, 23), new Point(14, 23));
                    dc.DrawLine(white, new Point(14, 9), new Point(18, 16));
                    dc.DrawLine(white, new Point(14, 23), new Point(18, 16));
                    dc.DrawLine(cyan, new Point(18, 16), new Point(28, 16));
                    dc.DrawLine(cyan, new Point(24, 12), new Point(28, 16));
                    dc.DrawLine(cyan, new Point(24, 20), new Point(28, 16));
                    break;

                case "layer_freeze":
                    // Snowflake
                    for (var i = 0; i < 3; i++)
                    {
                        var rad = i * Math.PI / 3.0;
                        var dx = Math.Cos(rad) * 10.5;
                        var dy = Math.Sin(rad) * 10.5;
                        dc.DrawLine(cyan, new Point(16 - dx, 16 - dy), new Point(16 + dx, 16 + dy));
                    }
                    dc.DrawEllipse(CyanSoftBrush, white, new Point(16, 16), 3.2, 3.2);
                    break;

                case "layer_lock":
                    // Padlock
                    dc.DrawGeometry(null, white, Geometry.Parse("M 11,14 L 11,10 A 5,5 0 0 1 21,10 L 21,14"));
                    dc.DrawRoundedRectangle(AmberSoftBrush, amber, new Rect(8, 14, 16, 12), 2.5, 2.5);
                    dc.DrawEllipse(AmberBrush, null, new Point(16, 20), 1.8, 1.8);
                    break;

                case "detail_extract":
                    // Dashed crop frame + magnifier
                    var dash = cyan.Clone();
                    dash.DashStyle = new DashStyle(new double[] { 2.5, 2.0 }, 0);
                    dash.Freeze();
                    dc.DrawRectangle(CyanSoftBrush, dash, new Rect(4, 5, 17, 17));
                    dc.DrawEllipse(null, white, new Point(20, 20), 5.5, 5.5);
                    dc.DrawLine(amber, new Point(24, 24), new Point(28.5, 28.5));
                    break;

                case "scale_convert":
                    // Small rect -> Large rect + diagonal arrow
                    dc.DrawRectangle(null, muted, new Rect(4, 16, 10, 10));
                    dc.DrawRectangle(CyanSoftBrush, cyan, new Rect(12, 5, 16, 16));
                    dc.DrawLine(amber, new Point(10, 20), new Point(22, 10));
                    dc.DrawLine(amber, new Point(17.5, 10), new Point(22, 10));
                    dc.DrawLine(amber, new Point(22, 10), new Point(22, 14.5));
                    break;

                case "leader_fix":
                    // Leader line with arrow pointing to text A
                    dc.DrawLine(cyan, new Point(5, 24), new Point(13, 11));
                    dc.DrawLine(cyan, new Point(13, 11), new Point(20, 11));
                    dc.DrawLine(cyan, new Point(5, 24), new Point(5.5, 18.5));
                    dc.DrawLine(cyan, new Point(5, 24), new Point(10, 22));
                    DrawGlyph(dc, "A", 21, 5, 14, WhiteBrush);
                    break;

                case "dim_override":
                    // Dimension ticks + highlighted text badge
                    dc.DrawLine(muted, new Point(5, 7), new Point(5, 22));
                    dc.DrawLine(muted, new Point(27, 7), new Point(27, 22));
                    dc.DrawLine(white, new Point(5, 18), new Point(27, 18));
                    dc.DrawRoundedRectangle(AmberSoftBrush, amber, new Rect(9, 6, 14, 10), 2, 2);
                    DrawGlyph(dc, "!", 14, 5.5, 10, AmberBrush);
                    break;

                case "slope_mark":
                    // Slope triangle + arrow
                    dc.DrawLine(white, new Point(5, 24), new Point(27, 24));
                    dc.DrawLine(white, new Point(27, 24), new Point(27, 11));
                    dc.DrawLine(cyan, new Point(5, 24), new Point(27, 11));
                    dc.DrawLine(amber, new Point(10, 17), new Point(22, 10));
                    dc.DrawLine(amber, new Point(18, 9.5), new Point(22, 10));
                    dc.DrawLine(amber, new Point(22, 10), new Point(20.5, 13.5));
                    break;

                case "text_frame":
                    // Rounded box around letter A
                    dc.DrawRoundedRectangle(CyanSoftBrush, cyan, new Rect(4, 6, 24, 20), 3, 3);
                    DrawGlyph(dc, "A", 11.5, 7.5, 15, WhiteBrush);
                    break;

                case "double_line":
                    // Parallel bent pipe + amber centerline
                    dc.DrawLine(cyan, new Point(5, 8), new Point(22, 8));
                    dc.DrawLine(cyan, new Point(22, 8), new Point(22, 26));
                    dc.DrawLine(white, new Point(5, 18), new Point(12, 18));
                    dc.DrawLine(white, new Point(12, 18), new Point(12, 26));
                    dc.DrawLine(amber, new Point(5, 13), new Point(17, 13));
                    dc.DrawLine(amber, new Point(17, 13), new Point(17, 26));
                    break;

                case "beam_mark":
                    // Two horizontal beam lines + B label
                    dc.DrawLine(cyan, new Point(4, 15), new Point(28, 15));
                    dc.DrawLine(cyan, new Point(4, 23), new Point(28, 23));
                    DrawGlyph(dc, "B", 11, 1.5, 12, AmberBrush);
                    break;

                case "col_mark":
                    // Solid-accent column square + C label above
                    dc.DrawRectangle(CyanSoftBrush, cyan, new Rect(9, 14, 14, 13));
                    DrawGlyph(dc, "C", 11.5, 1, 12, AmberBrush);
                    break;

                case "slab_sym":
                    // Slab frame + two-way cross arrow
                    dc.DrawRectangle(null, muted, new Rect(4, 4, 24, 24));
                    dc.DrawLine(cyan, new Point(8, 16), new Point(24, 16));
                    dc.DrawLine(cyan, new Point(16, 8), new Point(16, 24));
                    dc.DrawEllipse(AmberBrush, null, new Point(16, 16), 2.0, 2.0);
                    break;

                case "slab_fix":
                    dc.DrawRectangle(null, muted, new Rect(4, 4, 24, 24));
                    dc.DrawLine(green, new Point(9, 16), new Point(23, 16));
                    dc.DrawLine(green, new Point(16, 9), new Point(16, 23));
                    dc.DrawEllipse(GreenSoftBrush, green, new Point(16, 16), 4.5, 4.5);
                    break;

                case "slab_layer":
                    DrawDiamond(dc, 16, 10, 10, 4, CyanSoftBrush, cyan);
                    dc.DrawLine(amber, new Point(7, 20), new Point(25, 20));
                    dc.DrawLine(amber, new Point(7, 25), new Point(25, 25));
                    break;

                case "dim_check":
                    dc.DrawLine(white, new Point(5, 21), new Point(27, 21));
                    dc.DrawRoundedRectangle(AmberSoftBrush, amber, new Rect(8, 6, 16, 11), 2, 2);
                    DrawGlyph(dc, "?", 13, 5.5, 10, AmberBrush);
                    break;

                case "dim_fix":
                    dc.DrawLine(white, new Point(5, 22), new Point(27, 22));
                    dc.DrawRoundedRectangle(GreenSoftBrush, green, new Rect(6, 6, 20, 12), 2, 2);
                    DrawGlyph(dc, "5", 12.5, 5.5, 11, GreenBrush);
                    break;

                case "dim_restore":
                    dc.DrawLine(muted, new Point(6, 22), new Point(26, 22));
                    dc.DrawGeometry(null, cyan, Geometry.Parse("M 9,14 A 7,7 0 1 1 22,17"));
                    dc.DrawLine(cyan, new Point(9, 14), new Point(9, 9));
                    dc.DrawLine(cyan, new Point(9, 14), new Point(14, 14));
                    break;

                case "dim_flip":
                    dc.DrawLine(white, new Point(5, 16), new Point(27, 16));
                    dc.DrawLine(amber, new Point(16, 6), new Point(16, 26));
                    dc.DrawLine(cyan, new Point(12, 10), new Point(16, 6));
                    dc.DrawLine(cyan, new Point(20, 10), new Point(16, 6));
                    dc.DrawLine(cyan, new Point(12, 22), new Point(16, 26));
                    dc.DrawLine(cyan, new Point(20, 22), new Point(16, 26));
                    break;

                case "text_up":
                    DrawGlyph(dc, "A", 8, 9, 14, WhiteBrush);
                    dc.DrawLine(green, new Point(24, 24), new Point(24, 8));
                    dc.DrawLine(green, new Point(20.5, 12), new Point(24, 8));
                    dc.DrawLine(green, new Point(27.5, 12), new Point(24, 8));
                    break;

                case "text_align":
                    dc.DrawLine(cyan, new Point(6, 6), new Point(6, 26));
                    dc.DrawLine(white, new Point(10, 9), new Point(26, 9));
                    dc.DrawLine(amber, new Point(10, 16), new Point(22, 16));
                    dc.DrawLine(white, new Point(10, 23), new Point(27, 23));
                    break;

                case "revit_prep":
                    // Isometric cube with R accent
                    dc.DrawGeometry(CyanSoftBrush, cyan, Geometry.Parse("M 16,4 L 26,9.5 L 26,22 L 16,27.5 L 6,22 L 6,9.5 Z"));
                    dc.DrawLine(white, new Point(6, 9.5), new Point(16, 15));
                    dc.DrawLine(white, new Point(26, 9.5), new Point(16, 15));
                    dc.DrawLine(white, new Point(16, 15), new Point(16, 27.5));
                    break;

                case "image_embed":
                    // Photo frame with mountain + down arrow
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(5, 6, 22, 19));
                    dc.DrawGeometry(null, cyan, Geometry.Parse("M 7,22 L 13,14 L 18,19 L 21,16 L 25,22"));
                    dc.DrawEllipse(AmberBrush, null, new Point(21, 11), 2, 2);
                    break;

                case "folder_embed":
                    // Folder silhouette + green badge
                    dc.DrawGeometry(AmberSoftBrush, amber, Geometry.Parse("M 4,8 L 13,8 L 15,11 L 28,11 L 28,25 L 4,25 Z"));
                    dc.DrawEllipse(GreenSoftBrush, green, new Point(22, 21), 5, 5);
                    dc.DrawLine(green, new Point(22, 18.5), new Point(22, 23.5));
                    dc.DrawLine(green, new Point(19.5, 21), new Point(24.5, 21));
                    break;

                case "toolbox_grid":
                    // 2x2 neat grid
                    dc.DrawRoundedRectangle(CyanSoftBrush, cyan, new Rect(5, 5, 9.5, 9.5), 2, 2);
                    dc.DrawRoundedRectangle(null, white, new Rect(17.5, 5, 9.5, 9.5), 2, 2);
                    dc.DrawRoundedRectangle(null, white, new Rect(5, 17.5, 9.5, 9.5), 2, 2);
                    dc.DrawRoundedRectangle(AmberSoftBrush, amber, new Rect(17.5, 17.5, 9.5, 9.5), 2, 2);
                    break;

                case "shortcut_keys":
                    // Keycap with lightning/K
                    dc.DrawRoundedRectangle(CyanSoftBrush, white, new Rect(4, 7, 24, 18), 3.5, 3.5);
                    DrawGlyph(dc, "K", 11.5, 8, 14, CyanBrush);
                    break;

                case "text_align_lr":
                    // Vertical datum guide + horizontal lines snapped cleanly
                    dc.DrawLine(cyan, new Point(6, 5), new Point(6, 27));
                    dc.DrawLine(white, new Point(9, 9), new Point(25, 9));
                    dc.DrawLine(white, new Point(9, 16), new Point(21, 16));
                    dc.DrawLine(amber, new Point(9, 23), new Point(26, 23));
                    break;

                case "text_arc":
                    // Curved guide + "T" nodes along arc
                    dc.DrawEllipse(null, cyan, new Point(16, 28), 13, 13);
                    dc.DrawRoundedRectangle(AmberSoftBrush, white, new Rect(5, 8, 6, 5), 1, 1);
                    dc.DrawRoundedRectangle(AmberSoftBrush, white, new Rect(13, 4, 6, 5), 1, 1);
                    dc.DrawRoundedRectangle(AmberSoftBrush, white, new Rect(21, 8, 6, 5), 1, 1);
                    break;

                case "text_increment":
                    // "1 -> 2" incrementing step badge
                    DrawGlyph(dc, "1", 5, 4, 12, WhiteBrush);
                    dc.DrawLine(cyan, new Point(12, 16), new Point(19, 16));
                    dc.DrawLine(cyan, new Point(16.5, 13), new Point(19.5, 16));
                    dc.DrawLine(cyan, new Point(16.5, 19), new Point(19.5, 16));
                    DrawGlyph(dc, "2", 20, 13, 13, GreenBrush);
                    break;

                case "text_join":
                    // Two text blocks merging into one
                    dc.DrawRoundedRectangle(CyanSoftBrush, white, new Rect(4, 6, 10, 8), 1.5, 1.5);
                    dc.DrawRoundedRectangle(CyanSoftBrush, white, new Rect(18, 6, 10, 8), 1.5, 1.5);
                    dc.DrawLine(amber, new Point(11, 10), new Point(21, 10));
                    dc.DrawRoundedRectangle(GreenSoftBrush, green, new Rect(5, 19, 22, 7), 2, 2);
                    break;

                case "text_space":
                    // Broom / compress brackets removing whitespace & blank lines
                    dc.DrawLine(white, new Point(5, 9), new Point(11, 9));
                    dc.DrawLine(muted, new Point(13, 9), new Point(19, 9));
                    dc.DrawLine(white, new Point(21, 9), new Point(27, 9));
                    dc.DrawLine(cyan, new Point(10, 16), new Point(22, 16));
                    dc.DrawLine(amber, new Point(13, 13), new Point(10, 16));
                    dc.DrawLine(amber, new Point(13, 19), new Point(10, 16));
                    dc.DrawLine(amber, new Point(19, 13), new Point(22, 16));
                    dc.DrawLine(amber, new Point(19, 19), new Point(22, 16));
                    dc.DrawLine(green, new Point(6, 23), new Point(26, 23));
                    break;

                case "text_case":
                    // "Aa" dual-case glyph
                    DrawGlyph(dc, "A", 5, 6, 15, WhiteBrush);
                    DrawGlyph(dc, "a", 17.5, 9, 13, CyanBrush);
                    break;

                case "text_punc":
                    // Chinese comma <-> English comma toggle
                    DrawGlyph(dc, "，", 5, 5, 16, AmberBrush);
                    dc.DrawLine(cyan, new Point(12, 16), new Point(21, 16));
                    DrawGlyph(dc, ",", 21, 6, 18, WhiteBrush);
                    break;

                case "text_affix":
                    // "[+]" prefix/suffix brackets
                    dc.DrawLine(cyan, new Point(8, 7), new Point(5, 7));
                    dc.DrawLine(cyan, new Point(5, 7), new Point(5, 25));
                    dc.DrawLine(cyan, new Point(5, 25), new Point(8, 25));
                    dc.DrawLine(cyan, new Point(24, 7), new Point(27, 7));
                    dc.DrawLine(cyan, new Point(27, 7), new Point(27, 25));
                    dc.DrawLine(cyan, new Point(27, 25), new Point(24, 25));
                    DrawGlyph(dc, "A", 11, 6, 14, WhiteBrush);
                    break;

                case "text_brush":
                    // Content painter brush
                    dc.DrawRoundedRectangle(AmberSoftBrush, amber, new Rect(5, 6, 12, 8), 2, 2);
                    dc.DrawLine(white, new Point(17, 10), new Point(23, 10));
                    dc.DrawLine(white, new Point(23, 10), new Point(23, 25));
                    dc.DrawLine(cyan, new Point(6, 20), new Point(18, 20));
                    dc.DrawLine(green, new Point(6, 25), new Point(16, 25));
                    break;

                case "text_swap":
                    // Bidirectional A <-> B arrows
                    dc.DrawLine(cyan, new Point(6, 11), new Point(25, 11));
                    dc.DrawLine(cyan, new Point(21, 7.5), new Point(25, 11));
                    dc.DrawLine(cyan, new Point(21, 14.5), new Point(25, 11));
                    dc.DrawLine(amber, new Point(26, 21), new Point(7, 21));
                    dc.DrawLine(amber, new Point(11, 17.5), new Point(7, 21));
                    dc.DrawLine(amber, new Point(11, 24.5), new Point(7, 21));
                    break;

                case "text_prop":
                    // Eyedropper / style brush over "T"
                    DrawGlyph(dc, "T", 5, 7, 15, WhiteBrush);
                    dc.DrawLine(cyan, new Point(16, 23), new Point(25, 9));
                    dc.DrawEllipse(AmberBrush, white, new Point(25, 9), 2.5, 2.5);
                    break;

                case "text_box_slot":
                    // Rounded stadium / slot frame around "T"
                    dc.DrawRoundedRectangle(CyanSoftBrush, cyan, new Rect(3.5, 8.5, 25, 15), 7.5, 7.5);
                    DrawGlyph(dc, "T", 12, 8.5, 11.5, WhiteBrush);
                    break;

                case "text_box_mask":
                    // Filled wipeout box with crossed background line hidden
                    dc.DrawLine(muted, new Point(3, 16), new Point(7, 16));
                    dc.DrawLine(muted, new Point(25, 16), new Point(29, 16));
                    dc.DrawRoundedRectangle(AmberSoftBrush, white, new Rect(6, 8, 20, 16), 2, 2);
                    DrawGlyph(dc, "T", 12, 8, 12, AmberBrush);
                    break;

                case "text_settings":
                    // Gear / dial next to "T"
                    DrawGlyph(dc, "T", 5, 5, 14, WhiteBrush);
                    dc.DrawEllipse(CyanSoftBrush, cyan, new Point(22, 21), 5, 5);
                    dc.DrawEllipse(AmberBrush, null, new Point(22, 21), 1.8, 1.8);
                    break;

                case "hatch_clip":
                    // Hatched block with diagonal cut corner & scissor guide
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(5, 6, 22, 20));
                    dc.DrawLine(muted, new Point(6, 14), new Point(14, 6));
                    dc.DrawLine(muted, new Point(6, 22), new Point(22, 6));
                    dc.DrawLine(muted, new Point(12, 26), new Point(27, 11));
                    dc.DrawRectangle(AmberSoftBrush, amber, new Rect(15, 14, 12, 12));
                    break;

                case "hatch_clip_split":
                    // Outer hatched slab + floating independent cut-out hatch piece
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(4, 5, 18, 18));
                    dc.DrawLine(muted, new Point(5, 13), new Point(13, 5));
                    dc.DrawLine(muted, new Point(5, 21), new Point(21, 5));
                    dc.DrawRectangle(GreenSoftBrush, green, new Rect(15, 16, 13, 11));
                    dc.DrawLine(green, new Point(16, 24), new Point(24, 16));
                    break;

                case "hatch_split":
                    // Single hatch split apart by glowing dashed cut line
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(4, 7, 10, 18));
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(18, 7, 10, 18));
                    dc.DrawLine(muted, new Point(5, 14), new Point(12, 7));
                    dc.DrawLine(muted, new Point(19, 14), new Point(26, 7));
                    dc.DrawLine(amber, new Point(16, 4), new Point(16, 28));
                    break;

                case "hatch_union":
                    // Two hatched areas fused into one bold outline
                    dc.DrawRoundedRectangle(GreenSoftBrush, green, new Rect(4, 8, 24, 16), 3, 3);
                    dc.DrawLine(white, new Point(7, 20), new Point(18, 9));
                    dc.DrawLine(white, new Point(14, 23), new Point(25, 12));
                    break;

                case "hatch_disasso":
                    // Broken link badge over hatch
                    dc.DrawRectangle(CyanSoftBrush, muted, new Rect(5, 7, 22, 18));
                    dc.DrawLine(amber, new Point(8, 23), new Point(24, 9));
                    dc.DrawEllipse(null, white, new Point(11, 13), 3.5, 3.5);
                    dc.DrawEllipse(null, white, new Point(21, 19), 3.5, 3.5);
                    break;

                case "hatch_bound":
                    // Glowing polyline boundary around hatch
                    dc.DrawRectangle(CyanSoftBrush, cyan, new Rect(5, 7, 22, 18));
                    dc.DrawEllipse(AmberBrush, null, new Point(5, 7), 2.1, 2.1);
                    dc.DrawEllipse(AmberBrush, null, new Point(27, 7), 2.1, 2.1);
                    dc.DrawEllipse(AmberBrush, null, new Point(27, 25), 2.1, 2.1);
                    dc.DrawEllipse(AmberBrush, null, new Point(5, 25), 2.1, 2.1);
                    break;

                case "hatch_base":
                    // Crosshair origin point at lower-left corner of hatch grid
                    dc.DrawRectangle(CyanSoftBrush, white, new Rect(7, 6, 19, 18));
                    dc.DrawLine(muted, new Point(14, 6), new Point(14, 24));
                    dc.DrawLine(muted, new Point(7, 15), new Point(26, 15));
                    dc.DrawEllipse(AmberBrush, white, new Point(7, 24), 3.2, 3.2);
                    break;

                case "control_center":
                    // Sliders
                    dc.DrawLine(muted, new Point(5, 10), new Point(27, 10));
                    dc.DrawLine(muted, new Point(5, 16), new Point(27, 16));
                    dc.DrawLine(muted, new Point(5, 22), new Point(27, 22));
                    dc.DrawEllipse(CyanBrush, white, new Point(12, 10), 2.8, 2.8);
                    dc.DrawEllipse(AmberBrush, white, new Point(21, 16), 2.8, 2.8);
                    dc.DrawEllipse(GreenBrush, white, new Point(14, 22), 2.8, 2.8);
                    break;

                case "mcp_core":
                default:
                    // Connected network hub
                    dc.DrawLine(cyan, new Point(16, 16), new Point(7, 8));
                    dc.DrawLine(cyan, new Point(16, 16), new Point(25, 8));
                    dc.DrawLine(cyan, new Point(16, 16), new Point(16, 26));
                    dc.DrawEllipse(GreenSoftBrush, green, new Point(16, 16), 4.2, 4.2);
                    dc.DrawEllipse(CyanBrush, null, new Point(7, 8), 2.8, 2.8);
                    dc.DrawEllipse(CyanBrush, null, new Point(25, 8), 2.8, 2.8);
                    dc.DrawEllipse(AmberBrush, null, new Point(16, 26), 2.8, 2.8);
                    break;
            }
        }

        private static void DrawDiamond(DrawingContext dc, double cx, double cy, double rx, double ry, Brush? fill, Pen pen)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(cx, cy - ry), true, true);
                ctx.LineTo(new Point(cx + rx, cy), true, false);
                ctx.LineTo(new Point(cx, cy + ry), true, false);
                ctx.LineTo(new Point(cx - rx, cy), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(fill, pen, g);
        }

        private static void DrawGlyph(DrawingContext dc, string text, double x, double y, double fontSize, Brush brush)
        {
#pragma warning disable CS0618
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                fontSize,
                brush);
#pragma warning restore CS0618
            dc.DrawText(ft, new Point(x, y));
        }

        private static SolidColorBrush FreezeBrush(Color color)
        {
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }

        private static Pen FreezePen(Brush brush, double thickness)
        {
            var p = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            p.Freeze();
            return p;
        }
    }
}
