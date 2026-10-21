using System;
using System.Collections.Generic;
using System.Linq;
using YangCAD.Core.Tools;
namespace YangCAD.AutoCADHost.Mcp
{
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string,ICadTool> _tools=new Dictionary<string,ICadTool>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync=new object();
        public void Register(ICadTool tool){lock(_sync)_tools[tool.Descriptor.Name]=tool;}
        public bool TryGet(string name,out ICadTool tool){lock(_sync)return _tools.TryGetValue(name,out tool!);}
        public ToolDescriptor[] Describe(){lock(_sync)return _tools.Values.Select(x=>x.Descriptor).OrderBy(x=>x.Name).ToArray();}
    }
}
