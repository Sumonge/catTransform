#define AppVersion "0.2.0"
#ifdef Lite
#define PackageFlavor "lite"
#else
#define PackageFlavor "full"
#endif
[Setup]
AppId={{AA7D681C-705F-4D67-B9B2-AB4E53C3B230}
AppName=catTransform
AppVersion={#AppVersion}
AppVerName=catTransform v0.2
DefaultDirName={localappdata}\Programs\catTransform
DefaultGroupName=catTransform
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts\installer
OutputBaseFilename=catTransform-{#AppVersion}-win-x64-{#PackageFlavor}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\catTransform.exe
SetupIconFile=..\src\CatTransform.App\Assets\catTransform.ico
[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
[Files]
Source: "..\artifacts\release\*"; DestDir: "{app}"; Excludes: "\runtime\*"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifndef Lite
Source: "..\artifacts\release\runtime\*"; DestDir: "{app}\runtime"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif
[Icons]
Name: "{group}\catTransform"; Filename: "{app}\catTransform.exe"
[Run]
Filename: "{app}\catTransform.exe"; Description: "启动 catTransform"; Flags: nowait postinstall skipifsilent
[UninstallDelete]
; Intentionally empty: never remove user Output, database, rules, or backups.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "catTransform"; Flags: uninsdeletevalue

