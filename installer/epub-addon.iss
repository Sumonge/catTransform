#define AddonVersion "0.2.0"
[Setup]
AppId={{7BFC6789-D7A9-4B7B-91EA-ACB90EE69713}
AppName=catTransform EPUB 组件
AppVersion={#AddonVersion}
DefaultDirName={localappdata}\catTransform\components\epub
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts\installer
OutputBaseFilename=catTransform-{#AddonVersion}-epub-addon-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\CatTransform.App\Assets\catTransform.ico
[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
[Files]
Source: "..\artifacts\release\runtime\*"; DestDir: "{app}\runtime"; Flags: ignoreversion recursesubdirs createallsubdirs
[UninstallDelete]
; Keep user settings, history, and output in their own locations.
