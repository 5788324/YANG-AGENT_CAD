#!/usr/bin/env python3
"""Source-level guard only. Native Windows/AutoCAD PGP/CUI and Ribbon activation need runtime smoke."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
paths = {
    'service': 'src/YangCAD.AutoCAD/Services/Cad/ShortcutAliasService.cs',
    'window': 'src/YangCAD.AutoCAD/UI/ShortcutManagerWindow.xaml',
    'window_code': 'src/YangCAD.AutoCAD/UI/ShortcutManagerWindow.xaml.cs',
    'ribbon': 'src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs',
    'commands': 'src/YangCAD.AutoCAD/Commands/ControlCenterCommands.cs',
    'control_xaml': 'src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml',
    'control_cs': 'src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml.cs',
}
s = {name: (root/path).read_text(encoding='utf-8-sig') for name,path in paths.items()}
checks = [
 ('PGP resolved through Autodesk profile', 'HostApplicationServices.Current.FindFile("acad.pgp"' in s['service']),
 ('PGP path must exist', 'File.Exists(path)' in s['service']),
 ('strict alias grammar', 'AliasInput.IsMatch(value)' in s['service']),
 ('feature catalog explicitly enumerated', 'public static IReadOnlyList<Feature> Features' in s['service']),
 ('no generic command injection via alias', '不允许给未知命令' in s['service']),
 ('own PGP block markers', 'YangCAD managed aliases BEGIN' in s['service'] and 'YangCAD managed aliases END' in s['service']),
 ('collision check with CAD aliases', 'CollisionMessage' in s['service'] and 'ParseAliases(outside)' in s['service']),
 ('override requires explicit user choice', 'allowExistingOverride' in s['service'] and 'OverrideBox.IsChecked == true' in s['window_code']),
 ('backup prior to overwriting PGP', 'File.Copy(path, backup, false)' in s['service']),
 ('atomic PGP replacement', 'File.Replace(temp, path, null)' in s['service']),
 ('remove only managed assignment', 'ReadManaged(data.Text)' in s['service'] and 'ReplaceManaged(pgpPath, data, aliases)' in s['service']),
 ('read legacy ANSI or UTF8/UTF16', 'Encoding.GetEncoding(28591)' in s['service'] and 'DecoderFallbackException' in s['service']),
 ('modeless CAD shortcut command registered', '[CommandMethod("YGKEYS")]' in s['commands']),
 ('shortcut manager has CAD feature dropdown', 'FeatureBox' in s['window'] and 'ShortcutAliasService.Features' in s['window_code']),
 ('shortcut manager supports remove', 'RemoveClick' in s['window'] and 'RemoveClick' in s['window_code']),
 ('CAD PGP reload requested by REINIT', '_.REINIT ' in s['window_code']),
 ('native CUI editor for Ctrl Shift', '_.CUI ' in s['window_code']),
 ('no global hotkey interception', not any(x in s['service']+s['window_code'] for x in ['RegisterHotKey(', 'SetWindowsHookEx(', 'GetAsyncKeyState('])),
 ('control center navigation shortcut page', 'Tag="Shortcuts"' in s['control_xaml'] and 'ShortcutsPage.Visibility' in s['control_cs']),
 ('control center opens shortcut command', 'YGKEYS ' in s['control_cs']),
 ('ribbon preserves prior active tab', 'var previousTab = ribbon.ActiveTab' in s['ribbon']),
 ('ribbon restores tab after insertion', 'ribbon.ActiveTab = restoreTab' in s['ribbon']),
 ('ribbon does not force YangCAD active', 'ribbon.ActiveTab = newTab' not in s['ribbon']),
 ('ribbon preserves native home fallback', '"常用"' in s['ribbon'] and '"Home"' in s['ribbon']),
 ('XAML parses', all(ET.parse(root/paths[k]) for k in ('window','control_xaml'))),
]
failures = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ': ' + name)
print('shortcuts/ribbon static policy: %d/%d PASS' % (len(checks)-len(failures),len(checks)))
if failures:
    raise SystemExit(1)
