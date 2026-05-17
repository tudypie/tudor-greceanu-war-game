# Splice Motion Canvas image-sequence renders into MP4(s) with ffmpeg.
#
# Usage:
#   .\build-video.ps1                         # auto-detects output folder, uses 24 fps
#   .\build-video.ps1 -Fps 30
#   .\build-video.ps1 -InputDir path\to\frames -Output final.mp4
#   .\build-video.ps1 -Crf 23                 # lower = better quality, larger file
#
# Motion Canvas writes either:
#   A) flat:        output\<NNNNNN>.png
#   B) per-scene:   output\<sceneName>\<NNNNNN>.png
# This script handles both: case A produces one MP4; case B produces one MP4
# per scene folder and concatenates them into a single final MP4.

[CmdletBinding()]
param(
  [string]$InputDir = 'animation\output',
  [int]$Fps = 24,
  [int]$Crf = 18,
  [string]$Output = 'cutscenes.mp4'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
  throw "ffmpeg not found on PATH. Install it or add it to PATH."
}

if (-not (Test-Path $InputDir)) {
  throw "Input directory not found: $InputDir"
}

# Detect layout: are PNGs directly in $InputDir (flat) or in subfolders (per scene)?
$rootPngs = Get-ChildItem -Path $InputDir -Filter '*.png' -File -ErrorAction SilentlyContinue
$sceneDirs = Get-ChildItem -Path $InputDir -Directory -ErrorAction SilentlyContinue |
             Where-Object { (Get-ChildItem -Path $_.FullName -Filter '*.png' -File).Count -gt 0 }

function Build-Mp4 {
  param(
    [string]$FromDir,
    [string]$OutFile
  )
  # Probe filename pattern (e.g. 000001.png, frame_0001.png)
  $first = Get-ChildItem -Path $FromDir -Filter '*.png' -File | Sort-Object Name | Select-Object -First 1
  if (-not $first) { throw "No PNGs in $FromDir" }

  # Extract numeric padding length from first filename: e.g. "frame_000123.png" -> prefix "frame_", pad 6
  if ($first.BaseName -match '^(.*?)(\d+)$') {
    $prefix = $matches[1]
    $pad    = $matches[2].Length
    $pattern = '{0}%0{1}d.png' -f $prefix, $pad
  } else {
    throw "Couldn't determine numeric pattern from '$($first.Name)'"
  }

  $globPath = Join-Path $FromDir $pattern
  Write-Host "  ffmpeg pattern: $globPath -> $OutFile"

  & ffmpeg -y -framerate $Fps -i $globPath `
    -c:v libx264 -crf $Crf -pix_fmt yuv420p -movflags +faststart `
    $OutFile
  if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed for $FromDir" }
}

if ($rootPngs.Count -gt 0 -and $sceneDirs.Count -eq 0) {
  # Case A: flat folder
  Write-Host "Detected flat PNG sequence in $InputDir"
  Build-Mp4 -FromDir $InputDir -OutFile $Output
  Write-Host "Wrote $Output"
  return
}

if ($sceneDirs.Count -gt 0) {
  # Case B: per-scene subfolders -> one MP4 per scene, then concat
  Write-Host "Detected $($sceneDirs.Count) scene folder(s); rendering each and concatenating"
  $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("mc-mux-" + [Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $tmpDir | Out-Null

  $parts = @()
  foreach ($d in $sceneDirs | Sort-Object Name) {
    $part = Join-Path $tmpDir ("{0}.mp4" -f $d.Name)
    Write-Host "Scene: $($d.Name)"
    Build-Mp4 -FromDir $d.FullName -OutFile $part
    $parts += $part
  }

  $listFile = Join-Path $tmpDir 'concat.txt'
  $parts | ForEach-Object { "file '$($_ -replace "'", "''")'" } | Set-Content -Path $listFile -Encoding ascii

  & ffmpeg -y -f concat -safe 0 -i $listFile -c copy $Output
  if ($LASTEXITCODE -ne 0) { throw "concat ffmpeg failed" }

  Remove-Item -Recurse -Force $tmpDir
  Write-Host "Wrote $Output"
  return
}

throw "No PNGs found under $InputDir (neither flat nor in subfolders)."
