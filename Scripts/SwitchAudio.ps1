$Root = Split-Path -Parent $PSScriptRoot
$SoundVolumeView = Join-Path $Root "Tools\SoundVolumeView\SoundVolumeView.exe"

$Device1 = "Fosi Audio K5 Pro\Device\Headphones\Render"
$Device2 = "A50 X Game\Device\Headphones\Render"

$ConfigFolder = Join-Path $Root "Config"
$StateFile = Join-Path $ConfigFolder "audio-state.txt"

if (!(Test-Path $SoundVolumeView)) {
    exit
}

if (!(Test-Path $ConfigFolder)) {
    New-Item -ItemType Directory -Path $ConfigFolder | Out-Null
}

$LastDevice = ""
if (Test-Path $StateFile) {
    $LastDevice = Get-Content $StateFile -ErrorAction SilentlyContinue
}

if ($LastDevice -eq "Device1") {
    & $SoundVolumeView /SetDefault $Device2 all
    Set-Content $StateFile "Device2"
}
else {
    & $SoundVolumeView /SetDefault $Device1 all
    Set-Content $StateFile "Device1"
}