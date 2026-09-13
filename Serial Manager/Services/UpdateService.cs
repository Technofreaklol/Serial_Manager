using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SerialManager.Services;

/// <summary>
/// Ergebnis einer erfolgreichen Update-Prüfung: die auf GitHub gefundene
/// neuere Version, der Download-Link zum passenden Installer-Asset (siehe
/// InstallerAssetName unten) und die Versionshinweise (Release-Beschreibung
/// aus GitHub, für die "Was ist neu"-Anzeige in UpdateAvailableWindow).
/// </summary>
public sealed record UpdateCheckResult(Version Version, string DownloadUrl, string FileName, string ReleaseNotes);

/// <summary>
/// Kapselt die Update-Logik OHNE Velopack.
///
/// Hintergrund: Velopack (wie zuvor verwendet) installiert bewusst pro
/// Benutzer unter %LocalAppData%, NICHT nach "C:\Program Files" - nur so
/// kann es sich selbst ohne Administratorrechte im Hintergrund
/// aktualisieren. Seit dem Umstieg auf einen klassischen Installer
/// (build/installer.iss, Inno Setup), der wie bei den meisten
/// Windows-Anwendungen nach "C:\Program Files\SerialManager" installiert,
/// braucht JEDES Update Administratorrechte (Schreibzugriff auf Program
/// Files) - ein stiller Hintergrund-Update ohne Zutun des Benutzers ist
/// damit nicht mehr möglich. Stattdessen läuft ein Update jetzt so ab:
///
///   1. CheckForUpdatesAsync: Prüft über die GitHub-API, ob im Repository
///      ein neueres Release als die aktuell laufende Version vorliegt.
///   2. DownloadInstallerAsync: Lädt das zugehörige Setup.exe in einen
///      temporären Ordner herunter.
///   3. InstallUpdateAsync: Installiert das heruntergeladene Setup.exe und
///      beendet die laufende Anwendung.
///
/// Für Schritt 3 gibt es zwei Wege (siehe InstallUpdateAsync): den
/// bevorzugten Weg über einen bei der Installation angelegten
/// Taskplaner-Task OHNE erneuten UAC-Dialog, und einen "runas"-Fallback MIT
/// UAC-Dialog für Installationen, die diesen Task noch nicht kennen (siehe
/// dort für Details).
/// </summary>
public class UpdateService
{
    // Name des bei der Installation (siehe build/installer.iss) mit
    // höchsten Rechten angelegten Taskplaner-Tasks, über den Updates ohne
    // erneuten UAC-Dialog installiert werden (siehe InstallUpdateAsync).
    private const string ScheduledTaskName = "SerialManagerUpdate";

    // Muss exakt zu "DestDir" im [Dirs]-Abschnitt von build/installer.iss
    // passen - dort wird dieser Ordner mit Schreibrechten für normale
    // Benutzer angelegt (Permissions: users-modify), damit die Anwendung
    // (ohne Adminrechte) den heruntergeladenen Installer dort ablegen kann,
    // bevor der mit SYSTEM-Rechten laufende Taskplaner-Task ihn ausführt.
    private const string StagingDirectory = @"C:\ProgramData\SerialManager\PendingUpdate";

    // Muss auf das tatsächliche GitHub-Repository zeigen, in dem die
    // Releases (siehe .github/workflows/release.yml) veröffentlicht werden.
    private const string GitHubOwnerAndRepo = "Technofreaklol/Serial_Manager";

    private const string GitHubApiLatestReleaseUrl =
        "https://api.github.com/repos/" + GitHubOwnerAndRepo + "/releases/latest";

