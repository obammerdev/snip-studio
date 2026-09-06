#ifndef AppVersion
  #define AppVersion "1.2.2"
#endif
#ifndef PublishDir
  #define PublishDir "..\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif
#ifdef InstallerTest
  #define ProductName "Snip Studio Installer Test"
  #define StartupName "SnipStudio.InstallerTest"
  #define InstanceMutex "Local\SnipStudio.InstallerTest"
  #define SetupName "SnipStudio-InstallerTest"
#else
  #define ProductName "Snip Studio"
  #define StartupName "SnipStudio"
  #define InstanceMutex "Local\SnipStudio.Desktop.v1"
  #define SetupName "SnipStudio-Setup"
#endif

[Setup]
#ifdef InstallerTest
AppId=SnipStudio.InstallerTest
#else
AppId={{C38C5929-3178-49D9-8F11-869E9286F7B2}
#endif
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=obammerdev
AppPublisherURL=https://github.com/obammerdev/snip-studio
AppSupportURL=https://github.com/obammerdev/snip-studio/issues
AppUpdatesURL=https://github.com/obammerdev/snip-studio/releases/latest
VersionInfoDescription=Snip Studio Setup
VersionInfoProductName=Snip Studio
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\{#ProductName}
DefaultGroupName={#ProductName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
WizardStyle=modern dark
WizardSizePercent=110
WizardSmallImageFile=wizard-small.bmp
WizardImageFile=wizard-large.bmp
SetupIconFile=..\Assets\SnipStudio.ico
UninstallDisplayIcon={app}\SnipStudio.exe
UninstallDisplayName={#ProductName}
DisableWelcomePage=yes
DisableDirPage=auto
DisableProgramGroupPage=yes
DisableReadyPage=yes
UsePreviousTasks=no
AppMutex={#InstanceMutex}
CloseApplications=no
RestartApplications=no
Compression=lzma2
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename={#SetupName}

[Tasks]
Name: startup; Description: "Launch quietly when I sign in to Windows"; GroupDescription: "Ready when you need it"; Check: StartupWasEnabled
Name: startup; Description: "Launch quietly when I sign in to Windows"; GroupDescription: "Ready when you need it"; Flags: unchecked; Check: StartupWasDisabled
Name: desktopicon; Description: "Add a desktop shortcut"; GroupDescription: "Shortcuts"; Check: DesktopIconExists
Name: desktopicon; Description: "Add a desktop shortcut"; GroupDescription: "Shortcuts"; Flags: unchecked; Check: DesktopIconMissing

[Files]
Source: "{#PublishDir}\SnipStudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{userprograms}\{#ProductName}"; Filename: "{app}\SnipStudio.exe"; Comment: "Capture, annotate, and share"
Name: "{userdesktop}\{#ProductName}"; Filename: "{app}\SnipStudio.exe"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userdesktop}\{#ProductName}.lnk"; Tasks: not desktopicon

[Run]
Filename: "{app}\SnipStudio.exe"; Description: "Open Snip Studio"; Flags: nowait postinstall skipifsilent

[Messages]
WizardSelectTasks=Make it yours
SelectTasksDesc=A couple of choices, then you're ready.
SelectTasksLabel2=Snip Studio is always available from the Start menu. You can change startup in Settings at any time.
FinishedHeadingLabel=Ready to snip.
FinishedLabel=Press Ctrl + Alt + Shift + S for an instant capture while Snip Studio is running.%n%nDraw, annotate, then copy or save. Your images stay on your device.
SetupAppRunningError=Snip Studio is running.%n%nChoose Quit Snip Studio from its tray menu, or press Ctrl + Q in the editor. Then click OK to continue.%n%nYour saved captures and settings will be kept.
UninstallAppRunningError=Snip Studio is running.%n%nChoose Quit Snip Studio from its tray menu, or press Ctrl + Q in the editor. Then click OK to continue.%n%nUninstall keeps your saved captures and settings.
ConfirmUninstall=Remove %1 and its shortcuts?%n%nYour saved captures and settings will stay on this device.

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
var
  StartupInitiallyEnabled: Boolean;
  DesktopInitiallyExists: Boolean;

function InitializeSetup: Boolean;
begin
  StartupInitiallyEnabled := RegValueExists(HKCU, RunKey, '{#StartupName}');
  DesktopInitiallyExists := FileExists(ExpandConstant('{userdesktop}\{#ProductName}.lnk'));
  Result := True;
end;

function StartupWasEnabled: Boolean;
begin Result := StartupInitiallyEnabled; end;
function StartupWasDisabled: Boolean;
begin Result := not StartupInitiallyEnabled; end;
function DesktopIconExists: Boolean;
begin Result := DesktopInitiallyExists; end;
function DesktopIconMissing: Boolean;
begin Result := not DesktopInitiallyExists; end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('startup') then
    begin
      if not RegWriteStringValue(HKCU, RunKey, '{#StartupName}', ExpandConstant('"{app}\SnipStudio.exe" --background')) then
        MsgBox('Snip Studio is installed, but Windows did not save the startup option. You can enable it in Settings.', mbInformation, MB_OK);
    end
    else
      RegDeleteValue(HKCU, RunKey, '{#StartupName}');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  // Do not remove a startup entry that now belongs to another copy of the app.
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, RunKey, '{#StartupName}', StartupCommand) then
      if CompareText(StartupCommand, ExpandConstant('"{app}\SnipStudio.exe" --background')) = 0 then
        RegDeleteValue(HKCU, RunKey, '{#StartupName}');
end;
