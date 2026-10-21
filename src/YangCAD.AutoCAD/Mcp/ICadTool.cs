using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using YangCAD.Core.Tools;

namespace YangCAD.AutoCADHost.Mcp
{
    public sealed class CadToolContext
    {
        public Document Document { get; }
        public Autodesk.AutoCAD.DatabaseServices.Database Database => Document.Database;
        public bool DryRun { get; }
        public IReadOnlyDictionary<string, object?> Args { get; }
        public CadToolContext(Document document, bool dryRun, IReadOnlyDictionary<string, object?> args)
        {
            Document = document;
            DryRun = dryRun;
            Args = args;
        }
    }
    public interface ICadTool { ToolDescriptor Descriptor { get; } object? Execute(CadToolContext context); }
}
