#ifndef PayloadDir
  #define PayloadDir "..\release\native-windows"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\.."
#endif
[Setup]
AppId={{B38D3347-09B6-41CB-B30D-3CC45B393CF5}
AppName=NetPeep
AppVersion=2.0.0
AppPublisher=MD SOWROVE HOSSAIN
AppPublisherURL=https://www.facebook.com/mhsowrove
AppSupportURL=mailto:mhsowrove@gmail.com
DefaultDirName={localappdata}\Programs\NetPeep
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseDir}
OutputBaseFilename=NetPeep-2.0.0-Windows-x64-Setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\NetPeep.exe
WizardStyle=modern
Compression=lzma2/normal
SolidCompression=yes
LicenseFile=..\LICENSE
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\NetPeep.Native.2
VersionInfoCompany=MD SOWROVE HOSSAIN
VersionInfoDescription=NetPeep compact network monitor
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start NetPeep when I sign in"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\NetPeep"; Filename: "{app}\NetPeep.exe"
Name: "{autodesktop}\NetPeep"; Filename: "{app}\NetPeep.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NetPeep"; ValueData: """{app}\NetPeep.exe"" --startup"; Tasks: startup; Flags: uninsdeletevalue
[Run]
Filename: "{app}\NetPeep.exe"; Description: "Launch NetPeep"; Flags: nowait postinstall skipifsilent
