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

Serial Manager wird über einen Windows-Installer (`Serial ManagerSetup.exe`)
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
