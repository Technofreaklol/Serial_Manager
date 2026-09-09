# Serial Manager

Windows-Anwendung (WPF, .NET 8) zur Verwaltung von Seriennummern,
Artikeln und Maschinen mit SQLite- oder MySQL-Datenbank.

## Voraussetzungen (Entwicklung)

- .NET 8 SDK
- Visual Studio 2022 (oder eine andere IDE mit .NET/WPF-Unterstützung)
- Windows (WPF-Anwendungen können nur unter Windows entwickelt und
  ausgeführt werden)

## Entwicklung starten

```powershell
dotnet restore
dotnet run --project "Serial Manager\Serial Manager.csproj"
```

Im Entwicklungsbetrieb (Start über Visual Studio oder `dotnet run`)
ist die Anwendung **nicht** "installiert" im Sinne von Velopack -
die Update-Suche (siehe unten) wird dann automatisch übersprungen.

## Installation & automatische Updates

Serial Manager wird über einen Windows-Installer (`SerialManagerSetup.exe`)
installiert und aktualisiert sich danach selbst über
[Velopack](https://velopack.io):

- Der Installer legt die Anwendung im Benutzerprofil ab und erstellt
  Verknüpfungen (Startmenü/Desktop).
- Beim Start prüft die Anwendung im Hintergrund automatisch, ob auf
  GitHub eine neuere Version veröffentlicht wurde. Fällt der Check aus
  (z. B. kein Internet), passiert einfach nichts - die App startet
  ganz normal weiter.
- Über den Menüpunkt **Hilfe → Nach Updates suchen...** kann jederzeit
  manuell geprüft werden. Ist eine neue Version verfügbar, wird nach
  Bestätigung heruntergeladen, installiert und die Anwendung
  automatisch neu gestartet.
- Updates werden aus den [GitHub Releases](https://github.com/Technofreaklol/Serial_Manager/releases)
  dieses Repositories bezogen.

Die technische Umsetzung findet sich in:

- `Serial Manager/App.xaml.cs` - Velopack-Einstiegspunkt (`VelopackApp.Build().Run()`)
- `Serial Manager/Services/UpdateService.cs` - Update-Prüfung/-Anwendung
- `Serial Manager/Views/MainWindow.xaml(.cs)` - Menüpunkt "Nach Updates suchen..."
  sowie stille Prüfung beim Start

## Neue Version veröffentlichen (für Maintainer)

Es gibt zwei Wege, ein neues Release (Installer + Update-Pakete) zu
erzeugen und zu veröffentlichen:

### Option A: Automatisch per GitHub Actions (empfohlen)

1. `<Version>` in `Serial Manager/Serial Manager.csproj` erhöhen
   (nur zur Dokumentation - maßgeblich ist der Tag).
2. Commit erstellen und pushen.
3. Einen Tag im Format `vX.Y.Z` erstellen und pushen:

   ```powershell
   git tag v1.1.0
   git push origin main --tags
   ```

4. Der Workflow `.github/workflows/release.yml` baut die Anwendung
   automatisch, verpackt sie mit `vpk` und veröffentlicht Installer +
   Update-Pakete als GitHub Release. Kein zusätzliches Setup nötig -
   `GITHUB_TOKEN` wird von GitHub Actions automatisch bereitgestellt.

### Option B: Lokal per Skript

Voraussetzung (einmalig): [`vpk`](https://docs.velopack.io) als
.NET-Tool installieren:

```powershell
dotnet tool install -g vpk
```

Danach:

```powershell
# Nur bauen & verpacken (lokal in build/Releases ablegen):
./build/Release-Build.ps1 -Version 1.1.0

# Bauen, verpacken UND direkt auf GitHub veröffentlichen
# (benötigt ein GitHub Personal Access Token mit Schreibrecht
# auf dieses Repository):
$env:GITHUB_TOKEN = "ghp_xxx"
./build/Release-Build.ps1 -Version 1.1.0 -Publish
```

Wichtig: Die Versionsnummer muss bei jedem Release erhöht werden
(SemVer, z. B. `1.0.3` → `1.1.0`), sonst erkennt Velopack keine neue
Version.

Das Installer-/App-Icon (`SerialManagerSetup.exe`, Verknüpfungen,
Taskleiste) wird automatisch aus `Serial Manager/Assets/Logo.ico`
übernommen (per `--icon` in `vpk pack` bzw. `ApplicationIcon` in der
.csproj) - dafür muss dort nichts weiter eingestellt werden.

## Windows-Warnung "Unbekannter Herausgeber"

Ohne digitale Signatur zeigt Windows SmartScreen beim ersten Ausführen
von `SerialManagerSetup.exe` (und der App selbst) die Warnung
"Windows hat Ihren PC geschützt" mit unbekanntem Herausgeber an. Das
liegt daran, dass die Datei nicht code-signiert ist - das ist normal
und lässt sich nur über eine der folgenden Optionen beheben:

### Für die interne Verteilung in der Firma (kostenlos)

Die Warnung wird von Windows meist nur dann angezeigt, wenn die Datei
mit einer "Internetzone"-Markierung (Mark of the Web) heruntergeladen
wurde (z. B. per Browser-Download oder E-Mail-Anhang). Wird die
Installer-Datei stattdessen z. B. über eine interne Netzwerkfreigabe,
USB-Stick oder eine Softwareverteilung (Intune, GPO) bereitgestellt,
taucht die Warnung häufig gar nicht erst auf, bzw. lässt sich zentral
per Gruppenrichtlinie/Intune als Ausnahme freigeben. Für ein internes
Firmenwerkzeug wie dieses ist das meist der einfachste Weg.

Alternativ kann jeder Nutzer die Warnung einmalig selbst bestätigen:
"Weitere Informationen" → "Trotzdem ausführen".

### Für eine echte Code-Signatur (kostenpflichtig)

Um die Warnung dauerhaft für alle Nutzer verschwinden zu lassen (auch
bei Download aus dem Internet), muss die Datei mit einem
Code-Signing-Zertifikat signiert werden:

| Option | Kosten (Stand 2026) | Hinweis |
|---|---|---|
| **Azure Trusted Signing** | ca. 10 $/Monat | Empfohlen: günstigste Option, funktioniert gut mit CI/CD (GitHub Actions); aktuell nur für Einzelpersonen/Firmen mit Sitz in den USA/Kanada verfügbar |
| **OV-Zertifikat** (klassische CA) | ca. 150-300 €/Jahr | Für alle Länder, benötigt Hardware-Token (USB/HSM) |
| **EV-Zertifikat** | ab ca. 400 €/Jahr | Seit 2024 kein Vorteil mehr gegenüber OV bei SmartScreen - nicht mehr nötig |
| Selbstsigniert | kostenlos | Entfernt die Warnung **nicht** bei anderen Nutzern, nur für eigene Tests |

Auch mit Zertifikat verschwindet die Warnung nicht sofort, sondern
baut sich über mehrere signierte Releases als "Reputation" bei
Microsoft auf.

Sobald ein Zertifikat vorhanden ist, lässt es sich einbinden:

- **Lokal:** `./build/Release-Build.ps1 -Version 1.1.0 -SignParams '/f "C:\pfad\zertifikat.pfx" /p "Kennwort" /tr http://timestamp.digicert.com /td sha256 /fd sha256'`
- **GitHub Actions:** Die `.pfx`-Datei Base64-codiert als Repository-Secret
  `WINDOWS_CERTIFICATE_BASE64` hinterlegen, das Passwort als
  `WINDOWS_CERTIFICATE_PASSWORD` (Settings → Secrets and variables →
  Actions). Der Workflow signiert dann automatisch, sobald diese
  Secrets gesetzt sind - ohne sie läuft er wie bisher unsigniert
  weiter.
