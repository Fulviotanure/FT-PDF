; Inno Setup Script para o CCPDF
; Cria o instalador oficial para Windows com registro de programa padrão e atalhos

#define MyAppName "CCPDF"
#define MyAppVersion "2.5.2"
#define MyAppPublisher "Fulvio Tanure"
#define MyAppURL "https://github.com/Fulviotanure/ft-pdf"
#define MyAppExeName "CCPDF.exe"

[Setup]
AppId={{D37E84B1-29C0-461B-B0A2-E8B18CF38F01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist-installer
OutputBaseFilename=Instalador_FT_PDF_Lite_v2.0.0
SetupIconFile=..\Assets\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associatepdf"; Description: "Associar arquivos PDF (.pdf) ao CCPDF"; GroupDescription: "Associações:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Registry]
; Registrar ProgID para arquivos PDF
Root: HKCR; Subkey: ".pdf\OpenWithProgids"; ValueType: string; ValueName: "CCPDF.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatepdf
Root: HKCR; Subkey: "CCPDF.Document"; ValueType: string; ValueName: ""; ValueData: "Documento PDF"; Flags: uninsdeletekey; Tasks: associatepdf
Root: HKCR; Subkey: "CCPDF.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Assets\app.ico"; Tasks: associatepdf
Root: HKCR; Subkey: "CCPDF.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatepdf

; Registrar em Applications para aparecer no menu "Abrir com..."
Root: HKLM; Subkey: "SOFTWARE\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Flags: uninsdeletekey; Tasks: associatepdf
Root: HKLM; Subkey: "SOFTWARE\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associatepdf

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
