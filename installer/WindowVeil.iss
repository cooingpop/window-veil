#ifndef AppVersion
  #error Pass AppVersion from VERSION with /DAppVersion=MAJOR.MINOR.PATCH
#endif

[Setup]
AppId={{5AB75D1B-785C-4A61-AB5D-5963A0F4CB99}
AppName=Window Veil
AppVersion={#AppVersion}
AppPublisher=Window Veil contributors
DefaultDirName={localappdata}\Programs\WindowVeil
DefaultGroupName=Window Veil
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=WindowVeil-Setup-{#AppVersion}-win-x64
Compression=lzma
SolidCompression=yes
SetupIconFile=..\assets\WindowVeil.ico
UninstallDisplayIcon={app}\WindowVeil.exe
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\dist\WindowVeil.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Window Veil"; Filename: "{app}\WindowVeil.exe"

[Run]
Filename: "{app}\WindowVeil.exe"; Description: "Start Window Veil"; Flags: nowait postinstall skipifsilent

[Code]
// The app adds itself to the Windows startup list when "Start Window Veil when Windows starts" is checked.
// Remove that entry on uninstall so Windows does not try to start a program that is gone.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WindowVeil');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', 'WindowVeil');
  end;
end;
