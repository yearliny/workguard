#define MyAppName "工作防沉迷"
#define MyAppExeName "WorkGuard.exe"
#ifndef AppVersion
  #error AppVersion must be supplied by the build script.
#endif
#ifndef SourceDir
  #error SourceDir must be supplied by the build script.
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by the build script.
#endif

[Setup]
AppId={{B632BA27-C541-4E56-BC41-40A9721199E4}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher=WorkGuard
AppPublisherURL=https://github.com/yearliny/workguard
AppSupportURL=https://github.com/yearliny/workguard/issues
DefaultDirName={localappdata}\Programs\WorkGuard
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=WorkGuard-{#AppVersion}-win-x64-setup
SetupIconFile=..\src\WorkGuard.Windows\Assets\WorkGuard.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesEnvironment=no
ChangesAssociations=no
AllowNoIcons=yes
MinVersion=10.0.19045

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Registry]
; WorkGuard owns this value at runtime. The installer only guarantees uninstall cleanup.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "WorkGuard"; Flags: uninsdeletevalue

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
