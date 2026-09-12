#requires -version 3.0
<#
.SYNOPSIS
    Batch-convert Spine export files for Unity import.

.DESCRIPTION
    Recursively processes a Spine asset folder:
      *.atlas  -> *.atlas.txt
      *.skel   -> *.skel.json   if the file content is JSON
      *.skel   -> *.skel.bytes  if the file content is binary

    The script can be run from any folder:
      powershell -ExecutionPolicy Bypass -File .\process_spine_exports.ps1

    Use -WhatIf to preview operations without renaming anything.

.PARAMETER Root
    Root folder to process. Defaults to the folder containing this script.

.PARAMETER WhatIf
    Preview the files that would be renamed, but do not change anything.

.EXAMPLE
    .\process_spine_exports.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string]$Root
)

if ([string]::IsNullOrWhiteSpace($Root)) {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $Root = $PSScriptRoot
    } else {
        $Root = (Get-Location).Path
    }
}

if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    Write-Error "Root folder does not exist: $Root"
    exit 1
}

$Root = (Resolve-Path -LiteralPath $Root).Path

function Test-SpineSkeletonIsJson {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    try {
        $stream = [System.IO.File]::OpenRead($File.FullName)
        try {
            $buffer = New-Object byte[] 8192
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) {
                return $false
            }

            $index = 0

            # Skip UTF-8 BOM.
            if ($read -ge 3 -and
                $buffer[0] -eq 0xEF -and
                $buffer[1] -eq 0xBB -and
                $buffer[2] -eq 0xBF) {
                $index = 3
            }

            # Skip leading ASCII whitespace.
            while ($index -lt $read) {
                $b = $buffer[$index]
                if ($b -ne 0x20 -and $b -ne 0x09 -and $b -ne 0x0A -and $b -ne 0x0D) {
                    break
                }
                $index++
            }

            if ($index -ge $read) {
                return $false
            }

            # Spine JSON normally starts with '{'.
            return ($buffer[$index] -eq [byte]0x7B)
        } finally {
            $stream.Dispose()
        }
    } catch {
        Write-Warning "Cannot read '$($File.FullName)'. Treat it as binary."
        return $false
    }
}

function Rename-SpineFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File,

        [Parameter(Mandatory = $true)]
        [string]$NewName
    )

    $targetPath = Join-Path -Path $File.DirectoryName -ChildPath $NewName

    if (Test-Path -LiteralPath $targetPath) {
        Write-Warning "Target already exists, skipped: $targetPath"
        return $false
    }

    if ($PSCmdlet.ShouldProcess($File.FullName, "Rename to $NewName")) {
        Rename-Item -LiteralPath $File.FullName -NewName $NewName -ErrorAction Stop
        Write-Host "Renamed: $($File.Name) -> $NewName"
        return $true
    }

    Write-Host "WhatIf: $($File.Name) -> $NewName"
    return $false
}

Write-Host "Processing root: $Root"

$atlasRenamed = 0
$skelJsonRenamed = 0
$skelBinaryRenamed = 0

# 1. Atlas: xxx.atlas -> xxx.atlas.txt
$atlasFiles = Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -ieq '.atlas' }

foreach ($file in $atlasFiles) {
    if (Rename-SpineFile -File $file -NewName ($file.Name + '.txt')) {
        $atlasRenamed++
    }
}

# 2. Skeleton: detect JSON or binary.
$skelFiles = Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -ieq '.skel' }

foreach ($file in $skelFiles) {
    if (Test-SpineSkeletonIsJson -File $file) {
        if (Rename-SpineFile -File $file -NewName ($file.Name + '.json')) {
            $skelJsonRenamed++
        }
    } else {
        if (Rename-SpineFile -File $file -NewName ($file.Name + '.bytes')) {
            $skelBinaryRenamed++
        }
    }
}

Write-Host ""
Write-Host "Done."
Write-Host "  atlas -> .atlas.txt : $atlasRenamed"
Write-Host "  skel  -> .skel.json : $skelJsonRenamed"
Write-Host "  skel  -> .skel.bytes: $skelBinaryRenamed"
