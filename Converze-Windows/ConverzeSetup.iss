; Inno Setup Script for Converze for Windows (v2.2.0)
; Installs into C:\Program Files\Converze with full Windows Uninstaller

[Setup]
AppId={{B8F41C2A-9D5E-4F3B-8A1E-6C7D8E9F0A1B}
AppName=Converze
AppVersion=1.0
AppPublisher=Converze
DefaultDirName={autopf}\Converze
DefaultGroupName=Converze
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
OutputDir=dist
OutputBaseFilename=Converze-InnoSetup-1.0
SetupIconFile=Resources\AppIcon.ico
UninstallDisplayIcon={app}\Resources\AppIcon.ico
UninstallDisplayName=Converze
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "romanian"; MessagesFile: "compiler:Languages\Romanian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "dist\Converze\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Converze"; Filename: "{app}\Converze.exe"; IconFilename: "{app}\Resources\AppIcon.ico"
Name: "{group}\Uninstall Converze"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Converze"; Filename: "{app}\Converze.exe"; IconFilename: "{app}\Resources\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Converze.exe"; Description: "Pornește Converze"; Flags: nowait postinstall skipifsilent runasoriginaluser
