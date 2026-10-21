from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
checks=[]
def text(p): return (root/p).read_text(encoding='utf-8-sig')
def ok(name, cond): checks.append((name,bool(cond)))

def handlers(xaml_text):
    return set(re.findall(r'(?:Click|SelectionChanged)="([A-Za-z_][A-Za-z0-9_]*)"', xaml_text))

def methods(code_text):
    return set(re.findall(r'\b(?:private|public|protected|internal)\s+(?:static\s+)?(?:void|bool|string|object\??|[A-Za-z_][A-Za-z0-9_<>?]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(', code_text))

ribbon=text('src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs')
commands='\n'.join(x.read_text(encoding='utf-8-sig') for x in (root/'src/YangCAD.AutoCAD/Commands').glob('*.cs'))
registry=text('src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs')
cc_x=text('src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml')
cc_c=text('src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml.cs')
mcp_x=text('src/YangCAD.AutoCAD/UI/McpWindow.xaml')
mcp_c=text('src/YangCAD.AutoCAD/UI/McpWindow.xaml.cs')
tb_x=text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml')
tb_c=text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs')
param_c=text('src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml.cs')
scale_x=text('src/YangCAD.AutoCAD/UI/ScaleConvertWindow.xaml')
scale_c=text('src/YangCAD.AutoCAD/UI/ScaleConvertWindow.xaml.cs')

buttons=re.findall(r'Button\("([^"]+)",\s*"([^"]+)"', ribbon)
ok('ribbon contains MCP/工具箱/比例转换/控制中心', [x[0] for x in buttons] == ['MCP','工具箱','比例转换','控制中心'])
ok('ribbon commands have command methods', all(cmd.strip() in commands for _,cmd in buttons))
ok('control center has no visible MCP controls', all(x not in cc_x for x in ['MCP 状态','复制 MCP 配置','允许 MCP 修改当前 DWG','Tag="MCP"']))
ok('mcp window has no duplicate theme selector', all(x not in mcp_x for x in ['墨曜 · Obsidian','月瓷 · Porcelain','雾蓝 · Mist']))
ok('toolbox registry is exactly two descriptors', re.findall(r'Id\s*=\s*"([^"]+)"', registry) == ['project.revit.prepare','project.image.embed'])
ok('toolbox has no system/planning navigation', all(x not in tb_x for x in ['系统','规划','CategoryList','SearchBox','收藏','最近']))

for label,x,c in [('control center',cc_x,cc_c),('mcp window',mcp_x,mcp_c),('toolbox',tb_x,tb_c),('scale converter',scale_x,scale_c)]:
    missing=handlers(x)-methods(c)
    ok(label+' xaml event handlers exist', not missing)

ok('toolbox path browser supports file/folder', all(x in param_c for x in ['OpenFileDialog','FolderBrowserDialog','BrowseFile','BrowseFolder']))
ok('scale converter exposes both drawing modes', '标准模式（模型空间按 1:1 实尺寸绘制）' in scale_x and '缩尺绘图模式（模型空间不是按真实 1:1 绘制）' in scale_x)
ok('mcp has copy refresh diagnostics selfcheck logs actions', all(x in mcp_x for x in ['CopyMcpConfigClick','RefreshClick','CopyDiagnosticsClick','SelfCheckClick','OpenLogsClick']))

failed=[n for n,v in checks if not v]
for n,v in checks:
    print(('PASS' if v else 'FAIL')+': '+n)
print(f'ui surface policy: {len(checks)-len(failed)}/{len(checks)} PASS')
raise SystemExit(1 if failed else 0)
