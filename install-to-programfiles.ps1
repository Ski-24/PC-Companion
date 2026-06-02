# install-to-programfiles.ps1
# Installs PC Companion into "C:\Program Files\PC Companion" as a CLEAN runtime-only copy
# (just the build output: EXE, its DLLs, deps/runtimeconfig, and .pdb) and points the login
# Startup shortcut at it. No source / Scripts / Tools / Icons / Config clutter -- Gopher360
# and the icon are embedded resources inside the EXE, so they aren't needed on disk.
#
# Re-run this after any rebuild to update the installed copy.
# Self-elevates (UAC prompt) because writing to Program Files requires admin.
#
#   powershell -ExecutionPolicy Bypass -File "C:\PC-Control-App\install-to-programfiles.ps1"

$ErrorActionPreference = 'Stop'

# --- Self-elevate ---------------------------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting administrator rights (approve the UAC prompt)..."
    Start-Process powershell -Verb RunAs -ArgumentList @(
        '-ExecutionPolicy','Bypass','-NoExit','-File',"`"$PSCommandPath`"")
    return
}

$buildOut = 'C:\PC-Control-App\App\PCCompanion\bin\Debug\net8.0-windows'
$dest     = 'C:\Program Files\PC Companion'

if (-not (Test-Path "$buildOut\PCCompanion.exe")) {
    Write-Error "Build output not found at $buildOut - build the project first."; return
}

# Stop any installed instance that might be running from $dest (locks the EXE)
Get-Process PCCompanion -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$dest*" } | Stop-Process -Force

# --- Clean the destination, then copy only the runtime output (flattened) -------
if (Test-Path $dest) {
    Write-Host "Cleaning old contents of $dest ..."
    Remove-Item "$dest\*" -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $dest | Out-Null
}

Write-Host "Installing runtime files:"
Write-Host "  from $buildOut"
Write-Host "  to   $dest"
robocopy $buildOut $dest /E /R:1 /W:1 /NFL /NDL /NJH | Out-Null
$rc = $LASTEXITCODE
if ($rc -ge 8) { Write-Error "robocopy failed (exit $rc)"; return }
Write-Host "Copy complete (robocopy exit $rc; codes 0-7 mean success)."

# --- Point the login Startup shortcut at the installed EXE ----------------------
$exe = Join-Path $dest 'PCCompanion.exe'
if (-not (Test-Path $exe)) { Write-Error "Installed EXE not found at $exe"; return }

$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'PCCompanion.exe.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath       = $exe
$s.Arguments        = '--startup'   # silent launch into the tray (no popup) at login
$s.WorkingDirectory = $dest
$s.Description      = 'PC Companion'
$s.Save()

Write-Host "Startup shortcut now points to: $exe"
Write-Host ""
Write-Host "Done. PC Companion installed (clean) to: $dest"
