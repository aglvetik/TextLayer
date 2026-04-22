#define AppName "TextLayer"
#define AppVersion "0.1.0"
#define AppPublisher "TextLayer"
#define AppExeName "TextLayer.exe"
#define PayloadDir "..\dist\TextLayer"

[Setup]
AppId={{7F33F3E4-3E41-4F3A-BB40-016A8E1DD6C5}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\TextLayer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=TextLayer-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ShowLanguageDialog=yes
LanguageDetectionMethod=none
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.StartupGroup=Startup:
russian.StartupGroup=Автозапуск:
english.StartupTask=Start TextLayer automatically when I start my computer
russian.StartupTask=Запускать TextLayer автоматически при запуске компьютера
english.LaunchNow=Launch TextLayer now
russian.LaunchNow=Запустить TextLayer сейчас

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:StartupGroup}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TextLayer"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall TextLayer"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TextLayer"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TextLayer"; ValueData: """{app}\{#AppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchNow}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
