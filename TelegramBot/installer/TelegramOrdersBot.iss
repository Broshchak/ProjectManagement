; Inno Setup script. Build dist first with build_windows.ps1.
#define AppName "Telegram Orders Bot"
#define AppVersion "1.0.0"
#define AppExeName "TelegramOrdersBot.exe"

[Setup]
AppId={{B7A7C5A0-20A9-4C8D-A2B9-1A2B3C4D5E6F}}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\TelegramOrdersBot
DefaultGroupName={#AppName}
OutputBaseFilename=TelegramOrdersBot-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
Uninstallable=yes

[Files]
Source: "..\dist\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\.env.example"; DestDir: "{app}"; DestName: ".env.example"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: autostart

[Tasks]
Name: "autostart"; Description: "Start the bot when Windows starts"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start the bot"; Flags: nowait postinstall skipifsilent
