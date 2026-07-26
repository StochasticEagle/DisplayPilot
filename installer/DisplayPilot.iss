#define MyAppName "DisplayPilot"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "StochasticEagle"
#define MyAppExeName "DisplayPilot.exe"

[Setup]
AppId={{B98DDFE4-C70B-49F4-95A8-A11D47393F66}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=DisplayPilot-Setup-win-x64
SetupIconFile=..\src\DisplayPilot.App\Assets\displaypilot-primary.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\DisplayPilot-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\installer\DisplayPilot-Prerequisites.exe"; Flags: dontcopy

[Tasks]
Name: "startup"; Description: "Start DisplayPilot when users sign in"; GroupDescription: "Startup:"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKLM64; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplayPilot"; ValueData: """{app}\{#MyAppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile('DisplayPilot-Prerequisites.exe');
  if not Exec(
       ExpandConstant('{tmp}\DisplayPilot-Prerequisites.exe'),
       '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010',
       '',
       SW_HIDE,
       ewWaitUntilTerminated,
       ResultCode) then
  begin
    Result := 'The shared system runtime installer could not be started.';
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode <> 0 then
    Result :=
      'The shared system runtime installation failed with exit code ' +
      IntToStr(ResultCode) + '.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and
     (not WizardIsTaskSelected('startup')) then
    RegDeleteValue(
      HKLM64,
      'Software\Microsoft\Windows\CurrentVersion\Run',
      'DisplayPilot');
end;
