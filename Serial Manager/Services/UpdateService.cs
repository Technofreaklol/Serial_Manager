using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace SerialManager.Services;

/// <summary>
/// Kapselt die Update-Logik auf Basis von Velopack.
///
/// Als Update-Quelle dient das GitHub-Repository des Projekts: Ein
/// "vpk"-Release-Vorgang lädt Setup.exe + Release-Dateien in die
/// GitHub Releases des Repos hoch, und diese Klasse prüft dort, ob
/// eine neuere Version verfügbar ist.
///
/// Wichtig: Ein Update-Check ergibt nur Sinn, wenn die Anwendung über
/// den Velopack-Installer installiert wurde (<see cref="IsInstalled"/>).
/// Im Entwicklungsbetrieb (z. B. Start über Visual Studio/"dotnet run")
/// ist das nicht der Fall - dort wird der Check übersprungen.
/// </summary>
public class UpdateService
{
    // Muss auf das tatsächliche GitHub-Repository zeigen, in dem die
    // Releases (per "vpk upload github") veröffentlicht werden.
    private const string GitHubRepoUrl =
        "https://github.com/Technofreaklol/Serial_Manager";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        // accessToken: null -> öffentliches Repo, unauthentifizierter
        // Zugriff (60 Anfragen/Stunde/IP laut GitHub-Limit, für einen
        // gelegentlichen Update-Check völlig ausreichend).
        // prerelease: false -> Vorabversionen werden ignoriert.
        _manager = new UpdateManager(
            new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: false));
    }

    /// <summary>
    /// True, wenn die Anwendung über den Velopack-Installer installiert
    /// wurde. False im Entwicklungsbetrieb (Visual Studio, dotnet run).
    /// </summary>
    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>
    /// Prüft auf GitHub, ob eine neuere Version verfügbar ist.
    /// Gibt null zurück, wenn keine Aktualisierung vorliegt, die
    /// Anwendung nicht installiert wurde oder GitHub nicht erreichbar
    /// ist (z. B. kein Internetzugang).
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!IsInstalled)
            return null;

        try
        {
            return await _manager.CheckForUpdatesAsync();
        }
        catch
        {
            // Kein Internetzugang, GitHub nicht erreichbar o. Ä. -
            // der Update-Check ist ein "Best effort" und darf die
            // Anwendung niemals zum Absturz bringen.
            return null;
        }
    }

    /// <summary>
    /// Lädt das gefundene Update herunter.
    /// </summary>
    public Task DownloadUpdateAsync(UpdateInfo update, Action<int>? onProgress = null)
    {
        return _manager.DownloadUpdatesAsync(update, onProgress);
    }

    /// <summary>
    /// Wendet das heruntergeladene Update an und startet die
    /// Anwendung automatisch neu. Diese Methode kehrt nicht zurück -
    /// der aktuelle Prozess wird beendet.
    /// </summary>
    public void ApplyUpdateAndRestart(UpdateInfo update)
    {
        _manager.ApplyUpdatesAndRestart(update);
    }
}
