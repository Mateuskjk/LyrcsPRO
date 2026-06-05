; LyricsPro - Inno Setup Script
; IMPORTANTE: Rode build-installer.ps1 ANTES de compilar este arquivo.
; O script gera a pasta publish\win-x64 que o instalador usa.

#define AppName      "LyricsPro"
#define AppVersion   "1.0.0"
#define AppPublisher "LyricsPro"
#define AppExe       "LyricsPro.exe"
#define PublishDir   "..\publish\win-x64"

[Setup]
AppId={{A3F2C1B4-8D7E-4F9A-B6C2-1E3D5A7F9B0C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/lyricspro
AppSupportURL=https://github.com/lyricspro
AppUpdatesURL=https://github.com/lyricspro

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=no

OutputDir=.\output
OutputBaseFilename=LyricsPro-Setup-v{#AppVersion}

SetupIconFile=..\src\LyricsPro\Assets\logo.ico
WizardStyle=modern
WizardSizePercent=120

Compression=lzma2/ultra64
SolidCompression=yes

MinVersion=10.0.17763
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Area de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir LyricsPro agora"; Flags: nowait postinstall skipifsilent
