from pathlib import Path
root=Path(__file__).resolve().parents[1]
checks=[]
def text(p): return (root/p).read_text(encoding='utf-8-sig')
def ok(name,cond): checks.append((name,bool(cond)))
view=text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs')
xaml=text('src/YangCAD.AutoCAD/UI/ToolboxView.xaml')
panel=text('src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml.cs')
panel_xaml=text('src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml')
registry=text('src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs')
ext=text('src/YangCAD.AutoCAD/ExtensionApplication.cs')

kinds=['Text','Number','Bool','Choice','Layer','ObjectSelection','Point','File','Folder','FileOrFolder']
ok('parameter panel is separate WPF control','ToolboxParameterPanel' in panel_xaml and '<local:ToolboxParameterPanel x:Name="ParameterPanel"' in xaml)
ok('all parameter kinds handled',all(f'ToolboxParameterKind.{k}' in panel for k in kinds))
ok('shape validation required and range','shape.Required' in panel and 'shape.Min.HasValue' in panel and 'shape.Max.HasValue' in panel)
ok('choice validates allowed options','shape.Options.Contains(selected)' in panel)
ok('file and folder browse are native','OpenFileDialog' in panel and 'FolderBrowserDialog' in panel and 'BrowseFile' in panel and 'BrowseFolder' in panel)
ok('file-or-folder existence validated','FileOrFolder' in panel and '!File.Exists(expanded) && !Directory.Exists(expanded)' in panel)
ok('output folders may be newly typed','case ToolboxParameterKind.Folder:' in panel and 'shape.Kind == ToolboxParameterKind.FileOrFolder' in panel and 'shape.Kind == ToolboxParameterKind.Folder && !Directory.Exists' not in panel)
ok('runtime unavailable is visible and execute disabled','当前不可用' in view and 'ExecuteButton.IsEnabled = canExecute' in view and 'AvailabilityText.Text' in view)
ok('execution result and error stay in palette','ShowResult(true' in view and 'ShowResult(false' in view and 'ResultBorder.Visibility' in view)
ok('results can be copied','CopyResultClick' in view and 'Clipboard.SetText(ResultText.Text)' in view)
ok('favorites/recent/backlog ui removed',all(x not in view+xaml for x in ['ToggleFavorite','RecordRecent','ToolboxFavorites','ToolboxRecent','CategoryChanged','SearchChanged','FavoriteButton']))
ok('only two descriptors remain',registry.count('Id = "project.')==2 and 'system.' not in registry and 'cad.drawing.audit' not in registry)
ok('document activation refreshes availability','ToolboxPaletteManager.RefreshContext()' in ext)
failed=[n for n,v in checks if not v]
for n,v in checks: print(('PASS' if v else 'FAIL')+': '+n)
print(f'toolbox interaction policy: {len(checks)-len(failed)}/{len(checks)} PASS')
raise SystemExit(1 if failed else 0)
