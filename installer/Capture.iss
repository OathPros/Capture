; Compile through scripts/package.ps1 after publishing the self-contained app.
#ifndef AppVersion
  #error AppVersion must be supplied by the packaging script
#endif
#ifndef NumericVersion
  #error NumericVersion must be supplied by the packaging script
#endif
[Setup]
AppId={{B7BBA578-827A-4938-AFD0-3FCB06669B91}
AppName=YU Capture
AppVersion={#AppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher=YU Capture
AppSupportURL=https://github.com/OathPros/Capture
DefaultDirName={localappdata}\Programs\YU Capture
DefaultGroupName=YU Capture
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts
OutputBaseFilename=YU-Capture-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\Capture.App.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\Capture-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\installer-dependencies\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{userprograms}\YU Capture"; Filename: "{app}\Capture.App.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\YU Capture"; Filename: "{app}\Capture.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Capture.App.exe"; Description: "Launch YU Capture"; Flags: nowait postinstall skipifsilent

[Code]
function RuntimeInRegistry(Root: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(Root,
    'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version)
    and (Version <> '') and (Version <> '0.0.0.0');
end;

function HasWebView2(): Boolean;
begin
  Result := RuntimeInRegistry(HKCU) or RuntimeInRegistry(HKLM32) or RuntimeInRegistry(HKLM64);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if HasWebView2() then exit;
  WizardForm.StatusLabel.Caption := 'Installing Microsoft Edge WebView2 Runtime…';
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '',
    SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Microsoft Edge WebView2 could not start. Check your connection and try again, or contact IT.';
    exit;
  end;
  if not HasWebView2() then
    Result := 'Microsoft Edge WebView2 installation did not complete (code ' + IntToStr(ExitCode) + '). An internet connection and permission to install WebView2 are required. Please retry or contact IT.';
end;
