using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YangCAD.AutoCADHost.Infrastructure;
using YangCAD.AutoCADHost.Services.Cad;

namespace YangCAD.AutoCADHost.Toolbox
{
    /// <summary>
    /// GUI registry intentionally contains only the two user-facing production tools.
    /// MCP capabilities remain registered in RuntimeHost and are not mirrored as Toolbox cards.
    /// </summary>
    public sealed class ToolboxRegistry
    {
        private readonly List<ToolboxToolDescriptor> _descriptors = new List<ToolboxToolDescriptor>();
        private readonly Dictionary<string, IToolboxRuntimeAdapter> _adapters =
            new Dictionary<string, IToolboxRuntimeAdapter>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<ToolboxToolDescriptor> Descriptors => _descriptors;

        public void Register(ToolboxToolDescriptor descriptor, IToolboxRuntimeAdapter? adapter = null)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                throw new ArgumentException("Toolbox descriptor Id 不能为空。");
            if (_descriptors.Any(x => string.Equals(x.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("重复 Toolbox Tool Id：" + descriptor.Id);
            _descriptors.Add(descriptor);
            if (adapter != null) _adapters[descriptor.Id] = adapter;
        }

        public bool CanExecute(string id)
        {
            if (!_adapters.TryGetValue(id, out var adapter)) return false;
            try
            {
                return adapter.IsAvailable(Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument);
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("Toolbox availability check failed: " + id, ex);
                return false;
            }
        }

        public object? Execute(string id, IReadOnlyDictionary<string, object?> values)
        {
            if (!_adapters.TryGetValue(id, out var adapter))
                throw new InvalidOperationException("该工具尚未接入运行时：" + id);
            var document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (!adapter.IsAvailable(document)) throw new InvalidOperationException("当前宿主/文档不可执行该工具。");
            return adapter.Execute(document, values);
        }

        public object? Preview(string id, IReadOnlyDictionary<string, object?> values)
        {
            if (!_adapters.TryGetValue(id, out var adapter))
                throw new InvalidOperationException("该工具尚未接入运行时：" + id);
            var document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (!adapter.IsAvailable(document)) throw new InvalidOperationException("当前宿主/文档不可执行该工具。");
            return adapter.Preview(document, values);
        }

        public static ToolboxRegistry CreateDefault()
        {
            var registry = new ToolboxRegistry();

            registry.Register(new ToolboxToolDescriptor
            {
                Id = "project.revit.prepare",
                Name = "Revit 图纸预处理",
                CategoryPath = "Revit",
                Description = "将当前 DWG 克隆为 Revit 交换副本，按 RVT Exchange v1 规则分类常用几何、文字、引线、标注与填充；匿名块、动态块状态和 Xref 等安全边界会在报告中明确提示。原图不修改。",
                DeclaredStatus = ToolboxDeclaredStatus.Available,
                IsWrite = true,
                SupportsDryRun = true,
                SupportsMcp = true,
                SearchKeywords = new[] { "revit", "rvt", "预处理", "图层分离", "文字", "引线", "标注", "填充", "exchange" },
                ParameterShape = new[]
                {
                    Shape("output_folder", "输出目录", ToolboxParameterKind.Folder, false, "", description: "留空时优先输出到当前 DWG 同目录；未命名图纸输出到 Documents\\YangCADExports。可输入尚未创建的新目录。"),
                    Shape("output_suffix", "文件后缀", ToolboxParameterKind.Text, true, "_RVT", description: "输出 DWG 文件名后缀，例如 _RVT。已存在时自动生成不覆盖的新文件名。"),
                    Shape("scan_blocks", "检查普通块定义", ToolboxParameterKind.Bool, false, true, description: "仅统计普通块定义并写入诊断；RVT Exchange v1 不导出块定义子实体，因为协议未包含 BlockReference 实例变换。块引用会作为 unsupported 明确报告。"),
                    Shape("include_paperspace", "包含布局空间", ToolboxParameterKind.Bool, false, true, description: "处理 ModelSpace 与所有布局空间。"),
                    Shape("normalize_geometry", "线条归一为 ByLayer", ToolboxParameterKind.Bool, false, true, description: "几何线条迁移到交换层后，将颜色/线型/线宽覆盖归一为 ByLayer。标注、文字、引线、填充保留自身语义。")
                }
            }, new DelegateToolboxRuntimeAdapter("project.revit.prepare", doc => doc != null, (doc, values) =>
            {
                if (doc == null) throw new InvalidOperationException("当前没有活动 DWG。");
                var request = BuildRevitExchangeRequest(values);
                return CadTransactionRunner.ReadContext(doc, ctx =>
                    RevitExchangeService.Export(ctx.Database, ctx.Transaction, doc.Name, request));
            }, (doc, values) =>
            {
                if (doc == null) throw new InvalidOperationException("当前没有活动 DWG。");
                var request = BuildRevitExchangeRequest(values);
                return CadTransactionRunner.ReadContext(doc, ctx =>
                    RevitExchangeService.Preview(ctx.Database, ctx.Transaction, doc.Name, request));
            }));

            registry.Register(new ToolboxToolDescriptor
            {
                Id = "project.image.embed",
                Name = "引用内嵌（当前图纸 / 批量）",
                CategoryPath = "批处理",
                Description = "当前图纸一键生成内嵌副本；或选择文件夹批量处理。图片转换为 OLE，可选把 DWG 外部参照绑定为内部块。PDF/DGN/DWF Underlay 无法无损内嵌，会在结果中列为未处理。源 DWG 不覆盖。",
                DeclaredStatus = ToolboxDeclaredStatus.Experimental,
                IsWrite = true,
                SupportsDryRun = true,
                SupportsMcp = true,
                SupportsBatch = true,
                SearchKeywords = new[] { "image", "raster", "ole", "embed", "图片", "内嵌", "批量", "外链", "自包含" },
                ParameterShape = new[]
                {
                    Shape("input_path", "DWG 或文件夹（快捷按钮可不填）", ToolboxParameterKind.FileOrFolder, false, "", description: "可选择单个 DWG 或文件夹。也可使用上方快捷按钮，一键处理当前已保存 DWG 或选定文件夹。"),
                    Shape("output_folder", "输出目录", ToolboxParameterKind.Folder, false, "", description: "留空时生成输入目录同级的 *_Embedded 输出目录。可输入尚未创建的新目录；已有输出文件永不覆盖。"),
                    Shape("include_subfolders", "包含子文件夹", ToolboxParameterKind.Bool, false, true, description: "文件夹模式是否递归扫描子目录；输出会保留相对目录结构。"),
                    Shape("bind_dwg_xrefs", "同时绑定 DWG 外部参照", ToolboxParameterKind.Bool, false, true, description: "把已加载的 DWG Xref 绑定为内部块（保留前缀以防样式同名冲突）；丢失或无法绑定的引用报告失败。"),
                    Shape("show_worker", "显示 Worker AutoCAD", ToolboxParameterKind.Bool, false, false, description: "调试时可显示独立 AutoCAD 自动化进程；大量处理默认隐藏。"),
                    Shape("require_all_images", "单图必须全部成功", ToolboxParameterKind.Bool, false, true, description: "开启后，只要某张图片无法安全转换，该 DWG 就标记 FAIL 且不留下半成品。"),
                    Shape("command_timeout_seconds", "单次命令超时(秒)", ToolboxParameterKind.Number, false, 90d, min: 30, max: 600, description: "PASTECLIP 等 AutoCAD 命令等待上限。")
                }
            }, new DelegateToolboxRuntimeAdapter("project.image.embed", _ => true, (doc, values) =>
                ImageEmbedBatchService.Start(BuildImageEmbedRequest(values, doc?.Name)),
                (doc, values) => ImageEmbedBatchService.Preview(BuildImageEmbedRequest(values, doc?.Name))));

            return registry;
        }

        private static ToolboxParameterShape Shape(string key, string label, ToolboxParameterKind kind, bool required, object? defaultValue,
            double? min = null, double? max = null, string[]? options = null, string description = "")
            => new ToolboxParameterShape
            {
                Key = key,
                Label = label,
                Kind = kind,
                Required = required,
                DefaultValue = defaultValue,
                Min = min,
                Max = max,
                Options = options ?? Array.Empty<string>(),
                Description = description
            };

        private static RevitExchangeService.Request BuildRevitExchangeRequest(IReadOnlyDictionary<string, object?> values)
        {
            string TextValue(string key, string fallback)
                => values.TryGetValue(key, out var raw) && raw != null ? (Convert.ToString(raw, CultureInfo.CurrentCulture) ?? fallback) : fallback;
            bool BoolValue(string key, bool fallback)
                => values.TryGetValue(key, out var raw) && raw != null ? Convert.ToBoolean(raw, CultureInfo.CurrentCulture) : fallback;

            return new RevitExchangeService.Request
            {
                OutputFolder = TextValue("output_folder", "").Trim(),
                OutputSuffix = TextValue("output_suffix", "_RVT").Trim(),
                ScanBlockDefinitions = BoolValue("scan_blocks", true),
                IncludePaperSpace = BoolValue("include_paperspace", true),
                NormalizeGeometryOverrides = BoolValue("normalize_geometry", true)
            };
        }

        private static ImageEmbedBatchService.Request BuildImageEmbedRequest(IReadOnlyDictionary<string, object?> values, string? currentDrawingPath)
        {
            string TextValue(string key, string fallback)
                => values.TryGetValue(key, out var raw) && raw != null ? (Convert.ToString(raw, CultureInfo.CurrentCulture) ?? fallback) : fallback;
            bool BoolValue(string key, bool fallback)
                => values.TryGetValue(key, out var raw) && raw != null ? Convert.ToBoolean(raw, CultureInfo.CurrentCulture) : fallback;
            int IntValue(string key, int fallback)
                => values.TryGetValue(key, out var raw) && raw != null ? Convert.ToInt32(raw, CultureInfo.CurrentCulture) : fallback;

            return new ImageEmbedBatchService.Request
            {
                InputPath = string.IsNullOrWhiteSpace(TextValue("input_path", "")) ? (currentDrawingPath ?? "") : TextValue("input_path", "").Trim(),
                OutputFolder = TextValue("output_folder", "").Trim(),
                IncludeSubfolders = BoolValue("include_subfolders", true),
                ShowWorker = BoolValue("show_worker", false),
                RequireAllImages = BoolValue("require_all_images", true),
                CommandTimeoutSeconds = IntValue("command_timeout_seconds", 90),
                BindDrawingXrefs = BoolValue("bind_dwg_xrefs", true)
            };
        }
    }
}
