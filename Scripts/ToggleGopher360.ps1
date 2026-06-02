$Root = Split-Path -Parent $PSScriptRoot
$GopherFolder = Join-Path $Root "Tools\Gopher360"
$GopherExe = Join-Path $GopherFolder "Gopher.exe"
$ProcessName = "Gopher"

if (!(Test-Path $GopherExe)) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("Gopher.exe not found:`n$GopherExe", "PC Companion")
    exit
}

$Running = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue

if ($Running) {
    Stop-Process -Name $ProcessName -Force
}
else {
    Start-Process -FilePath $GopherExe -WorkingDirectory $GopherFolder -WindowStyle Hidden
}