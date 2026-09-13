; Serial Manager - Windows-Installer (Inno Setup)
;
; Installiert die Anwendung klassisch nach "C:\Program Files\SerialManager"
; (Administratorrechte erforderlich) - im Gegensatz zum vorher verwendeten
; Velopack-Installer, der bewusst PRO BENUTZER ohne Admin-Rechte unter
; %LocalAppData% installiert hat.
;
; Damit trotzdem nicht JEDES künftige Update einen eigenen UAC-Dialog
; braucht, legt dieser Installer zusätzlich einmalig einen
; Taskplaner-Task ("SerialManagerUpdate") mit höchsten Rechten an (siehe
; [Run] unten). Die App selbst (Services/UpdateService.cs) legt künftige
; heruntergeladene Updates in einem eigens dafür freigegebenen Ordner ab
; (siehe [Dirs] unten) und löst den Task dann nur noch aus - ohne erneuten
; UAC-Dialog, weil die Rechte-Erhöhung bereits HIER, beim (admin-
; pflichtigen) Anlegen des Tasks, genehmigt wurde. Nur die allererste
; Aktualisierung ab einer Version von VOR Einführung dieses Mechanismus
; braucht noch einen klassischen UAC-Dialog (Fallback in UpdateService.cs) -
; dabei wird der Task dann gleich mit angelegt.
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

[Dirs]
; Ablageort für einen von der App (OHNE Adminrechte) heruntergeladenen,
; noch nicht installierten Update-Installer - siehe UpdateService.cs
; "StagingDirectory". "users-modify" gibt der Windows-Gruppe "Users"
; (also jedem normalen, nicht-administrativen Benutzer) Schreibrechte NUR
; auf diesen einen kleinen Ordner - nicht auf {app} selbst, das bleibt
; ganz normal admin-geschützt.
Name: "C:\ProgramData\SerialManager\PendingUpdate"; Permissions: users-modify

[Icons]
Name: "{group}\Serial Manager"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,Serial Manager}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Serial Manager"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Legt (bei Erstinstallation UND bei jedem Update-Lauf, da hier ohnehin
; schon Adminrechte vorhanden sind - "/F" überschreibt einen ggf. bereits
; vorhandenen, älteren Task anstandslos) einen Taskplaner-Task mit
; höchsten Rechten an, über den UpdateService.cs künftige Updates OHNE
; erneuten UAC-Dialog installieren kann (siehe Kommentar oben).
; "/RU SYSTEM" braucht keine hinterlegten Benutzeranmeldedaten. Der Task
; selbst wird nur über "schtasks /Run" ausgelöst, nie über einen eigenen
; Zeitplan - "/SC ONCE /ST 00:00 /SD 01/01/2099" ist deshalb nur ein
; Datum weit in der Zukunft, das schtasks als Pflichtangabe verlangt, aber
; von der Anwendung nie abgewartet wird.
Filename: "{sys}\schtasks.exe"; Parameters: "/Create /TN ""SerialManagerUpdate"" /TR ""\""C:\ProgramData\SerialManager\PendingUpdate\SerialManagerSetup.exe\"" /VERYSILENT /NORESTART /CLOSEAPPLICATIONS"" /SC ONCE /ST 00:00 /SD 01/01/2099 /RL HIGHEST /RU SYSTEM /F"; Flags: runhidden; StatusMsg: "Automatischer Update-Mechanismus wird eingerichtet..."

Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Serial Manager}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Entfernt den Task wieder, wenn die Anwendung deinstalliert wird -
; "runhidden" unterdrückt das kurz aufblitzende Konsolenfenster von
; schtasks.exe, "/F" verhindert eine Rückfrage/einen Fehler, falls der
; Task (z. B. bei einer sehr alten Installation) gar nicht existiert.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""SerialManagerUpdate"" /F"; Flags: runhidden
