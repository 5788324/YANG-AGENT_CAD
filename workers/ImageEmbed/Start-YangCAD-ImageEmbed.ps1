param(
    [Parameter(Mandatory=$true)][string]$JobPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
# AutoCAD COM may return an existing session instead of a new process.
# Never operate on/quit an AutoCAD process that was already running when the job began.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class YangCadWindowOwner {
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
'@

function Write-Status([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 12
    $tmp = $Path + '.tmp.' + [Guid]::NewGuid().ToString('N')
    [System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
    try {
        if (Test-Path -LiteralPath $Path) {
            try { [System.IO.File]::Replace($tmp, $Path, $null) } catch { [System.IO.File]::Copy($tmp, $Path, $true) }
        }
        else {
            [System.IO.File]::Move($tmp, $Path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Wait-Quiescent($Acad, [int]$TimeoutSeconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
        try {
            $state = $Acad.GetAcadState()
            if ($state -and $state.IsQuiescent) {
                try { if ($null -ne $Acad.Documents) { return } } catch { }
            }
        }
        catch { }
    }
    throw "AutoCAD command did not become quiescent within $TimeoutSeconds seconds."
}

function Get-CollectionHandles($Collection, [string]$ObjectName) {
    $result = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($item in @($Collection)) {
        try {
            if ([string]$item.ObjectName -eq $ObjectName) { [void]$result.Add([string]$item.Handle) }
        }
        catch { }
    }
    return ,$result
}

function Find-ByHandle($Collection, [string]$Handle) {
    foreach ($item in @($Collection)) {
        try { if ([string]$item.Handle -eq $Handle) { return $item } } catch { }
    }
    return $null
}

function Resolve-ImagePath([string]$ImageFile, [string]$DwgPath) {
    if ([string]::IsNullOrWhiteSpace($ImageFile)) { return $null }
    if ([System.IO.Path]::IsPathRooted($ImageFile)) { return [System.IO.Path]::GetFullPath($ImageFile) }
    return [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetDirectoryName($DwgPath)) $ImageFile))
}

function Snapshot-Raster($Raster, [string]$LayoutName, [string]$DwgPath) {
    $imageFile = [string]$Raster.ImageFile
    $resolved = Resolve-ImagePath $imageFile $DwgPath
    $origin = @($Raster.Origin)
    return [ordered]@{
        handle = [string]$Raster.Handle
        layout = $LayoutName
        image_file = $imageFile
        resolved_path = $resolved
        origin = @([double]$origin[0], [double]$origin[1], [double]$origin[2])
        width = [double]$Raster.ImageWidth
        height = [double]$Raster.ImageHeight
        rotation = [double]$Raster.Rotation
        layer = [string]$Raster.Layer
        clipping_enabled = [bool]$Raster.ClippingEnabled
        brightness = [int]$Raster.Brightness
        contrast = [int]$Raster.Contrast
        fade = [int]$Raster.Fade
        transparency = [bool]$Raster.Transparency
    }
}

function Set-ActiveLayout($Doc, [string]$LayoutName) {
    if (-not [string]::IsNullOrWhiteSpace($LayoutName)) {
        try {
            $layout = $Doc.Layouts.Item($LayoutName)
            if ($layout) {
                if ([bool]$layout.ModelType) {
                    $Doc.ActiveSpace = 1
                }
                else {
                    $Doc.ActiveSpace = 0
                    try { $Doc.MSpace = $false } catch { }
                    try { $Doc.ActiveLayout = $layout } catch { }
                }
                return $layout
            }
        }
        catch { }
    }

    for ($i = 0; $i -lt $Doc.Layouts.Count; $i++) {
        $layout = $Doc.Layouts.Item($i)
        $isModel = [bool]$layout.ModelType
        $match = $false
        if (-not [string]::IsNullOrWhiteSpace($LayoutName)) {
            if ([string]::Equals([string]$layout.Name, $LayoutName, [System.StringComparison]::OrdinalIgnoreCase)) {
                $match = $true
            }
            elseif ($isModel -and ($LayoutName -eq 'Model' -or $LayoutName -eq '模型')) {
                $match = $true
            }
        }
        elseif ($isModel) {
            $match = $true
        }

        if ($match) {
            if ($isModel) {
                $Doc.ActiveSpace = 1
            }
            else {
                $Doc.ActiveSpace = 0
                try { $Doc.MSpace = $false } catch { }
                try { $Doc.ActiveLayout = $layout } catch { }
            }
            return $layout
        }
    }

    # Never fall back to another layout. Wrong-space OLE placement is worse than a per-image failure.
    throw "Layout not found: $LayoutName"
}

function Convert-Raster($Acad, $Doc, $Layout, $RasterInfo, [int]$TimeoutSeconds) {
    if (-not (Test-Path -LiteralPath $RasterInfo.resolved_path)) {
        return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='missing_image_file'; path=$RasterInfo.resolved_path }
    }
    if ($RasterInfo.clipping_enabled) {
        return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='clipped_image_not_supported_v1'; path=$RasterInfo.resolved_path }
    }
    if ($RasterInfo.brightness -ne 50 -or $RasterInfo.contrast -ne 50 -or $RasterInfo.fade -ne 0) {
        return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='visual_adjustments_not_supported_v1'; path=$RasterInfo.resolved_path; brightness=$RasterInfo.brightness; contrast=$RasterInfo.contrast; fade=$RasterInfo.fade }
    }
    if ($RasterInfo.transparency) {
        return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='raster_transparency_not_supported_v1'; path=$RasterInfo.resolved_path }
    }

    $layoutObj = Set-ActiveLayout $Doc $RasterInfo.layout
    $block = $layoutObj.Block
    $before = Get-CollectionHandles $block 'AcDbOle2Frame'

    $image = $null
    $bitmap = $null
    try {
        $image = [System.Drawing.Image]::FromFile($RasterInfo.resolved_path)
        $bitmap = New-Object System.Drawing.Bitmap($image)
        [System.Windows.Forms.Clipboard]::SetImage($bitmap)

        $x = ([double]$RasterInfo.origin[0]).ToString('0.###############', [System.Globalization.CultureInfo]::InvariantCulture)
        $y = ([double]$RasterInfo.origin[1]).ToString('0.###############', [System.Globalization.CultureInfo]::InvariantCulture)
        # Wait for quiescent state before sending command
        Wait-Quiescent $Acad $TimeoutSeconds
        Start-Sleep -Milliseconds 250
        $pasted = $false
        for ($r = 0; $r -lt 5 -and -not $pasted; $r++) {
            try {
                $Doc.SendCommand("_.PASTECLIP $x,$y`n1`n0`n")
                $pasted = $true
            }
            catch [System.Runtime.InteropServices.COMException] {
                Start-Sleep -Milliseconds 500
            }
        }
        if (-not $pasted) {
            $Doc.SendCommand("_.PASTECLIP $x,$y`n1`n0`n")
        }
        Wait-Quiescent $Acad $TimeoutSeconds

        $after = Get-CollectionHandles $block 'AcDbOle2Frame'
        $newHandles = @()
        if ($null -ne $after) {
            if ($null -eq $before) {
                $newHandles = @($after)
            }
            else {
                $newHandles = @($after | Where-Object { -not $before.Contains($_) })
            }
        }
        if ($newHandles.Count -ne 1) {
            # A failed per-image conversion must not leave stray OLE frames behind when
            # require_all_images=false and the DWG is allowed to become PARTIAL.
            foreach ($newHandle in $newHandles) {
                $stray = Find-ByHandle $block $newHandle
                if ($stray) { try { $stray.Delete() } catch { } }
            }
            return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='ole_delta_not_unique'; new_ole_count=$newHandles.Count }
        }

        $ole = Find-ByHandle $block $newHandles[0]
        if ($null -eq $ole) {
            return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='new_ole_not_found' }
        }

        try { $ole.LockAspectRatio = $false } catch { }
        $ole.Width = [double]$RasterInfo.width
        $ole.Height = [double]$RasterInfo.height
        $ole.Rotation = [double]$RasterInfo.rotation
        $ole.InsertionPoint = [double[]]@([double]$RasterInfo.origin[0], [double]$RasterInfo.origin[1], [double]$RasterInfo.origin[2])
        $ole.Layer = [string]$RasterInfo.layer
        [void]$ole.Update()

        $raster = Find-ByHandle $block $RasterInfo.handle
        if ($null -eq $raster) {
            try { [void]$ole.Delete() } catch { }
            return [ordered]@{ ok=$false; handle=$RasterInfo.handle; reason='source_raster_disappeared_before_delete' }
        }
        [void]$raster.Delete()
        return [ordered]@{ ok=$true; handle=$RasterInfo.handle; ole_handle=[string]$ole.Handle; layout=$RasterInfo.layout; path=$RasterInfo.resolved_path }
    }
    finally {
        try { [System.Windows.Forms.Clipboard]::Clear() } catch { }
        if ($bitmap) { $bitmap.Dispose() }
        if ($image) { $image.Dispose() }
    }
}

