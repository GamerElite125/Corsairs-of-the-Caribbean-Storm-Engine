# Builds a patch zip for the Corsairs of the Caribbean launcher.
# Includes every game file changed since a date (or listed in a text file), with paths
# relative to the game folder, so the launcher can install it.
#
# Examples (run in PowerShell from anywhere):
#   .\make_patch.ps1 -Version 1.1.1 -Since "2026-11-01 12:00"
#   .\make_patch.ps1 -Version 1.1.2 -FileList changed.txt
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Since,
    [string]$FileList,
    [string]$Game
)
# $PSScriptRoot is not available inside param() defaults in Windows PowerShell 5, so resolve here
if (-not $Game) { $Game = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\..")).Path }
if (-not (Test-Path (Join-Path $Game "engine.exe"))) { throw "Not a game folder: $Game" }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$skip = '\\(SAVE|Docs_AND_Report|launcher_assets\\cache)\\|\\options$|\\options_start$|\\engine\.ini|\\cotc_version\.txt$|\.log$'
if ($FileList) {
    $files = Get-Content $FileList | Where-Object { $_.Trim() } | ForEach-Object { Join-Path $Game $_.Trim() }
} elseif ($Since) {
    $t = [datetime]$Since
    $files = Get-ChildItem "$Game\Program", "$Game\RESOURCE" -Recurse -File | Where-Object { $_.LastWriteTime -gt $t } | ForEach-Object FullName
    $files += Get-ChildItem $Game -File -Filter *.exe | Where-Object { $_.LastWriteTime -gt $t } | ForEach-Object FullName
} else { throw "Give -Since or -FileList" }
$files = $files | Where-Object { (Test-Path $_) -and ($_ -notmatch $skip) }

$out = Join-Path $PSScriptRoot "patch-v$Version.zip"
if (Test-Path $out) { Remove-Item $out }
$zip = [System.IO.Compression.ZipFile]::Open($out, 'Create')
foreach ($f in $files) {
    $rel = $f.Substring($Game.TrimEnd('\').Length + 1).Replace('\', '/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f, $rel) | Out-Null
    Write-Host "  + $rel"
}
$zip.Dispose()
Write-Host "`n$($files.Count) files -> $out"
Write-Host "Next: create a GitHub Release with tag v$Version and attach this zip (see PUBLISHING_UPDATES.md)."
