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
///   3. RunInstallerAndShutdown: Startet dieses Setup.exe mit
///      angeforderten Administratorrechten (UAC-Dialog) und beendet die
///      laufende Anwendung sofort, damit der Installer die Dateien im
///      Installationsordner überschreiben kann.
///
/// Der Benutzer muss den UAC-Dialog bestätigen - das ist der Preis für
/// eine "echte" Installation unter Program Files statt eines
/// Pro-Benutzer-Installs ohne Admin-Rechte.
/// </summary>
public class UpdateService
{
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
    /// Startet den heruntergeladenen Installer mit angeforderten
    /// Administratorrechten (UAC-Dialog) und beendet anschließend sofort
    /// die aktuell laufende Anwendung, damit der Installer die Dateien im
    /// Installationsordner überschreiben kann. Diese Methode kehrt nicht
    /// zurück.
    /// </summary>
    public void RunInstallerAndShutdown(string installerFilePath)
    {
        var startInfo = new ProcessStartInfo(installerFilePath)
        {
            UseShellExecute = true,

            // "runas" fordert Windows auf, den UAC-Dialog für
            // Administratorrechte anzuzeigen - ohne diese Rechte kann der
            // Installer nicht nach "C:\Program Files\SerialManager"
            // schreiben. "/SILENT" (statt "/VERYSILENT") zeigt weiterhin
            // eine Fortschrittsanzeige an, aber keine weiteren Dialoge/
            // Rückfragen mehr. "/CLOSEAPPLICATIONS" lässt Inno Setup
            // laufende Instanzen von SerialManager.exe automatisch
            // schließen, falls trotz des sofortigen Beendens hier noch
            // eine zweite Instanz offen sein sollte.
            Verb = "runas",
            Arguments = "/SILENT /NORESTART /CLOSEAPPLICATIONS"
        };

        Process.Start(startInfo);

        Environment.Exit(0);
    }
}