function Process-Drawing($Acad, $Item, $Job) {
    $input = [System.IO.Path]::GetFullPath([string]$Item.input_path)
    $output = [System.IO.Path]::GetFullPath([string]$Item.output_path)
    $sourceBefore = Get-Sha256 $input
    if (-not $sourceBefore) { throw "Input DWG not found: $input" }
    if (Test-Path -LiteralPath $output) {
        return [ordered]@{ status='SKIPPED'; reason='output_exists'; input_path=$input; output_path=$output; source_sha_before=$sourceBefore; source_sha_after=(Get-Sha256 $input) }
    }

    $outDir = [System.IO.Path]::GetDirectoryName($output)
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $workPath = Join-Path $outDir (([System.IO.Path]::GetFileNameWithoutExtension($output)) + '.__yangcad_work_' + [Guid]::NewGuid().ToString('N') + '.dwg')
    Copy-Item -LiteralPath $input -Destination $workPath -Force

    $doc = $null
    $publishedOutput = $false
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds([int]$Job.command_timeout_seconds)
        while ([DateTime]::UtcNow -lt $deadline -and $null -eq $doc) {
            try {
                if ($null -ne $Acad.Documents) {
                    $doc = $Acad.Documents.Open($workPath)
                }
            } catch {
                Start-Sleep -Milliseconds 500
            }
            if ($null -eq $doc) { Start-Sleep -Milliseconds 250 }
        }
        if ($null -eq $doc) {
            $doc = $Acad.Documents.Open($workPath)
        }
        Wait-Quiescent $Acad ([int]$Job.command_timeout_seconds)

        $rasters = New-Object System.Collections.Generic.List[object]
        $snapshotFailures = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $doc.Layouts.Count; $i++) {
            $layout = $doc.Layouts.Item($i)
            $layoutName = [string]$layout.Name
            $block = $layout.Block
            foreach ($entity in @($block)) {
                try {
                    if ([string]$entity.ObjectName -eq 'AcDbRasterImage') {
                        try {
                            [void]$rasters.Add((Snapshot-Raster $entity $layoutName $input))
                        }
                        catch {
                            $handle = $null
                            try { $handle = [string]$entity.Handle } catch { }
                            [void]$snapshotFailures.Add([ordered]@{
                                ok = $false
                                handle = $handle
                                layout = $layoutName
                                reason = 'raster_snapshot_failed'
                                error = $_.Exception.Message
                            })
                        }
                    }
                }
                catch { }
            }
        }

        # Underlays have no native lossless Bind equivalent: report, never detach/drop.
        $underlayReferences = New-Object System.Collections.Generic.List[object]
        for ($li = 0; $li -lt $doc.Layouts.Count; $li++) {
            $layoutScan = $doc.Layouts.Item($li)
            foreach ($entity in @($layoutScan.Block)) {
                try {
                    $typeName = [string]$entity.ObjectName
                    if ($typeName -match 'PdfReference|DgnReference|DwfReference|Underlay') {
                        [void]$underlayReferences.Add([ordered]@{
                            kind = $typeName; handle = [string]$entity.Handle;
                            layout = [string]$layoutScan.Name; reason = 'underlay_cannot_bind_losslessly'
                        })
                    }
                } catch { }
            }
        }
        $nestedRasterCount = 0
        $xrefBlockCount = 0
        foreach ($block in @($doc.Blocks)) {
            try {
                if ($block.IsXRef) { $xrefBlockCount++; continue }
                if ($block.IsLayout) { continue }
                foreach ($entity in @($block)) {
                    try { if ([string]$entity.ObjectName -eq 'AcDbRasterImage') { $nestedRasterCount++ } } catch { }
                }
            }
            catch { }
        }

        if ($rasters.Count -eq 0 -and $snapshotFailures.Count -eq 0 -and
            ((-not [bool]$Job.bind_dwg_xrefs) -or $xrefBlockCount -eq 0)) {
            $doc.Close($false)
            $doc = $null
            Remove-Item -LiteralPath $workPath -Force -ErrorAction SilentlyContinue
            $emptyStatus = if ($nestedRasterCount -gt 0 -or $underlayReferences.Count -gt 0) { 'PARTIAL' } else { 'SKIPPED' }
            return [ordered]@{
                status=$emptyStatus;
                reason='no_convertible_references'; input_path=$input; output_path=$output;
                source_sha_before=$sourceBefore; source_sha_after=(Get-Sha256 $input); nested_raster_count=$nestedRasterCount;
                xref_block_count=$xrefBlockCount; unsupported_references=$underlayReferences.ToArray()
            }
        }

        $results = New-Object System.Collections.Generic.List[object]
        foreach ($snapshotFailure in $snapshotFailures) { [void]$results.Add($snapshotFailure) }
        foreach ($rasterInfo in $rasters) {
            [void]$results.Add((Convert-Raster $Acad $doc $null $rasterInfo ([int]$Job.command_timeout_seconds)))
        }
        $failed = @($results | Where-Object { -not $_.ok })
        $boundXrefs = New-Object System.Collections.Generic.List[string]
        $failedXrefs = New-Object System.Collections.Generic.List[object]
        if ([bool]$Job.bind_dwg_xrefs) {
            # Snapshot names before Bind() mutates the Blocks collection.
            $xrefNames = New-Object System.Collections.Generic.List[string]
            foreach ($candidateBlock in @($doc.Blocks)) {
                try { if ([bool]$candidateBlock.IsXRef) { [void]$xrefNames.Add([string]$candidateBlock.Name) } } catch { }
            }
            foreach ($xrefName in $xrefNames) {
                try {
                    $xrefBlock = $doc.Blocks.Item($xrefName)
                    if (-not [bool]$xrefBlock.IsXRef) { continue }
                    # Temporary output DWG is in a different folder: repair resolvable relative
                    # Xref paths against the original DWG before binding the copied database.
                    try {
                        $savedXrefPath = [string]$xrefBlock.Path
                        if ($savedXrefPath -and -not [System.IO.Path]::IsPathRooted($savedXrefPath)) {
                            $resolvedXrefPath = Resolve-ImagePath $savedXrefPath $input
                            if (Test-Path -LiteralPath $resolvedXrefPath) {
                                $xrefBlock.Path = $resolvedXrefPath
                            }
                        }
                    } catch { }
                    # COM Bind(false) retains name prefixes to avoid style/layer collisions.
                    [void]$xrefBlock.Bind($false)
                    if ([bool]$doc.Blocks.Item($xrefName).IsXRef) {
                        throw "Bind() returned without error but the block remains an external reference."
                    }
                    [void]$boundXrefs.Add($xrefName)
                }
                catch {
                    [void]$failedXrefs.Add([ordered]@{ name=$xrefName; reason='xref_bind_failed'; error=$_.Exception.Message })
                }
            }
        }
        if ([bool]$Job.require_all_images -and ($failed.Count -gt 0 -or $nestedRasterCount -gt 0)) {
            $failJson = $failed | ConvertTo-Json -Compress -Depth 4
            throw "Image embedding incomplete: $($failed.Count) top-level image(s) failed ($failJson) and $nestedRasterCount nested block image(s) are unsupported; source raster(s) are preserved in the discarded working copy."
        }

        # Bind can introduce nested raster/underlay references from an Xref.
        # Count them after binding; never claim the output is fully self-contained if they remain.
        $remainingXrefCount = 0
        $remainingNestedRasterCount = 0
        foreach ($checkBlock in @($doc.Blocks)) {
            try {
                if ([bool]$checkBlock.IsXRef) { $remainingXrefCount++; continue }
                if ([bool]$checkBlock.IsLayout) { continue }
                foreach ($entity in @($checkBlock)) {
                    try { if ([string]$entity.ObjectName -eq 'AcDbRasterImage') { $remainingNestedRasterCount++ } } catch { }
                }
            } catch { }
        }
        $doc.Save()
        $doc.Close($false)
        $doc = $null

        # Verify the source before publishing the finished DWG. If another process
        # changed the input while this worker was running, discard the working copy.
        $sourceAfter = Get-Sha256 $input
        if ($sourceAfter -ne $sourceBefore) {
            throw "Input SHA changed unexpectedly before output publication: $input"
        }

        # Deliberately omit -Force: a destination created after the initial check is
        # a race and must cause FAIL rather than overwrite someone else's file.
        Move-Item -LiteralPath $workPath -Destination $output
        $publishedOutput = $true

        $sourceFinal = Get-Sha256 $input
        if ($sourceFinal -ne $sourceBefore) {
            Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
            $publishedOutput = $false
            throw "Input SHA changed unexpectedly during output publication: $input"
        }
        $sourceAfter = $sourceFinal
        $embeddedCount = 0
        $failedCount = 0
        foreach ($res in $results) {
            try {
                if ($res.ok) { $embeddedCount++ } else { $failedCount++ }
            } catch { $failedCount++ }
        }
        $statusStr = if ($failedCount -eq 0 -and $nestedRasterCount -eq 0 -and
            $failedXrefs.Count -eq 0 -and $underlayReferences.Count -eq 0 -and
            $remainingXrefCount -eq 0 -and $remainingNestedRasterCount -eq 0) { 'PASS' } else { 'PARTIAL' }
        return [pscustomobject]@{
            status = $statusStr
            input_path = [string]$input
            output_path = [string]$output
            source_sha_before = [string]$sourceBefore
            source_sha_after = [string]$sourceAfter
            raster_count = [int]($rasters.Count + $snapshotFailures.Count)
            embedded_count = [int]$embeddedCount
            failed_count = [int]$failedCount
            nested_raster_count = [int]$nestedRasterCount
            xref_block_count = [int]$xrefBlockCount
            bound_xref_count = [int]$boundXrefs.Count
            bound_xrefs = $boundXrefs.ToArray()
            failed_xrefs = $failedXrefs.ToArray()
            remaining_xref_count = [int]$remainingXrefCount
            remaining_nested_raster_count = [int]$remainingNestedRasterCount
            unsupported_references = $underlayReferences.ToArray()
            image_results = @($results.ToArray())
        }
    }
    catch {
        if ($doc) { try { $doc.Close($false) } catch { } }
        if (Test-Path -LiteralPath $workPath) { Remove-Item -LiteralPath $workPath -Force -ErrorAction SilentlyContinue }
        if ($publishedOutput -and (Test-Path -LiteralPath $output)) {
            Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
            $publishedOutput = $false
        }
        $sourceAfter = Get-Sha256 $input
        return [ordered]@{
            status = 'FAIL'
            input_path = $input
            output_path = $output
            source_sha_before = $sourceBefore
            source_sha_after = $sourceAfter
            error = "$($_.Exception.Message) at $($_.ScriptStackTrace)"
        }
    }
}