    // Name des Installer-Assets, das der Release-Workflow an jedes
    // GitHub-Release anhängt (siehe build/installer.iss "OutputBaseFilename"
    // und .github/workflows/release.yml).
    private const string InstallerAssetName = "SerialManagerSetup.exe";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Die GitHub-API verlangt zwingend einen User-Agent-Header - ohne
        // ihn antwortet sie mit "403 Forbidden".
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SerialManager", GetCurrentVersion().ToString()));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    /// <summary>
    /// True, wenn die Anwendung aus einem "Program Files"-Ordner heraus
    /// läuft (also über den Installer installiert wurde). False im
    /// Entwicklungsbetrieb (Visual Studio, "dotnet run") oder wenn die
    /// EXE von irgendwo anders gestartet wurde - dort ergibt eine
    /// Update-Prüfung keinen Sinn.
    /// </summary>
    public bool IsInstalled
    {
        get
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                string programFiles64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                return exePath.StartsWith(programFiles64, StringComparison.OrdinalIgnoreCase) ||
                       exePath.StartsWith(programFiles86, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Liefert die Version der aktuell laufenden Anwendung (aus
    /// &lt;Version&gt; in der .csproj, zur Build-Zeit in die Assembly
    /// übernommen).
    /// </summary>
    public static Version GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Prüft auf GitHub, ob eine neuere Version als die aktuell laufende
    /// verfügbar ist. Gibt null zurück, wenn keine Aktualisierung
    /// vorliegt, die Anwendung nicht installiert ist oder GitHub nicht
    /// erreichbar ist (z. B. kein Internetzugang) - ein Update-Check ist
    /// immer "Best effort" und darf die Anwendung nie zum Absturz
    /// bringen.
    /// </summary>
    public async Task<UpdateCheckResult?> CheckForUpdatesAsync()
    {
        if (!IsInstalled)
            return null;

        try
        {
            using var response = await HttpClient.GetAsync(GitHubApiLatestReleaseUrl);

            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            var root = json.RootElement;

            string? tagName = root.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            // Tags haben das Format "v1.2.3" (siehe .githooks/post-commit).
            string versionText = tagName.TrimStart('v', 'V');

            if (!Version.TryParse(versionText, out var latestVersion))
                return null;

            if (latestVersion <= GetCurrentVersion())
                return null;

            // "body" = der Freitext, den man beim Erstellen des GitHub-
            // Release eingibt (bzw. "--notes" bei "gh release create") -
            // wird unverändert als Versionshinweise angezeigt
            // (UpdateAvailableWindow). Kann fehlen/leer sein.
            string releaseNotes = root.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString() ?? ""
                : "";

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString()
                    : null;

                if (!string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? downloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp)
                    ? urlProp.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    return null;

                return new UpdateCheckResult(latestVersion, downloadUrl, InstallerAssetName, releaseNotes);
            }

            // Release gefunden, aber kein passendes Installer-Asset
            // angehängt - sollte bei einem vollständigen Release-Workflow
            // nicht vorkommen.
            return null;
        }
        catch
        {
            // Kein Internetzugang, GitHub nicht erreichbar, unerwartetes
            // Antwortformat o. Ä.
            return null;
        }
    }

    /// <summary>
    /// Lädt den Installer des gefundenen Updates in einen temporären
    /// Ordner herunter und gibt den vollständigen Dateipfad zurück.
    /// "progress" wird laufend mit dem Fortschritt in Prozent (0-100)
    /// aufgerufen (für die Fortschrittsanzeige in UpdateAvailableWindow) -
    /// wenn der Server keine Content-Length mitliefert, bleibt der
    /// Fortschritt bei 0, bis der Download abgeschlossen ist (dann 100).
    /// </summary>
    public async Task<string> DownloadInstallerAsync(UpdateCheckResult update, IProgress<double>? progress = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SerialManagerUpdate");
        Directory.CreateDirectory(tempDir);

        string filePath = Path.Combine(tempDir, update.FileName);

        using var response = await HttpClient.GetAsync(
            update.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(filePath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalRead += bytesRead;

            if (totalBytes is > 0)
                progress?.Report((double)totalRead / totalBytes.Value * 100);
        }

        progress?.Report(100);

        return filePath;
    }

    /// <summary>
    /// Installiert den heruntergeladenen Installer und beendet anschließend
    /// die laufende Anwendung. Kehrt im Erfolgsfall NICHT zurück.
    ///
    /// Bevorzugter Weg (wenn ScheduledTaskName existiert): der Installer
    /// wird in StagingDirectory abgelegt und über den bei einer früheren
    /// Installation angelegten Taskplaner-Task mit höchsten Rechten
    /// ausgeführt - OHNE erneuten UAC-Dialog, weil die Rechte-Erhöhung
    /// bereits beim Anlegen des Tasks (durch einen admin-pflichtigen
    /// Installations-/Update-Lauf) genehmigt wurde. Der Task läuft als
    /// SYSTEM unsichtbar in Sitzung 0 (kein Desktop-Zugriff) und beendet
    /// dabei (CLOSEAPPLICATIONS) zwangsweise auch diesen Prozess selbst -
    /// deshalb wird VOR dem Auslösen des Tasks ein eigenständiger,
    /// verdeckter PowerShell-Prozess gestartet (StartDetachedUpdateWatcher),
    /// der unabhängig von diesem Prozess auf das Ergebnis wartet und die
    /// Anwendung danach automatisch neu startet. Diese Methode selbst
    /// kehrt nach dem Auslösen des Tasks nicht zurück, sondern beendet
    /// sich sofort selbst.
    ///
    /// Fallback (Task existiert noch nicht - z. B. die erste
    /// Aktualisierung ab einer Version von VOR Einführung dieses
    /// Mechanismus): klassischer "runas"-Aufruf mit einem einzelnen
    /// UAC-Dialog wie bisher. Der Installer legt dabei den Task gleich mit
    /// an (siehe build/installer.iss), sodass JEDES weitere Update danach
    /// ohne UAC auskommt.
    /// </summary>
    public async Task InstallUpdateAsync(
        string downloadedInstallerPath,
        Version targetVersion,
        IProgress<string>? statusProgress = null)
    {
        if (!ScheduledUpdateTaskExists())
        {
            statusProgress?.Report("Administratorrechte werden angefordert...");
            RunInstallerElevatedAndShutdown(downloadedInstallerPath);
            return;
        }

        statusProgress?.Report("Update wird installiert...");

        Directory.CreateDirectory(StagingDirectory);

        string stagedInstaller = Path.Combine(StagingDirectory, InstallerAssetName);
        File.Copy(downloadedInstallerPath, stagedInstaller, true);

        string exePath = GetInstalledExePath();

        // WICHTIG: Der Taskplaner-Task installiert mit "/CLOSEAPPLICATIONS" -
        // das beendet zwangsweise GENAU DIESEN Prozess (SerialManager.exe
        // hält die eigenen .exe/.dll-Dateien gesperrt), sobald der
        // Installer startet. Ein Warten auf das Ergebnis und ein
        // anschließender Neustart HIER IM PROZESS (wie in einer früheren
        // Version dieser Methode) würde deshalb nie zu Ende laufen - die
        // Anwendung würde einfach kommentarlos verschwinden, ohne sich je
        // selbst neu zu starten. Stattdessen wird VOR dem Auslösen des
        // Tasks ein von diesem Prozess unabhängiges (nicht an dessen
        // Lebensdauer gebundenes), verstecktes PowerShell-Skript gestartet:
        // Es läuft in der normalen interaktiven Sitzung des angemeldeten
        // Benutzers (im Gegensatz zu allem, was der SYSTEM-Task selbst
        // starten könnte - Sitzung 0 hat keinen Desktop-Zugriff), wartet
        // dort in Ruhe auf die neue Version und startet die Anwendung
        // danach automatisch neu.
        StartDetachedUpdateWatcher(exePath, targetVersion);

        var runInfo = new ProcessStartInfo("schtasks.exe")
        {
            Arguments = $"/Run /TN \"{ScheduledTaskName}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var runProcess = Process.Start(runInfo))
        {
            // Wartet nur auf schtasks.exe selbst (das reine Auslösen des
            // Tasks) - das kehrt praktisch sofort zurück, OHNE auf das
            // Ende der eigentlichen (im Hintergrund als SYSTEM laufenden)
            // Installation zu warten. Das Warten auf die Installation
            // übernimmt ab jetzt ausschließlich der oben gestartete,
            // unabhängige PowerShell-Watcher.
            await runProcess!.WaitForExitAsync();
        }

        statusProgress?.Report("Update wird im Hintergrund abgeschlossen...");

        // Beendet sich selbst, statt darauf zu warten, dass
        // "/CLOSEAPPLICATIONS" des gleich startenden Installers diesen
        // Prozess zwangsweise beendet - sauberer und schneller. Der oben
        // gestartete Watcher übernimmt ab hier den automatischen Neustart
        // nach abgeschlossenem Update.
        Environment.Exit(0);
    }

    // Startet einen eigenständigen, verdeckten PowerShell-Prozess, der NICHT
    // an die Lebensdauer von SerialManager.exe gebunden ist (also auch dann
    // weiterläuft, wenn dieser Prozess gleich durch "/CLOSEAPPLICATIONS"
    // des Installers oder durch das eigene Environment.Exit(0) beendet
    // wird) und in der normalen interaktiven Benutzersitzung ausgeführt
    // wird (nicht in Sitzung 0/SYSTEM wie der Taskplaner-Task selbst -
    // deshalb kann NUR dieser Watcher die Anwendung nach dem Update
    // sichtbar neu starten).
    private static void StartDetachedUpdateWatcher(string exePath, Version targetVersion)
    {
        Directory.CreateDirectory(StagingDirectory);

        // Liegt bewusst im selben (per [Dirs]/"users-modify" für normale
        // Benutzer beschreibbaren) Ordner wie der gestagte Installer.
        string scriptPath = Path.Combine(StagingDirectory, "update-watcher.ps1");

        File.WriteAllText(scriptPath, UpdateWatcherScript);

        var psi = new ProcessStartInfo("powershell.exe")
        {
            Arguments =
                "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass " +
                $"-File \"{scriptPath}\" -ExePath \"{exePath}\" -TargetVersion \"{targetVersion}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        // Bewusst KEIN "using"/Warten auf diesen Prozess: Er soll
        // unabhängig von diesem (gleich beendeten) Prozess weiterlaufen
        // (siehe Kommentar in InstallUpdateAsync).
        Process.Start(psi);
    }

    // Wartet (im eigenständigen, von SerialManager.exe unabhängigen
    // PowerShell-Prozess - siehe StartDetachedUpdateWatcher) bis die
    // installierte .exe die Zielversion erreicht hat, und startet die
    // Anwendung danach automatisch neu. "[Version]"-Vergleich in
    // PowerShell verhält sich wie Version.CompareTo in .NET: ein fehlender
    // Revisionsteil (z. B. Ziel "1.0.34" aus einem GitHub-Tag) zählt als
    // -1 und ist damit automatisch kleiner als die von .NET beim Build
    // erzeugte 4-teilige FileVersion (z. B. "1.0.34.0") - ">=" ist daher
    // korrekt, sobald die neue Version installiert ist.
    private const string UpdateWatcherScript = """
    param(
        [string]$ExePath,
        [string]$TargetVersion
    )

    $target = [Version]$TargetVersion
    $deadline = (Get-Date).AddSeconds(90)

    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $ExePath) {
            try {
                $installedText = (Get-Item -LiteralPath $ExePath).VersionInfo.FileVersion
                if ($installedText) {
                    $installed = [Version]$installedText
                    if ($installed -ge $target) {
                        break
                    }
                }
            } catch {
                # Datei wird evtl. gerade vom Installer überschrieben -
                # beim nächsten Versuch erneut prüfen.
            }
        }
        Start-Sleep -Milliseconds 500
    }

    # Kurze zusätzliche Pause, damit der Installer die Datei sicher
    # freigegeben hat, bevor sie erneut gestartet wird.
    Start-Sleep -Milliseconds 500

    try {
        Start-Process -FilePath $ExePath
    } catch {
        # Best effort - falls der Neustart fehlschlägt, ist das Update an
        # dieser Stelle trotzdem bereits installiert; der Benutzer kann
        # die Anwendung einfach manuell erneut öffnen.
    }
    """;

    private static bool ScheduledUpdateTaskExists()
    {
        try
        {
            var queryInfo = new ProcessStartInfo("schtasks.exe")
            {
                Arguments = $"/Query /TN \"{ScheduledTaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(queryInfo);
            process!.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            // schtasks.exe nicht gefunden o. Ä. - dann lieber der sichere
            // "runas"-Fallback als ein Absturz.
            return false;
        }
    }

    private static string GetInstalledExePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SerialManager",
            "SerialManager.exe");

    /// <summary>
    /// Fallback für Installationen ohne ScheduledTaskName (siehe
    /// InstallUpdateAsync): startet den Installer mit angeforderten
    /// Administratorrechten (UAC-Dialog) und beendet anschließend sofort
    /// die aktuell laufende Anwendung. Kehrt nicht zurück.
    /// </summary>
    private static void RunInstallerElevatedAndShutdown(string installerFilePath)
    {
        var startInfo = new ProcessStartInfo(installerFilePath)
        {
            UseShellExecute = true,

            // "runas" fordert Windows auf, den UAC-Dialog für
            // Administratorrechte anzuzeigen - ohne diese Rechte kann der
            // Installer nicht nach "C:\Program Files\SerialManager"
            // schreiben (und, beim ersten Mal, auch nicht den
            // ScheduledTaskName-Task anlegen). "/SILENT" (statt
            // "/VERYSILENT") zeigt weiterhin eine Fortschrittsanzeige an,
            // aber keine weiteren Dialoge/Rückfragen mehr.
            // "/CLOSEAPPLICATIONS" lässt Inno Setup laufende Instanzen von
            // SerialManager.exe automatisch schließen, falls trotz des
            // sofortigen Beendens hier noch eine zweite Instanz offen sein
            // sollte.
            Verb = "runas",
            Arguments = "/SILENT /NORESTART /CLOSEAPPLICATIONS"
        };

        Process.Start(startInfo);

        Environment.Exit(0);
    }
}
