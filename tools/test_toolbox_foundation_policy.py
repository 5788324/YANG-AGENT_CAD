from pathlib import Path
import re

root=Path(__file__).resolve().parents[1]
checks=[]
def text(p): return (root/p).read_text(encoding='utf-8-sig')
def ok(name,cond): checks.append((name,bool(cond)))
runner=text('src/YangCAD.AutoCAD/Infrastructure/CadTransactionRunner.cs')
ctx=text('src/YangCAD.AutoCAD/Infrastructure/CadExecutionContext.cs')
contracts=text('src/YangCAD.AutoCAD/Toolbox/ToolboxContracts.cs')
registry=text('src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs')
palette=text('src/YangCAD.AutoCAD/UI/ToolboxPaletteManager.cs')
view=text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml')
ribbon=text('src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs')
cc=text('src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml')
mcp=text('src/YangCAD.AutoCAD/UI/McpWindow.xaml')
styles=text('src/YangCAD.AutoCAD/UI/Themes/SharedStyles.xaml')
commands=text('src/YangCAD.AutoCAD/Commands/ControlCenterCommands.cs')
ids=re.findall(r'Id\s*=\s*"([^"]+)"', registry)

ok('old transaction runner signatures preserved','Func<Transaction,T> body' in runner and runner.count('public static T Write<T>')==1 and runner.count('public static T Read<T>')==1)
ok('explicit context runner names','ReadContext<T>' in runner and 'WriteContext<T>' in runner)
ok('context constructor internal','internal CadExecutionContext' in ctx)
ok('context stays four-field','Document { get;' in ctx and 'Database { get;' in ctx and 'Editor { get;' in ctx and 'Transaction { get;' in ctx and 'DryRun' not in ctx and 'AllowErase' not in ctx)
ok('descriptor adapter split','class ToolboxToolDescriptor' in contracts and 'interface IToolboxRuntimeAdapter' in contracts)
ok('path parameter kinds added for production tools','Folder' in contracts and 'FileOrFolder' in contracts)
ok('toolbox exposes exactly two production tools', ids == ['project.revit.prepare','project.image.embed'])
ok('legacy gui tools and backlog removed', all(x not in registry for x in ['system.mcp-status','system.document-context','system.parameter-demo','cad.drawing.audit','cad.layer.audit','cad.polyline.inspect','cad.polyline.clean','cad.layer.cleanup','Planned(registry']))
ok('toolbox navigation shell removed', all(x not in view for x in ['CategoryList','规划','收藏工具','最近使用']))
ok('WPF palette uses AddVisual','AddVisual(' in palette and 'PaletteSet(' in palette)
ok('ribbon has standalone MCP core button','Button("MCP", "YANGCAD_MCP "' in ribbon and 'MCP 状态' not in ribbon)
ok('ribbon keeps toolbox and control center','YANGCAD_TOOLBOX ' in ribbon and 'YANGCAD ' in ribbon)
ok('control center is settings/about only','系统设置' in cc and '关于' in cc and 'Tag="MCP"' not in cc and '复制 MCP 配置' not in cc)
control_method=commands[commands.find('public void OpenControlCenter()'):commands.find('[CommandMethod("YANGCAD_MCP")]')]
ok('control center does not initialize MCP runtime', 'RuntimeHost.EnsureInitialized()' not in control_method)
ok('dedicated mcp window owns mcp controls','复制 MCP 配置' in mcp and '允许 MCP 修改当前 DWG' in mcp and 'CAD 网关就绪' in mcp)
ok('navigation list is explicitly themed','NavigationList' in styles and 'Foreground" Value="{DynamicResource TextPrimaryBrush}' in styles)
failed=[n for n,v in checks if not v]
for n,v in checks: print(('PASS' if v else 'FAIL')+': '+n)
print(f'toolbox foundation policy: {len(checks)-len(failed)}/{len(checks)} PASS')
raise SystemExit(1 if failed else 0)
