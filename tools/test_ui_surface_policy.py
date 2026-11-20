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
layer_x=text('src/YangCAD.AutoCAD/UI/LayerQuickWindow.xaml')
layer_c=text('src/YangCAD.AutoCAD/UI/LayerQuickWindow.xaml.cs')

buttons=re.findall(r'(?:Button|MenuItem)\("([^"]+)",\s*"([^"]+)"', ribbon)
registered_commands = set(re.findall(r'CommandMethod\("([^"]+)"', commands))
ok('all public CAD command classes are discoverable', not any(
    '[assembly: CommandClass' in p.read_text(encoding='utf-8-sig')
    for p in (root/'src/YangCAD.AutoCAD').rglob('*.cs')))
ok('native CAD commands are unique', len(registered_commands) == len(re.findall(r'CommandMethod\("([^"]+)"', commands)))
ok('ribbon panels put drawing before project and settings',
   re.findall(r'AddPanel\(newTab, "([^"]+)"', ribbon) == [
       '常用工具', '图层与填充', '文字与标注', '结构绘图', '图纸与引用', '连接与设置'])
ok('ribbon commands have command methods', bool(buttons) and all(cmd.strip() in registered_commands for _,cmd in buttons))
ok('ribbon menus keep their category labels', 'IsSplit = false' in ribbon and 'IsSynchronizedWithCurrentItem = false' in ribbon)
ribbon_commands = {cmd.strip() for _, cmd in buttons}
ok('slab placement and repair are visible while old slab modes remain callable',
   {'SLB','SLBFIX'} <= ribbon_commands and not {'SLBP','SLBLAYER','SLBSET'} & ribbon_commands
   and {'SLBP','SLBLAYER','SLBSET'} <= registered_commands)
ok('ribbon and toolbox preserve active CAD commands', all(r'\x03' not in ui for ui in [ribbon, tb_c]))
ok('toolbox command buttons all route to registered commands',
   set(re.findall(r'Tag="([A-Z][A-Z0-9_]+)"', tb_x)) <= registered_commands)
ok('control center has no visible MCP controls', all(x not in cc_x for x in ['MCP 状态','复制 MCP 配置','允许 MCP 修改当前 DWG','Tag="MCP"']))
ok('mcp window has no duplicate theme selector', all(x not in mcp_x for x in ['墨曜 · Obsidian','月瓷 · Porcelain','雾蓝 · Mist']))
ok('toolbox registry is exactly two descriptors', re.findall(r'Id\s*=\s*"([^"]+)"', registry) == ['project.revit.prepare','project.image.embed'])
catalog = text('src/YangCAD.AutoCAD/Toolbox/CadToolCatalog.cs')
rows = re.findall(r'new CadToolItem\("[^"]+",\s*"[^"]+",\s*"[^"]+",\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"', catalog)
catalog_commands, hints, icon_keys = (list(col) for col in zip(*rows))
icons = set(re.findall(r'case "([^"]+)"', text('src/YangCAD.AutoCAD/UI/RibbonIcons.cs')))
ok('catalog commands are unique and registered', len(catalog_commands) == len(set(catalog_commands)) and set(catalog_commands) <= registered_commands)
ok('catalog alias hints are unique', len(hints) == len({hint.upper() for hint in hints}))
catalog_entries = re.findall(r'new CadToolItem\([^\n]+\n[^\n]+', catalog)
primary = {cmd for cmd, line in zip(catalog_commands, catalog_entries)
           if 'secondary: true' not in line and cmd != 'YANGCAD_TOOLBOX'}
ok('default toolbox keeps a compact primary catalog', 30 <= len(primary) <= 36)
ok('new combined commands appear in both main surfaces',
   {'YGTALIGN','YGTALONG','YGTSPLITJOIN','YGTCLEAN','YGTCONTENT','YGDIMCHECK','YGDIMEDIT','YGREFEMBED'}
   <= (primary & ribbon_commands))
ok('secondary tools are discoverable by search', '(!tool.IsSecondary || terms.Length > 0)' in tb_c)
ok('catalog icon keys exist', set(icon_keys) <= icons)
ok('toolbox renders catalog short labels and commands', all(x in tb_x for x in ['{Binding ShortName}', '{Binding Command}', 'ToolGroups']))
ok('four frequent tools are direct Ribbon buttons', all(re.search(r'(?<!MenuItem)Button\("[^"]+", "'+cmd+r' ', ribbon) for cmd in ('YGHCLIP','YGLEADERFIX','YGINC','YGDL')))

for label,x,c in [('control center',cc_x,cc_c),('mcp window',mcp_x,mcp_c),('toolbox',tb_x,tb_c),('scale converter',scale_x,scale_c),('layer quick',layer_x,layer_c)]:
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
