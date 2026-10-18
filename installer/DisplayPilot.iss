#define AppName "DisplayPilot"
#define AppVersion "0.1.0"
#ifndef AppArch
  #define AppArch "64"
#endif
#define BinaryDirectory "..\dist\" + AppArch
[Setup]
AppId={{B98DDFE4-C70B-49F4-95A8-A11D47393F66}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DisplayPilot
DefaultDirName={autopf}\DisplayPilot
DefaultGroupName=DisplayPilot
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\installers\{#AppArch}
OutputBaseFilename=DisplayPilot-Setup
SetupIconFile=..\assets\displaypilot-primary.ico
UninstallDisplayIcon={app}\DisplayPilot.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
#if AppArch == "64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Files]
Source: "{#BinaryDirectory}\DisplayPilot.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\DisplayPilot"; Filename: "{app}\DisplayPilot.exe"

[Run]
Filename: "{app}\DisplayPilot.exe"; Description: "Launch DisplayPilot"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM DisplayPilot.exe"; Flags: runhidden; RunOnceId: "StopDisplayPilot"
