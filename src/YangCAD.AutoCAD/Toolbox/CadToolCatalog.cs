using System;
using System.Collections.Generic;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Toolbox
{
    public sealed class CadToolItem
    {
        public string Id { get; }
        public string Category { get; }
        public string Name { get; }
        public string Command { get; }
        public string DefaultAliasHint { get; }
        public string IconKey { get; }
        public bool IsLargeRibbon { get; }
        public string Description { get; }

        public CadToolItem(
            string id,
            string category,
            string name,
            string command,
            string defaultAliasHint,
            string iconKey,
            bool isLargeRibbon,
            string description)
        {
            Id = id;
            Category = category;
            Name = name;
            Command = command;
            DefaultAliasHint = defaultAliasHint;
            IconKey = iconKey;
            IsLargeRibbon = isLargeRibbon;
            Description = description;
        }
    }

    public static class CadToolCatalog
    {
        public static IReadOnlyList<CadToolItem> All { get; } = new[]
        {
            // 图层
            new CadToolItem("cad.layer.panel", "图层", "图层工具", "YGLAYER", "YL", "layer_panel", true,
                "打开可视化图层面板，查看颜色与状态并批量开关、冻结、锁定或合并图层"),
            new CadToolItem("cad.layer.onoff", "图层", "图层开关", "YGLONOFF", "LF", "layer_off", false,
                "框选或点选图元，一键关闭/显示所在图层（支持块与外参内部图层）"),
            new CadToolItem("cad.layer.merge", "图层", "合并图层", "YGLMERGE", "LM", "layer_merge", false,
                "选中源图元与目标图元，批量将源图层对象合入目标图层"),
            new CadToolItem("cad.layer.freeze", "图层", "图层冻结", "YGLFREEZE", "FZ", "layer_freeze", false,
                "框选或点选图元，一键冻结/解冻所在图层"),
            new CadToolItem("cad.layer.lock", "图层", "图层锁定", "YGLLOCK", "LK", "layer_lock", false,
                "框选或点选图元，一键锁定/解锁所在图层"),

            // 修改
            new CadToolItem("cad.detail.extract", "修改", "提取大样", "YGDETAIL", "QG", "detail_extract", true,
                "框选区域提取大样副本并按倍数放大，自动记忆上次放大倍数"),
            new CadToolItem("cad.scale.convert", "修改", "比例转换", "YGSCALE", "SD", "scale_convert", true,
                "支持 1:1 标准标注尺度切换与缩尺绘图几何缩放，自动记忆上次比例"),
            new CadToolItem("cad.double.line", "结构", "智能双线", "YGDL", "DL", "double_line", true,
                "交互绘制带斜接/切平转角与可选中心线的智能双线（自动记忆宽度）"),
            new CadToolItem("cad.double.line.width", "结构", "改双线宽度", "YGDLW", "DLW", "double_line", false,
                "保持中心线不变修改已有双线或平行线段对的宽度"),

            // 结构标注
            new CadToolItem("cad.beam.mark", "结构", "梁尺寸标注", "BMK", "BMK", "beam_mark", true,
                "框选梁边线按图层自动配对测量宽度并生成梁高x宽标注文字"),
            new CadToolItem("cad.col.mark", "结构", "柱尺寸标注", "CMK", "CMK", "col_mark", true,
                "框选矩形柱轮廓自动量取尺寸并批量标注，跳过已有标注并汇总数量"),
            new CadToolItem("cad.slab.place", "结构", "板受力符号", "SLB", "SLB", "slab_sym", false,
                "点击板格内部布置受力符号；设定轮廓和梁线图层后按支承侧出箭头"),
            new CadToolItem("cad.slab.fix", "结构", "板符号修正", "SLBFIX", "SLBF", "slab_fix", false,
                "批量将已放置板受力符号重算居于形心并对齐板方向，自动去重"),
            new CadToolItem("cad.slab.layer", "结构", "板轮廓与梁线", "SLBLAYER", "SLBL", "slab_layer", false,
                "选择梁线样本与可选的闭合矩形板轮廓，按图纸设置识别图层"),
            new CadToolItem("cad.slab.preview", "结构", "板格预览", "SLBP", "SLBP", "slab_sym", false,
                "点击板格内部高亮预览识别出的封闭板格轮廓与受力方向"),

            // 标注
            new CadToolItem("cad.leader.fix", "标注", "引线修复", "YGLEADERFIX", "LFX", "leader_fix", true,
                "一键框选炸开的引线线段和文字，自动忽略小箭头并合并为原生多重引线"),
            new CadToolItem("cad.dim.override", "标注", "找改值标注", "YGDIMTXT", "DT", "dim_override", false,
                "快速筛选并高亮选中所有手动修改过显示文字的尺寸标注"),
            new CadToolItem("cad.slope.mark", "标注", "标注坡度", "YGSLOPE", "SP", "slope_mark", false,
                "点选斜线或拾取两点立即标出坡度（支持百分比、1:X 与角度）"),
            new CadToolItem("cad.text.frame", "标注", "文字加框", "YGTBOX", "TB", "text_frame", false,
                "框选单行或多行文字，一键套用记忆留白生成矩形或圆形边框"),
            new CadToolItem("cad.dim.check", "标注", "尾数检查", "DIMCHK", "DCK", "dim_check", false,
                "选出尾数不是 0 或 5 倍数的标注/数字文字（支持真实值/显示值模式）"),
            new CadToolItem("cad.dim.fix", "标注", "标注取整", "DIMFIX", "DFX", "dim_fix", false,
                "将容差范围内的标注或数字文字快速改写为 5 的整数倍"),
            new CadToolItem("cad.dim.restore", "标注", "恢复原值", "DIMRE", "DRE", "dim_restore", false,
                "清除标注替代文字，恢复显示真实的 CAD 测量数值"),
            new CadToolItem("cad.dim.flip", "标注", "翻转标注", "DIMFZ", "DFZ", "dim_flip", false,
                "将倒置或选定的尺寸标注文字翻转 180° 并移至尺寸线另一侧"),
            new CadToolItem("cad.text.up", "标注", "文字向上", "TXTUP", "TUP", "text_up", false,
                "框选文字、标注或属性块，一键将倒着/反向文字原地翻正"),
            new CadToolItem("cad.text.align", "标注", "段落对齐", "TXAL", "TXA", "text_align", false,
                "以基准文字为准，按设定行距倍数左对齐并旋转整理多个段落文字"),

            // 文字排版与快改
            new CadToolItem("cad.text.align_left", "文字", "文字左对齐", "YGTL", "YTL", "text_align_lr", false,
                "按最左外框对齐多行/单行文字或属性块"),
            new CadToolItem("cad.text.align_center", "文字", "水平居中对齐", "YGTC", "YTC", "text_align_lr", false,
                "按平均水平中心线居中对齐选定文字"),
            new CadToolItem("cad.text.align_right", "文字", "文字右对齐", "YGTR", "YTR", "text_align_lr", false,
                "按最右外框对齐选定文字"),
            new CadToolItem("cad.text.align_top", "文字", "文字顶对齐", "YGTT", "YTT", "text_align_lr", false,
                "按最上端外框齐平多个文字"),
            new CadToolItem("cad.text.align_bottom", "文字", "文字底对齐", "YGTB", "YTB", "text_align_lr", false,
                "按最下端外框齐平多个文字"),
            new CadToolItem("cad.text.align_mid", "文字", "垂直居中对齐", "YGTV", "YTV", "text_align_lr", false,
                "按平均垂直中线对齐同行文字"),
            new CadToolItem("cad.text.align_line", "文字", "文字按线对齐", "YGTLINE", "YTLL", "text_align_lr", false,
                "沿直线、多段线或样条线自动旋转并垂直投影对齐多个文字"),
            new CadToolItem("cad.text.align_arc", "文字", "文字按弧环绕", "YGTARC", "YTAR", "text_arc", false,
                "沿圆弧或圆均布或按点选方向径向环绕排列文字"),
            new CadToolItem("cad.text.inc", "文字", "递增复制", "YGINC", "YIC", "text_increment", true,
                "点选基准文字连续按步长递增复制到任意位置（支持楼层跳0与前导零）"),
            new CadToolItem("cad.text.join", "文字", "连接文字", "YGTJOIN", "YTJ", "text_join", false,
                "按书写阅读顺序将多个文字拼接为一个完整文字"),
            new CadToolItem("cad.text.s2m", "文字", "单行转多行", "YGTS2M", "YS2M", "text_join", false,
                "将多个 TEXT 按行列位置合并为可自动换行的 MTEXT"),
            new CadToolItem("cad.text.m2s", "文字", "多行转单行", "YGTM2S", "YM2S", "text_join", false,
                "将 MTEXT 按行拆分为独立保留样式的 TEXT 单行文字"),
            new CadToolItem("cad.text.explode", "文字", "文字炸碎", "YGTEXP", "YTX", "text_join", false,
                "按真实字宽将整串文字逐字/逐词拆分为独立文字对象"),
            new CadToolItem("cad.text.space", "文字", "去空格/去换行", "YGTSPACE", "YSP", "text_space", true,
                "去除全部空格、去除多余空格、去除换行或去除多余空行"),
            new CadToolItem("cad.text.case", "文字", "大小写/驼峰", "YGTCASE", "YCS", "text_case", true,
                "切换全大写、全小写、首字母大写与驼峰命名"),
            new CadToolItem("cad.text.punc", "文字", "中英标点切换", "YGTPUNC", "YPN", "text_punc", true,
                "智能互转中文与英文标点（自动保护小数点与比例号）"),
            new CadToolItem("cad.text.affix", "文字", "加删前后缀", "YGTAFFIX", "YAF", "text_affix", false,
                "批量在文字/标注首尾追加或删除指定前缀与后缀"),
            new CadToolItem("cad.text.brush", "文字", "刷内容", "YGTBRUSH", "YTB", "text_brush", true,
                "一次拾取源文字（含块属性/标注），连续点刷替换多个目标内容"),
            new CadToolItem("cad.text.swap", "文字", "换内容", "YGTSWAP", "YSW", "text_swap", false,
                "点选两个文字、块属性或标注，直接互换二者内容"),
            new CadToolItem("cad.text.prop", "文字", "改特性刷", "YGTPROP", "YPR", "text_prop", false,
                "复制源文字的字体样式、字高、宽比、旋转角及图层属性"),
            new CadToolItem("cad.text.box_slot", "文字", "跑道圆角框", "YGTBOXSLOT", "YBS", "text_box_slot", false,
                "为文字生成两侧半圆/跑道形封闭边框"),
            new CadToolItem("cad.text.box_mask", "文字", "文字遮罩框", "YGTBOXMASK", "YBM", "text_box_mask", false,
                "为文字生成带 WIPEOUT 遮罩外框并将文字置顶，遮盖下方线条"),
            new CadToolItem("cad.text.settings", "文字", "文字排版设置", "YGTSET", "YTS", "text_settings", false,
                "集中配置按线间距、弧距、递增步长与默认清洗模式"),

            // 填充图案增强
            new CadToolItem("cad.hatch.clip", "填充", "剪裁填充", "YGHCLIP", "HHJ", "hatch_clip", true,
                "用矩形或闭合边界挖除/保留/拆分挖出填充图案（锁定原点，纹理不跳位）"),
            new CadToolItem("cad.hatch.clip_split", "填充", "挖出并保留", "YGHCLIPSPLIT", "HHJK", "hatch_clip_split", true,
                "从原填充中挖掉选中区域但不删除，将该部分拆成独立新填充"),
            new CadToolItem("cad.hatch.clip_int", "填充", "仅留框内", "YGHCLIPINT", "HHJI", "hatch_clip", false,
                "只保留选定闭合区域内的填充图案，裁去框外部分（适合大样截取）"),
            new CadToolItem("cad.hatch.split", "填充", "拆分填充", "YGHSPLIT", "HHX", "hatch_split", true,
                "将多区域一体的填充按孤岛拆开，或指定切分线将整块填充一刀两半"),
            new CadToolItem("cad.hatch.union", "填充", "合并填充", "YGHUNION", "HHU", "hatch_union", true,
                "将多个填充布尔并集融合为统一填充，自动消除重叠白洞与共边缝隙"),
            new CadToolItem("cad.hatch.disasso", "填充", "去填充关联", "YGHDIS", "HHGL", "hatch_disasso", false,
                "批量或全图一键取消填充的边界关联性，防止改线时填充变形"),
            new CadToolItem("cad.hatch.boundary", "填充", "重建边界", "YGHBOUND", "HHB", "hatch_bound", false,
                "从填充图案逆向生成外环与内洞闭合多段线边界（可选恢复关联）"),
            new CadToolItem("cad.hatch.origin", "填充", "改填充基点", "YGHBASE", "HHI", "hatch_base", false,
                "拾取墙角或控制点，一键重设排砖/格栅填充图案的起铺原点"),

            // 专项
            new CadToolItem("project.revit.prepare", "专项", "Revit预处理", "YGREVIT", "RV", "revit_prep", true,
                "一键整理当前图纸并导出供 Revit 使用的标准化副本（原图不变）"),
            new CadToolItem("project.image.embed", "专项", "当前图内嵌", "YGEMBED", "EM", "image_embed", false,
                "将当前图纸的外部光栅图片与 DWG 外参安全内嵌为独立副本"),
            new CadToolItem("project.image.embed.batch", "专项", "批量内嵌", "YGEMBEDBATCH", "EMB", "folder_embed", false,
                "选择文件夹，后台批量将所有 DWG 的外链图片与参照内嵌为自包含副本"),
            new CadToolItem("ui.toolbox", "专项", "工具箱", "YANGCAD_TOOLBOX", "YY", "toolbox_grid", false,
                "打开或显示紧凑多分类工具箱面板（快捷命令 YY）"),

            // 系统
            new CadToolItem("ui.keys", "系统", "快捷键", "YGKEYS", "YK", "shortcut_keys", true,
                "为所有 YangCAD 工具自定义命令行缩写，自动冲突检测与备份"),
            new CadToolItem("ui.control", "系统", "控制中心", "YANGCAD", "YC", "control_center", true,
                "切换插件主题、查看本机环境与日志、管理设置"),
            new CadToolItem("ui.mcp", "系统", "MCP", "YANGCAD_MCP", "YM", "mcp_core", true,
                "查看本机 AI 网关连接状态、复制 MCP 配置并控制写入权限")
        };

        public static string GetActiveAliasOrHint(string? pgpPath, CadToolItem item)
        {
            if (item == null) return "";
            if (!string.IsNullOrWhiteSpace(pgpPath))
            {
                try
                {
                    var current = ShortcutAliasService.CurrentAlias(pgpPath!, item.Command);
                    if (!string.IsNullOrWhiteSpace(current)) return current!;
                    if (string.Equals(item.Command, "YANGCAD_TOOLBOX", StringComparison.OrdinalIgnoreCase))
                    {
                        var yy = ShortcutAliasService.CurrentAlias(pgpPath!, "YY");
                        if (!string.IsNullOrWhiteSpace(yy)) return yy!;
                    }
                }
                catch
                {
                    // Fall back to default hint when PGP cannot be parsed.
                }
            }
            return item.DefaultAliasHint;
        }
    }
}
