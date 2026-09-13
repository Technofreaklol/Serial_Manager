; Serial Manager - Windows-Installer (Inno Setup)
;
; Installiert die Anwendung klassisch nach "C:\Program Files\SerialManager"
; (Administratorrechte erforderlich) - im Gegensatz zum vorher verwendeten
; Velopack-Installer, der bewusst PRO BENUTZER ohne Admin-Rechte unter
; %LocalAppData% installiert hat. Die Kehrseite dieser Umstellung: jedes
; Update braucht jetzt einen UAC-Dialog, weil das Schreiben nach
; "Program Files" Administratorrechte voraussetzt (siehe
; Services/UpdateService.cs).
;
; Voraussetzung: Inno Setup 6 (https://jrsoftware.org/isinfo.php),
; "ISCC.exe" (Inno Setup Compiler, auch "iscc" im PATH genannt).
;
; Aufruf (siehe build/Release-Build.ps1 bzw. .github/workflows/release.yml):
;   iscc build\installer.iss ^
;        /DMyAppVersion=1.2.3 ^
;        /DPublishDir=C:\voller\pfad\zu\build\publish ^
;        /DOutputDir=C:\voller\pfad\zu\build\Releases
;
; PublishDir muss der Ordner sein, den "dotnet publish" erzeugt hat (also
; SerialManager.exe + alle DLLs + .NET-Laufzeitdateien - seit der
; Umstellung weg von Velopack bewusst KEIN Single-File-EXE mehr, siehe
; "Serial Manager.csproj").

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\build\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\build\Releases"
#endif

#define MyAppName "Serial Manager"
#define MyAppPublisher "Nikolai Hieber"
#define MyAppURL "https://www.hieber.me"
#define MyAppExeName "SerialManager.exe"

[Setup]
; Fester AppId (GUID) - MUSS bei künftigen Versionen unverändert bleiben,
; damit Inno Setup ein Update statt einer parallelen Zweitinstallation
; erkennt. Die doppelte öffnende geschweifte Klammer ist Inno-Setup-Syntax
; zum Einbetten einer GUID (kein Tippfehler) - siehe Inno-Setup-Doku zu
; "AppId".
AppId={{8F2B6C6B-6E9B-4E8C-9F0D-3E7A9B7C9A1F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; {autopf} = "C:\Program Files" (bei 64-Bit-Windows automatisch der
; echte 64-Bit-Programme-Ordner, siehe ArchitecturesInstallIn64BitMode
; unten) statt "Program Files (x86)".
DefaultDirName={autopf}\SerialManager
DefaultGroupName=Serial Manager
DisableProgramGroupPage=yes

; Erzwingt eine Installation für alle Benutzer nach "Program Files"
; (Administratorrechte/UAC-Dialog erforderlich) - genau das war der
; Wunsch: eine "echte" Installation wie bei den meisten anderen
; Windows-Anwendungen, statt eines Pro-Benutzer-Installs ohne Admin-Rechte.
PrivilegesRequired=admin

; Sorgt dafür, dass {autopf} auf 64-Bit-Windows den echten 64-Bit-
; Programme-Ordner meint (die veröffentlichte Anwendung ist win-x64,
; siehe Serial Manager.csproj "RuntimeIdentifier").
ArchitecturesInstallIn64BitMode=x64

OutputDir={#OutputDir}
OutputBaseFilename=SerialManagerSetup
SetupIconFile=..\Serial Manager\Assets\Logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

Compression=lzma2
SolidCompression=yes
WizardStyle=modern

; Schließt beim (Update-)Setup automatisch laufende Instanzen der
; Anwendung, statt mit einer Fehlermeldung ("Datei wird von
; SerialManager.exe verwendet") abzubrechen - relevant für den in
; UpdateService.cs beschriebenen Update-Ablauf.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Übernimmt 1:1 alles, was "dotnet publish" erzeugt hat.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Serial Manager"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,Serial Manager}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Serial Manager"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Serial Manager}"; Flags: nowait postinstall skipifsilent