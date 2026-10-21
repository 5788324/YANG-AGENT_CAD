from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
checks = []
def text(p): return (root / p).read_text(encoding='utf-8-sig')
def ok(name, cond): checks.append((name, bool(cond)))

shared = text('src/YangCAD.AutoCAD/UI/Themes/SharedStyles.xaml')
cc = text('src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml')
mcp = text('src/YangCAD.AutoCAD/UI/McpWindow.xaml')
toolbox = text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml')

required = {
    'WindowBackgroundBrush','SurfaceBrush','SurfaceAltBrush','BorderBrush',
    'TextPrimaryBrush','TextSecondaryBrush','AccentBrush','AccentSoftBrush',
    'AccentTextBrush','SuccessBrush','DangerBrush'
}
key_sets = []
for name in ['Obsidian','Porcelain','Mist']:
    content = text(f'src/YangCAD.AutoCAD/UI/Themes/{name}.xaml')
    keys = set(re.findall(r'x:Key="([^"]+)"', content))
    key_sets.append(keys)
    ok(f'{name} has all required theme brushes', required <= keys)
ok('three theme dictionaries have identical key sets', key_sets[0] == key_sets[1] == key_sets[2])

ok('navigation list owns themed background and foreground', 'x:Key="NavigationList"' in shared and 'x:Key="ThemeList"' in shared and 'TextPrimaryBrush' in shared)
ok('list item is explicitly themed', '<Style TargetType="ListBoxItem">' in shared and 'AccentSoftBrush' in shared)
ok('combobox items are explicitly themed', '<Style TargetType="ComboBoxItem">' in shared)
ok('separator and scrollbar are explicitly themed', '<Style TargetType="Separator">' in shared and '<Style TargetType="ScrollBar">' in shared)
ok('result text box is explicitly themed', 'x:Key="ResultTextBox"' in shared)
ok('control center nav uses NavigationList', 'x:Name="NavList" Style="{StaticResource NavigationList}"' in cc)
ok('toolbox has no legacy navigation list', 'CategoryList' not in toolbox and 'NavigationList' not in toolbox)
ok('all visible surfaces use dynamic theme background', all('DynamicResource WindowBackgroundBrush' in x for x in [cc,mcp,toolbox]))

xaml_files = list((root / 'src').rglob('*.xaml'))
parsed = True
for f in xaml_files:
    try: ET.parse(f)
    except Exception: parsed = False
ok('all XAML parses as XML', parsed and len(xaml_files) >= 8)

failed = [n for n,v in checks if not v]
for n,v in checks:
    print(('PASS' if v else 'FAIL') + ': ' + n)
print(f'ui theme policy: {len(checks)-len(failed)}/{len(checks)} PASS')
raise SystemExit(1 if failed else 0)