if (-not (Test-Path -LiteralPath $JobPath)) { throw "Job file not found: $JobPath" }
$job = Get-Content -LiteralPath $JobPath -Raw -Encoding UTF8 | ConvertFrom-Json
$statusPath = [string]$job.status_path
$logPath = [string]$job.log_path
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($statusPath)) | Out-Null

$status = [ordered]@{
    schema_version = 'yangcad-image-embed-job/1.0'
    job_id = [string]$job.job_id
    state = 'STARTING'
    started_at = [DateTime]::UtcNow.ToString('o')
    completed_at = $null
    worker_pid = $PID
    autocad_progid = [string]$job.autocad_progid
    total_files = @($job.items).Count
    passed = 0
    failed = 0
    partial = 0
    skipped = 0
    items = @()
}
Write-Status $statusPath $status

$acad = $null
$workerOwnsAutoCad = $false
$existingAcadPids = New-Object System.Collections.Generic.HashSet[int]
foreach ($proc in (Get-Process -Name acad -ErrorAction SilentlyContinue)) { [void]$existingAcadPids.Add([int]$proc.Id) }
try {
    $acad = New-Object -ComObject ([string]$job.autocad_progid)
    $acadHwnd = [IntPtr]([long]$acad.HWND)
    [uint32]$ownerPid = 0
    [void][YangCadWindowOwner]::GetWindowThreadProcessId($acadHwnd, [ref]$ownerPid)
    if ($ownerPid -eq 0 -or $existingAcadPids.Contains([int]$ownerPid)) {
        throw "AutoCAD COM returned an existing or unidentifiable host; refusing to modify/quit a user session. PID=$ownerPid"
    }
    $workerOwnsAutoCad = $true
    $status.worker_autocad_pid = [int]$ownerPid
    $acad.Visible = [bool]$job.show_worker
    Wait-Quiescent $acad ([int]$job.command_timeout_seconds)
    $status.state = 'RUNNING'
    Write-Status $statusPath $status

    foreach ($item in @($job.items)) {
        try {
            $result = Process-Drawing $acad $item $job
        }
        catch {
            $result = [ordered]@{
                status='FAIL'; input_path=[string]$item.input_path;
                output_path=[string]$item.output_path; reason='per_file_exception';
                error=$_.Exception.Message
            }
        }
        $status.items += $result
        switch ([string]$result.status) {
            'PASS' { $status.passed++ }
            'PARTIAL' { $status.partial++ }
            'SKIPPED' { $status.skipped++ }
            default { $status.failed++ }
        }
        Write-Status $statusPath $status
    }
    $status.state = if ($status.failed -eq 0 -and $status.partial -eq 0) { 'PASS' } elseif ($status.passed -gt 0 -or $status.partial -gt 0) { 'PARTIAL' } else { 'FAIL' }
}
catch {
    $status.state = 'FAIL'
    $status.fatal_error = $_.Exception.Message
}
finally {
    if ($acad -and $workerOwnsAutoCad) { try { $acad.Quit() } catch { } }
    $status.completed_at = [DateTime]::UtcNow.ToString('o')
    Write-Status $statusPath $status
    try { [System.IO.File]::AppendAllText($logPath, (($status | ConvertTo-Json -Depth 12) + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false))) } catch { }
}

if ($status.state -eq 'PASS') { exit 0 }
if ($status.state -eq 'PARTIAL') { exit 3 }
exit 1
