using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;

namespace YangCAD.AutoCADHost.Toolbox
{
    public enum ToolboxDeclaredStatus { Available, Experimental, Planned, Hidden }
    public enum ToolboxParameterKind { Text, Number, Bool, Choice, Layer, ObjectSelection, Point, File, Folder, FileOrFolder }

    public sealed class ToolboxParameterShape
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public ToolboxParameterKind Kind { get; set; }
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public string[] Options { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
    }

    public sealed class ToolboxToolDescriptor
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string CategoryPath { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconKey { get; set; } = "";
        public string Host { get; set; } = "AutoCAD";
        public int[] SupportedYears { get; set; } = Array.Empty<int>();
        public ToolboxDeclaredStatus DeclaredStatus { get; set; } = ToolboxDeclaredStatus.Available;
        public bool IsWrite { get; set; }
        public bool SupportsDryRun { get; set; }
        public bool SupportsGui { get; set; } = true;
        public bool SupportsMcp { get; set; }
        public bool SupportsBatch { get; set; }
        public string[] SearchKeywords { get; set; } = Array.Empty<string>();
        public ToolboxParameterShape[] ParameterShape { get; set; } = Array.Empty<ToolboxParameterShape>();
        public string HelpId { get; set; } = "";

        public string StatusText => DeclaredStatus switch
        {
            ToolboxDeclaredStatus.Available => "可用",
            ToolboxDeclaredStatus.Experimental => "实验",
            ToolboxDeclaredStatus.Planned => "计划",
            _ => "隐藏"
        };
    }

    public interface IToolboxRuntimeAdapter
    {
        string ToolId { get; }
        bool IsAvailable(Document? document);
        object? Execute(Document? document, IReadOnlyDictionary<string, object?> values);
        object? Preview(Document? document, IReadOnlyDictionary<string, object?> values);
    }

    public sealed class DelegateToolboxRuntimeAdapter : IToolboxRuntimeAdapter
    {
        private readonly Func<Document?, bool> _available;
        private readonly Func<Document?, IReadOnlyDictionary<string, object?>, object?> _execute;
        private readonly Func<Document?, IReadOnlyDictionary<string, object?>, object?>? _preview;
        public DelegateToolboxRuntimeAdapter(string toolId, Func<Document?, bool> available,
            Func<Document?, IReadOnlyDictionary<string, object?>, object?> execute,
            Func<Document?, IReadOnlyDictionary<string, object?>, object?>? preview = null)
        {
            ToolId = toolId;
            _available = available;
            _execute = execute;
            _preview = preview;
        }
        public string ToolId { get; }
        public bool IsAvailable(Document? document) => _available(document);
        public object? Execute(Document? document, IReadOnlyDictionary<string, object?> values) => _execute(document, values);
        public object? Preview(Document? document, IReadOnlyDictionary<string, object?> values)
            => _preview != null ? _preview(document, values) : _execute(document, values);
    }
}
