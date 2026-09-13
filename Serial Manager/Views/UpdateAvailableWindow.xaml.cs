using System;
using System.Windows;
using SerialManager.Services;

namespace SerialManager.Views;

/// <summary>
/// Zeigt ein gefundenes Update mit Versionsnummer und Versionshinweisen
/// (aus dem "body"-Feld des GitHub-Release, siehe UpdateService) an und
/// bietet drei Möglichkeiten: sofort aktualisieren (mit
/// Download-Fortschrittsanzeige), später erinnern (3 Tage Pause für genau
/// diese Version) oder die Version dauerhaft überspringen. Ersetzt die
/// vorherige, einfache MessageBox-Abfrage in MainWindow.
/// </summary>
public partial class UpdateAvailableWindow : Window
{
    private readonly UpdateCheckResult _update;
    private readonly UpdateService _updateService;
    private readonly SettingsService _settingsService = new();

    public UpdateAvailableWindow(UpdateCheckResult update, UpdateService updateService)
    {
        InitializeComponent();

        _update = update;
        _updateService = updateService;

        lblVersions.Text =
            $"Installiert: {UpdateService.GetCurrentVersion()}    →    Neu: {update.Version}";

        lblReleaseNotes.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "Keine Versionshinweise vorhanden."
            : update.ReleaseNotes;
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        panelButtons.IsEnabled = false;
        panelProgress.Visibility = Visibility.Visible;

        var downloadProgress = new Progress<double>(percent =>
        {
            progressBar.Value = percent;

            lblProgressStatus.Text = percent >= 100
                ? "Download abgeschlossen..."
                : $"Update wird heruntergeladen... ({percent:0}%)";
        });

        // Für die eigentliche Installation gibt es keinen Prozentwert mehr
        // (siehe UpdateService.InstallUpdateAsync - läuft ggf. unsichtbar im
        // Hintergrund) - nur noch Textstatus, Balken läuft "unbestimmt".
        var installStatusProgress = new Progress<string>(status =>
        {
            lblProgressStatus.Text = status;
        });

        try
        {
            string installerPath = await _updateService.DownloadInstallerAsync(_update, downloadProgress);

            // Vor der Installation den Später-erinnern-/Überspringen-
            // Zustand zurücksetzen, damit nach dem Update keine veralteten
            // Einträge übrig bleiben (die nächste, dann wieder neue Version
            // soll ganz normal gemeldet werden).
            _settingsService.SetValue("UpdateSkipVersion", "");
            _settingsService.SetValue("UpdateSnoozedVersion", "");
            _settingsService.SetValue("UpdateSnoozedUntilUtc", "");

            progressBar.IsIndeterminate = true;

            // Kehrt im Erfolgsfall nicht zurück - beendet die Anwendung
            // selbst (siehe UpdateService.InstallUpdateAsync).
            await _updateService.InstallUpdateAsync(installerPath, _update.Version, installStatusProgress);
        }
        catch (Exception ex)
        {
            progressBar.IsIndeterminate = false;
            panelProgress.Visibility = Visibility.Collapsed;
            panelButtons.IsEnabled = true;

            MessageBox.Show(
                "Das Update konnte nicht heruntergeladen/installiert werden:\n\n" + ex.Message,
                "Fehler beim Update",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        // 3 Tage Pause - danach wird beim automatischen Start-Check wieder
        // ganz normal auf diese (oder eine neuere) Version hingewiesen.
        _settingsService.SetValue("UpdateSnoozedVersion", _update.Version.ToString());
        _settingsService.SetValue(
            "UpdateSnoozedUntilUtc",
            DateTime.UtcNow.AddDays(3).ToString("o"));

        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Version {_update.Version} wird dann nicht mehr automatisch vorgeschlagen " +
            "(über \"Nach Updates suchen\" im Menü bleibt sie weiterhin manuell abrufbar).\n\n" +
            "Fortfahren?",
            "Version überspringen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _settingsService.SetValue("UpdateSkipVersion", _update.Version.ToString());

        Close();
    }
}
