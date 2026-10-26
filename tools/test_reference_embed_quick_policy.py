from pathlib import Path
R=Path(__file__).resolve().parents[1]
service=(R/'src/YangCAD.AutoCAD/Services/Cad/ImageEmbedBatchService.cs').read_text(encoding='utf-8-sig')
registry=(R/'src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs').read_text(encoding='utf-8-sig')
ui=(R/'src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs').read_text(encoding='utf-8-sig')
xaml=(R/'src/YangCAD.AutoCAD/UI/ToolboxView.xaml').read_text(encoding='utf-8-sig')
worker=(R/'workers/ImageEmbed/Start-YangCAD-ImageEmbed.ps1').read_text(encoding='utf-8-sig')
scale=(R/'src/YangCAD.AutoCAD/Services/Cad/ScaleConversionService.cs').read_text(encoding='utf-8-sig')
checks={
    'current shortcut clickable': 'QuickEmbedCurrentClick' in xaml and 'private void QuickEmbedCurrentClick' in ui,
    'folder shortcut clickable': 'QuickEmbedFolderClick' in xaml and 'FolderBrowserDialog' in ui,
    'current unsaved edits refused': 'GetSystemVariable("DBMOD")' in ui,
    'current default input': 'currentDrawingPath ?? ""' in registry,
    'worker launch not advertised as complete': '任务已启动（尚未完成）' in ui and 'started = true' in service,
    'source not overwritten': '输出路径不能与输入 DWG 相同' in service and 'Move-Item -LiteralPath $workPath -Destination $output' in worker,
    'relative raster uses ORIGINAL dwg': 'Snapshot-Raster $entity $layoutName $input' in worker and 'Snapshot-Raster $entity $layoutName $workPath' not in worker,
    'per file exceptions isolated': "reason='per_file_exception'" in worker,
    'DWG xref bind enabled': 'BindDrawingXrefs' in service and 'bind_dwg_xrefs' in worker and '[void]$xrefBlock.Bind($false)' in worker,
    'com ownership gate': 'GetWindowThreadProcessId' in worker and '$workerOwnsAutoCad' in worker,
    'existing AutoCAD not quit': 'if ($acad -and $workerOwnsAutoCad)' in worker,
    'underlays diagnosed': 'underlay_cannot_bind_losslessly' in worker and 'unsupported_references' in worker,
    'post bind residual references counted': 'remaining_xref_count' in worker and 'remaining_nested_raster_count' in worker,
    'dynamic blocks skipped during scale': 'dynamic_block_not_verified' in scale,
    'dimension correction cannot silently fail': '无法设置对象级 DIMLFAC' in scale,
}
for name,passed in checks.items(): print(('PASS' if passed else 'FAIL')+': '+name)
if not all(checks.values()): raise SystemExit('quick reference embed static policy failed')
print(f'quick reference embed static policy: {len(checks)}/{len(checks)} PASS')
