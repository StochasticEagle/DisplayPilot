#define MyAppName "DisplayPilot Prerequisites"

[Setup]
AppId={{E85697C5-3105-4058-B7AA-F33B11889641}
AppName={#MyAppName}
AppVersion=0.1.0
AppPublisher=StochasticEagle
CreateAppDir=no
CreateUninstallRegKey=no
Uninstallable=no
OutputDir=..\artifacts\installer
OutputBaseFilename=DisplayPilot-Prerequisites
SetupIconFile=..\src\DisplayPilot.App\Assets\displaypilot-primary.ico
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableWelcomePage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
RestartApplications=no

[Files]
Source: "..\artifacts\prerequisites\windowsdesktop-runtime-win-x64.exe"; Flags: dontcopy
Source: "..\artifacts\prerequisites\WindowsAppRuntimeInstall-x64.exe"; Flags: dontcopy
Source: "..\artifacts\prerequisites\vc_redist.x64.exe"; Flags: dontcopy

[Code]
function IsDotNetDesktopRuntimeInstalled: Boolean;
var
  Versions: TArrayOfString;
  Index: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(
       HKLM64,
       'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
       Versions) then
  begin
    for Index := 0 to GetArrayLength(Versions) - 1 do
    begin
      if Pos('10.', Versions[Index]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function RunRuntimeInstaller(
  const FileName: String;
  const Parameters: String;
  var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile(FileName);
  if not Exec(
       ExpandConstant('{tmp}\') + FileName,
       Parameters,
       '',
       SW_HIDE,
       ewWaitUntilTerminated,
       ResultCode) then
  begin
    Result := FileName + ' could not be started.';
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode <> 0 then
    Result :=
      FileName + ' failed with exit code ' + IntToStr(ResultCode) + '.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if not IsDotNetDesktopRuntimeInstalled then
    Result := RunRuntimeInstaller(
      'windowsdesktop-runtime-win-x64.exe',
      '/install /quiet /norestart',
      NeedsRestart);
  if Result <> '' then
    Exit;

  Result := RunRuntimeInstaller(
    'vc_redist.x64.exe',
    '/install /quiet /norestart',
    NeedsRestart);
  if Result <> '' then
    Exit;

  Result := RunRuntimeInstaller(
    'WindowsAppRuntimeInstall-x64.exe',
    '--quiet',
    NeedsRestart);
end;
