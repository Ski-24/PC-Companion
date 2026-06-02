# setup-copy.ps1
# Copies needed files from C:\PC-Control (read-only reference) into C:\PC-Control-App.
# Safe to re-run. Never touches C:\PC-Control.

$src = "C:\PC-Control"
$app = "C:\PC-Control-App"

# ── Create folder structure ────────────────────────────────────────────────────
$dirs = @(
    "$app\App\PCCompanion",
    "$app\Scripts",
    "$app\Config",
    "$app\Tools\Gopher360",
    "$app\Tools\SoundVolumeView",
    "$app\Icons",
    "$app\Logs"
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
    Write-Host "DIR  $d"
}

# ── Copy files ─────────────────────────────────────────────────────────────────
$files = @(
    "Scripts\ToggleGopher360.ps1",
    "Scripts\SwitchAudio.ps1",
    "Config\gopher-state.txt",
    "Config\audio-state.txt",
    "Config\hdr-state.txt",
    "Tools\Gopher360\Gopher.exe",
    "Tools\Gopher360\config.ini",
    "Tools\SoundVolumeView\SoundVolumeView.exe",
    "Icons\pccontrol.ico"
)
foreach ($f in $files) {
    $s = Join-Path $src $f
    $d = Join-Path $app $f
    if (Test-Path $s) {
        Copy-Item -Path $s -Destination $d -Force
        Write-Host "COPY $f"
    } else {
        Write-Warning "MISSING source: $s"
    }
}

Write-Host "`nsetup-copy.ps1 complete. C:\PC-Control was not modified."
