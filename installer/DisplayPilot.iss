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

function ExpandProfilePath(ProfilePath: String): String;
begin
  StringChangeEx(
    ProfilePath,
    '%SystemDrive%',
    ExpandConstant('{sd}'),
    True);
  Result := ProfilePath;
end;

function GetProfilesDirectory(): String;
var
  ProfilesDirectory: String;
begin
  if not RegQueryStringValue(
       HKLM64,
       'Software\Microsoft\Windows NT\CurrentVersion\ProfileList',
       'ProfilesDirectory',
       ProfilesDirectory) then
    ProfilesDirectory := ExpandConstant('{sd}\Users');

  Result := ExpandProfilePath(ProfilesDirectory);
end;

function GetProfileStartupDirectory(const ProfileDirectory: String): String;
begin
  Result :=
    AddBackslash(ProfileDirectory) +
    'AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup';
end;

procedure UpdateProfileStartupShortcut(
  const ProfileDirectory: String;
  const InstallShortcut: Boolean);
var
  ShortcutDirectory: String;
  ShortcutPath: String;
begin
  if not DirExists(ProfileDirectory) then
    Exit;

  ShortcutDirectory := GetProfileStartupDirectory(ProfileDirectory);
  ShortcutPath :=
    AddBackslash(ShortcutDirectory) + '{#MyAppName}.lnk';

  if InstallShortcut then
  begin
    ForceDirectories(ShortcutDirectory);
    ShortcutPath := CreateShellLink(
      ShortcutPath,
      'Start {#MyAppName} when this user signs in',
      ExpandConstant('{app}\{#MyAppExeName}'),
      '--startup',
      ExpandConstant('{app}'),
      ExpandConstant('{app}\{#MyAppExeName}'),
      0,
      SW_SHOWNORMAL);
    Log('Created per-user startup shortcut: ' + ShortcutPath);
  end
  else if DeleteFile(ShortcutPath) then
    Log('Removed per-user startup shortcut: ' + ShortcutPath);
end;

procedure UpdateAllProfileStartupShortcuts(const InstallShortcuts: Boolean);
var
  ProfileKeys: TArrayOfString;
  ProfileDirectory: String;
  ProfileKey: String;
  Index: Integer;
begin
  { Provision the Default User template for accounts created after installation. }
  UpdateProfileStartupShortcut(
    AddBackslash(GetProfilesDirectory()) + 'Default',
    InstallShortcuts);

  if not RegGetSubkeyNames(
       HKLM64,
       'Software\Microsoft\Windows NT\CurrentVersion\ProfileList',
       ProfileKeys) then
  begin
    Log('Could not enumerate Windows user profiles.');
    Exit;
  end;

  for Index := 0 to GetArrayLength(ProfileKeys) - 1 do
  begin
    if Pos('S-1-5-21-', ProfileKeys[Index]) = 1 then
    begin
      ProfileKey :=
        'Software\Microsoft\Windows NT\CurrentVersion\ProfileList\' +
        ProfileKeys[Index];
      if RegQueryStringValue(
           HKLM64,
           ProfileKey,
           'ProfileImagePath',
           ProfileDirectory) then
        UpdateProfileStartupShortcut(
          ExpandProfilePath(ProfileDirectory),
          InstallShortcuts);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Remove the superseded common shortcut, then provision per-profile entries. }
    DeleteFile(ExpandConstant('{commonstartup}\{#MyAppName}.lnk'));
    UpdateAllProfileStartupShortcuts(WizardIsTaskSelected('startup'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteFile(ExpandConstant('{commonstartup}\{#MyAppName}.lnk'));
    UpdateAllProfileStartupShortcuts(False);
    RegDeleteValue(
      HKCU,
      'Software\Microsoft\Windows\CurrentVersion\Run',
      'DisplayPilot');
  end;
end;
