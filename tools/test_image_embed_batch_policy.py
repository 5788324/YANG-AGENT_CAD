from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVICE = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/ImageEmbedBatchService.cs").read_text(encoding="utf-8-sig")
REGISTRY = (ROOT / "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs").read_text(encoding="utf-8-sig")
WORKER_PATH = ROOT / "workers/ImageEmbed/Start-YangCAD-ImageEmbed.ps1"
WORKER = WORKER_PATH.read_text(encoding="utf-8-sig")
PACKAGE = (ROOT / "tools/Package-Bundle.ps1").read_text(encoding="utf-8-sig")
VERIFY = (ROOT / "tools/Verify-Release.ps1").read_text(encoding="utf-8-sig")
CSPROJ = (ROOT / "src/YangCAD.AutoCAD/YangCAD.AutoCAD.csproj").read_text(encoding="utf-8-sig")

checks = []
def ok(name, cond): checks.append((name, bool(cond)))

segment = REGISTRY[REGISTRY.find('Id = "project.image.embed"'):]
ok("single image embed toolbox tool", 'Id = "project.image.embed"' in REGISTRY and 'Name = "批量图片内嵌"' in REGISTRY)
ok("tool is preview batch output-copy", 'IsWrite = true' in segment and 'SupportsDryRun = true' in segment and 'SupportsBatch = true' in segment)
ok("tool has product MCP coverage", 'SupportsMcp = true' in segment and 'project.image.embed' in (ROOT/'mcp/yangcad_mcp/server.py').read_text(encoding='utf-8-sig'))
ok("service exposes stable shared Request/Result", 'public sealed class Request' in SERVICE and 'public sealed class Result' in SERVICE and 'public static Result Preview' in SERVICE and 'public static Result Start' in SERVICE)
ok("service does not access active cad document", 'MdiActiveDocument' not in SERVICE and 'DocumentManager' not in SERVICE)
ok("preview does not create output folder", 'BuildPlan(request, createOutputDirectory: false)' in SERVICE and 'if (createOutputDirectory) Directory.CreateDirectory(outputRoot)' in SERVICE)
ok("execute uses independent STA powershell worker", 'powershell.exe' in SERVICE and '-Sta' in SERVICE and 'Start-YangCAD-ImageEmbed.ps1' in SERVICE and 'Process.Start' in SERVICE)
ok("worker uses exact-year autocad progid", all(x in SERVICE for x in ['AutoCAD.Application.24.0','AutoCAD.Application.24.1','AutoCAD.Application.24.2','AutoCAD.Application.24.3']))
ok("input overwrite is refused", '本工具永不原位覆盖源文件' in SERVICE and 'overwrite = false' in SERVICE)
ok("batch is bounded", 'files.Length > 5000' in SERVICE)
ok("job/status are user-local", 'YangCADAgent", "jobs", "image-embed"' in SERVICE and 'status.json' in SERVICE)
ok("worker output is working-copy then no-overwrite publish", '.__yangcad_work_' in WORKER and 'Copy-Item -LiteralPath $input -Destination $workPath' in WORKER and 'Move-Item -LiteralPath $workPath -Destination $output' in WORKER and 'Move-Item -LiteralPath $workPath -Destination $output -Force' not in WORKER)
ok("source sha is verified before and after output publication", 'source_sha_before' in WORKER and 'source_sha_after' in WORKER and 'before output publication' in WORKER and 'during output publication' in WORKER)
ok("worker embeds via clipboard pasteclip", 'Clipboard]::SetImage' in WORKER and '_.PASTECLIP' in WORKER)
ok("paper/model paste context is explicit", '$Doc.ActiveSpace = 1' in WORKER and '$Doc.ActiveSpace = 0' in WORKER and '$Doc.MSpace = $false' in WORKER and '$layout.ModelType' in WORKER)
ok("status updates use replace instead of delete gap", '[System.IO.File]::Replace($tmp, $Path, $null)' in WORKER and 'Remove-Item -LiteralPath $Path -Force' not in WORKER)
ok("raster snapshot failures cannot become no-image skip", 'snapshotFailures' in WORKER and 'raster_snapshot_failed' in WORKER)
ok("new ole is identified by delta and stray delta is cleaned", "Get-CollectionHandles $block 'AcDbOle2Frame'" in WORKER and 'ole_delta_not_unique' in WORKER and '$stray.Delete()' in WORKER)
ok("source raster deletes only after new ole normalized", '$ole.Width' in WORKER and '$ole.Height' in WORKER and '$ole.Rotation' in WORKER and '$raster.Delete()' in WORKER)
ok("unsafe raster visuals are refused", all(x in WORKER for x in ['clipped_image_not_supported_v1','visual_adjustments_not_supported_v1','raster_transparency_not_supported_v1']))
ok("nested block raster is not falsely claimed embedded", 'nestedRasterCount' in WORKER and 'nested block image(s) are unsupported' in WORKER)
ok("drawings without raster images are skipped without output copy", "no_top_level_raster_images" in WORKER and "$rasters.Count -eq 0 -and $snapshotFailures.Count -eq 0 -and $nestedRasterCount -eq 0" in WORKER)
ok("xref presence is reported separately", "xrefBlockCount" in WORKER and "xref_block_count" in WORKER)
ok("require all images discards partial dwg", 'Image embedding incomplete' in WORKER and 'Remove-Item -LiteralPath $workPath' in WORKER)
ok("release bundle packages worker", 'Contents\\ImageEmbed' in PACKAGE and 'Start-YangCAD-ImageEmbed.ps1' in PACKAGE)
ok("release verification requires worker", 'Contents\\ImageEmbed\\Start-YangCAD-ImageEmbed.ps1' in VERIFY)
ok("worker also embedded for dll-only fallback", 'YangCAD.Runtime.ImageEmbed.Start-YangCAD-ImageEmbed.ps1' in CSPROJ)
ok("powershell 5.1 generic-list result serialization is explicit", 'return [pscustomobject]@{' in WORKER and 'image_results = @($results.ToArray())' in WORKER)
ok("handle hashset is returned without pipeline enumeration", 'return ,$result' in WORKER)
ok("layout lookup fails closed instead of silently falling back", 'Never fall back to another layout' in WORKER and 'throw "Layout not found: $LayoutName"' in WORKER)
ok("OLE normalization remains strict", '$ole.Layer = [string]$RasterInfo.layer' in WORKER and '[void]$ole.Update()' in WORKER and '[void]$raster.Delete()' in WORKER)

failed=[n for n,v in checks if not v]
for n,v in checks: print(("PASS" if v else "FAIL")+": "+n)
if failed: raise SystemExit("image embed batch policy failed: "+", ".join(failed))
print(f"image embed batch policy: {len(checks)}/{len(checks)} PASS")
