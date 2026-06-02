# PC Companion

A lightweight Windows system-tray companion app (WPF, .NET 8). Left-click the tray icon
to open a popup that anchors above the taskbar, with four feature cards:

- **Gopher360** — toggle the Gopher360 gamepad-as-mouse driver
- **Audio** — switch the default playback device between two configured outputs
- **Display** — HDR toggle, SDR-content brightness, Auto HDR, and monitor brightness (DDC/CI)
- **Prayer Times** — next prayer name + countdown (offline calculated, per-country presets)

The popup is responsive: it anchors to the bottom-right of the current monitor's work area
and scrolls internally on small / high-DPI displays.

## Requirements

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) to build
  (end users running a published build only need the **.NET 8 Desktop Runtime**)

## Build & run

```powershell
git clone <your-repo-url>
cd PC-Control-App
dotnet build App\PCCompanion\PCCompanion.csproj -c Debug
.\App\PCCompanion\bin\Debug\net8.0-windows\PCCompanion.exe
```

Or open `App\PCCompanion\PCCompanion.csproj` in Visual Studio 2022 (17.8+) and press F5.

## Publish a standalone build

```powershell
dotnet publish App\PCCompanion\PCCompanion.csproj -r win-x64 -c Release `
    -p:PublishSingleFile=true --self-contained
```

`install-to-programfiles.ps1` is an optional helper that copies a build into
`C:\Program Files\PC Companion` and points the login Startup shortcut at it (self-elevates).

## Project layout

| Path | Purpose |
|------|---------|
| `App/PCCompanion/` | The WPF app (C# namespace `PCCompanion`) |
| &nbsp;&nbsp;`PopupWindow.xaml(.cs)` | Tray popup + all four feature cards |
| &nbsp;&nbsp;`*Service.cs` / `*Manager.cs` | Feature logic (brightness, HDR, SDR, audio, gopher) |
| &nbsp;&nbsp;`Prayer*.cs` | Prayer-time calculation, presets, countdown |
| &nbsp;&nbsp;`AppPaths.cs` | Runtime data location |
| `Tools/Gopher360/` | Gopher360 binary, embedded into the EXE at build time |
| `Icons/` | App icon (embedded) |
| `Scripts/` | Legacy standalone PowerShell helpers (superseded by the C# managers) |

## Configuration & logs

Settings and logs are stored under **`%LocalAppData%\PCCompanion`** (not in the repo):
`Config\settings.json`, state files, and daily `Logs\app-YYYY-MM-DD.log`.

## Notes

- **Stack:** .NET 8, WPF with `UseWindowsForms=true` (the tray icon uses `NotifyIcon`).
- **Bundled tool:** [Gopher360](https://github.com/Tylemagne/Gopher360) is embedded as a
  resource — credit to its authors; see Gopher360's own license for redistribution terms.
- The source currently contains some temporary diagnostic logging (`DisplayDiag.cs`,
  `DIAG-*` log lines) used to investigate an intermittent display-dimming issue.
