; ============================================================================
;  PC Companion — Inno Setup installer
;  Per-user install (no admin) to %LOCALAPPDATA%\PC Companion, with a proper
;  Add/Remove Programs (Apps) entry, optional desktop shortcut, optional
;  "start with Windows" (HKCU\...\Run), and optional Stream Deck support.
;
;  Build:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\PCCompanion.iss
;  Output: Installer\Output\PCCompanion-Setup-<version>.exe
;
;  IMPORTANT: build the app first so the publish folder exists:
;    dotnet publish App\PCCompanion\PCCompanion.csproj -r win-x64 -c Release `
;        --self-contained -p:PublishSingleFile=true -o publish
; ============================================================================

#define MyAppName        "PC Companion"
#define MyAppVersion     "1.0.11"
#define MyAppPublisher   "Abdulla"
#define MyAppExeName     "PCCompanion.exe"
#define RepoRoot         "C:\PC-Control-App"
#define PublishDir       RepoRoot + "\publish"
#define PluginFolderName "com.abdulla.pccompanion.sdPlugin"
#define PluginSourceDir  RepoRoot + "\StreamDeckPlugin\" + PluginFolderName

[Setup]
; A fixed AppId is what lets a re-run detect an existing install and update/repair
; it in place (rather than duplicating). Never change this GUID once shipped.
AppId={{815B203A-9B01-4426-9674-D462E5BB818C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}

; Per-user, no UAC. Inno registers the uninstaller under HKCU so it still shows up
; in Control Panel > Programs and Features and Settings > Apps as "PC Companion".
PrivilegesRequired=lowest
DefaultDirName={localappdata}\PC Companion
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

OutputDir={#RepoRoot}\Installer\Output
OutputBaseFilename=PCCompanion-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Create a &desktop shortcut";                          GroupDescription: "Shortcuts:";       Flags: unchecked
Name: "startup";      Description: "Start {#MyAppName} when I sign in to Windows";          GroupDescription: "Startup:";         Flags: checkedonce
Name: "streamdeck";   Description: "Install Stream Deck support (PC Companion plugin)";     GroupDescription: "Stream Deck:";     Flags: unchecked

[Files]
; --- Main app: the whole self-contained publish output (EXE + WPF native DLLs).
;     .pdb is debug-only and not needed at runtime.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

; --- Stream Deck plugin: only copied when the task is selected AND the user accepts
;     the repair prompt if it already exists (see ShouldCopyStreamDeck). Runtime logs
;     from a previous run are excluded.
Source: "{#PluginSourceDir}\*"; DestDir: "{userappdata}\Elgato\StreamDeck\Plugins\{#PluginFolderName}"; \
    Excludes: "logs\*"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: ShouldCopyStreamDeck

[Icons]
; Start Menu shortcut — launches normally (shows the popup when clicked).
Name: "{group}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
; Optional desktop shortcut.
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; "Start with Windows" — silent launch straight into the tray (--startup), pointing at
; the INSTALLED exe. uninsdeletevalue removes it cleanly on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --startup"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
; Offer to launch right after install (normal launch so the user sees it works).
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; \
    Flags: nowait postinstall skipifsilent
; Silent (in-app auto-update) path: relaunch straight into the tray so the app comes back
; on its own after UpdateService runs the installer with /VERYSILENT. WizardSilent gates
; this to silent runs only, so it never double-launches during a normal interactive install.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup"; \
    Flags: nowait; Check: WizardSilent

[Code]
var
  // 0 = not yet decided, 1 = copy plugin, 2 = skip (user declined repair)
  GStreamDeckDecision: Integer;

function StreamDeckPluginDir: String;
begin
  Result := ExpandConstant('{userappdata}\Elgato\StreamDeck\Plugins\{#PluginFolderName}');
end;

function StreamDeckManifestPath: String;
begin
  Result := StreamDeckPluginDir + '\manifest.json';
end;

// Gate for the plugin [Files] entry. Runs during the copy phase; the prompt fires once.
function ShouldCopyStreamDeck: Boolean;
begin
  if not WizardIsTaskSelected('streamdeck') then
  begin
    Result := False;
    Exit;
  end;

  if GStreamDeckDecision = 0 then
  begin
    if FileExists(StreamDeckManifestPath) then
    begin
      if MsgBox('Stream Deck support is already installed.' + #13#10 + #13#10 +
                'Repair / reinstall the PC Companion plugin files?',
                mbConfirmation, MB_YESNO) = IDYES then
        GStreamDeckDecision := 1
      else
        GStreamDeckDecision := 2;
    end
    else
      GStreamDeckDecision := 1;  // not installed yet -> install
  end;

  Result := (GStreamDeckDecision = 1);
end;

// Close any running instance before we overwrite the EXE (it would otherwise be locked).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM {#MyAppExeName} /F',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Remove the legacy Startup-folder shortcut left by the old PowerShell installer, so
    // login doesn't launch a stale dev build alongside the new Run entry. Startup is now
    // driven solely by the HKCU Run value pointing at the installed exe.
    DeleteFile(ExpandConstant('{userstartup}\PCCompanion.exe.lnk'));

    if GStreamDeckDecision = 1 then
    begin
      // Record that WE installed the plugin, so uninstall only removes it in that case
      // (never a plugin the user had from elsewhere).
      RegWriteDWordValue(HKCU, 'Software\PC Companion', 'StreamDeckInstalled', 1);
      // The Stream Deck app may need a restart to pick up the new actions.
      MsgBox('Stream Deck support installed.' + #13#10 + #13#10 +
             'If the PC Companion actions do not appear, restart the Stream Deck app.',
             mbInformation, MB_OK);
    end;
  end;
end;

// ---- Uninstall ------------------------------------------------------------------
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  PluginDir, ConfigDir: String;
  ResultCode: Integer;
  InstalledByUs: Cardinal;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Make sure the app isn't running so its files can be removed.
    Exec(ExpandConstant('{cmd}'), '/C taskkill /IM {#MyAppExeName} /F',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Stream Deck support — only offer to remove the plugin if THIS installer placed it
    // (marker written at install time). Ask, default Yes. Never touch other plugins.
    PluginDir := StreamDeckPluginDir;
    if RegQueryDWordValue(HKCU, 'Software\PC Companion', 'StreamDeckInstalled', InstalledByUs)
       and (InstalledByUs = 1) and DirExists(PluginDir) then
    begin
      if MsgBox('Remove Stream Deck support files too?' + #13#10 + #13#10 +
                'Only the PC Companion plugin will be removed; other Stream Deck ' +
                'plugins and settings are left untouched.',
                mbConfirmation, MB_YESNO) = IDYES then
        DelTree(PluginDir, True, True, True);
    end;
    // Clean up our own marker key regardless.
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\PC Companion');

    // User settings/config — ask, default NO so settings are preserved.
    ConfigDir := ExpandConstant('{localappdata}\PCCompanion');
    if DirExists(ConfigDir) then
    begin
      if MsgBox('Do you want to remove PC Companion settings and configuration files?' + #13#10 + #13#10 +
                'Choose No to keep your settings and logs for next time.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(ConfigDir, True, True, True);
    end;
  end;
end;
