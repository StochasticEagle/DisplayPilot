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
Source: "..\artifacts\prerequisites\windowsdesktop-runtime-win-x64.exe"; Flags: dontcopy
Source: "..\artifacts\prerequisites\WindowsAppRuntimeInstall-x64.exe"; Flags: dontcopy
Source: "..\artifacts\prerequisites\vc_redist.x64.exe"; Flags: dontcopy

[Tasks]
Name: "startup"; Description: "Start DisplayPilot for all users when they sign in"; GroupDescription: "Startup:"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--startup"; Tasks: startup

[Registry]
; Remove the obsolete machine-wide startup entry created by installers before 0.1.0.
Root: HKLM64; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "DisplayPilot"; Flags: deletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--remove-legacy-user-startup"; Flags: runasoriginaluser runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /T /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "CloseDisplayPilot"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Assets"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\DirectML.dll"
Type: files; Name: "{app}\Microsoft.ML.OnnxRuntime.dll"
Type: files; Name: "{app}\Microsoft.Windows.AI.*"
Type: files; Name: "{app}\Microsoft.Windows.Widgets.*"
Type: files; Name: "{app}\Microsoft.Web.WebView2.*"
Type: files; Name: "{app}\onnxruntime.dll"
Type: files; Name: "{app}\WebView2Loader.dll"
Type: dirifempty; Name: "{app}"

[Code]
function IsSuccessfulRuntimeExitCode(ResultCode: Integer): Boolean;
begin
  Result :=
    (ResultCode = 0) or
    (ResultCode = 3010) or
    (ResultCode = 1638) or
    (ResultCode = -2147023290);
end;

function RunRuntimeInstaller(
  const DisplayName: String;
  const FileName: String;
  const Parameters: String;
  var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile(FileName);
  WizardForm.StatusLabel.Caption := 'Installing ' + DisplayName + '...';
  Log('Starting ' + DisplayName + '.');

  if not Exec(
       ExpandConstant('{tmp}\') + FileName,
       Parameters,
       '',
       SW_HIDE,
       ewWaitUntilTerminated,
       ResultCode) then
  begin
    Result := DisplayName + ' installer could not be started.';
    Exit;
  end;

  Log(DisplayName + ' installer exit code: ' + IntToStr(ResultCode));
  if ResultCode = 3010 then
    NeedsRestart := True
  else if not IsSuccessfulRuntimeExitCode(ResultCode) then
    Result :=
      DisplayName + ' installation failed with exit code ' +
      IntToStr(ResultCode) + '. Setup cannot continue.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := RunRuntimeInstaller(
    '.NET 10 Desktop Runtime',
    'windowsdesktop-runtime-win-x64.exe',
    '/install /quiet /norestart',
    NeedsRestart);
  if Result <> '' then
    Exit;

  Result := RunRuntimeInstaller(
    'Microsoft Visual C++ Runtime',
    'vc_redist.x64.exe',
    '/install /quiet /norestart',
    NeedsRestart);
  if Result <> '' then
    Exit;

  Result := RunRuntimeInstaller(
    'Windows App SDK Runtime',
    'WindowsAppRuntimeInstall-x64.exe',
    '--quiet',
    NeedsRestart);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and
     (not WizardIsTaskSelected('startup')) then
    DeleteFile(ExpandConstant('{commonstartup}\{#MyAppName}.lnk'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(
      HKCU,
      'Software\Microsoft\Windows\CurrentVersion\Run',
      'DisplayPilot');
end;
