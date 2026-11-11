#define AppVersion "0.1.1"
[Setup]
AppId={{AA7D681C-705F-4D67-B9B2-AB4E53C3B230}
AppName=catTransform
AppVersion={#AppVersion}
AppVerName=catTransform v0.1
DefaultDirName={localappdata}\Programs\catTransform
DefaultGroupName=catTransform
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts\installer
OutputBaseFilename=catTransform-0.1.1-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\catTransform.exe
SetupIconFile=..\src\CatTransform.App\Assets\catTransform.ico
[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
[Files]
Source: "..\artifacts\release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\catTransform"; Filename: "{app}\catTransform.exe"
[Run]
Filename: "{app}\catTransform.exe"; Description: "启动 catTransform"; Flags: nowait postinstall skipifsilent
[UninstallDelete]
; Intentionally empty: never remove user Output, database, rules, or backups.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "catTransform"; Flags: uninsdeletevalue

