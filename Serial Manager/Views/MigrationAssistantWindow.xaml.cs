using Microsoft.EntityFrameworkCore;
using SerialManager.Data;
using SerialManager.Models;
using SerialManager.Services;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SerialManager.Views;

public partial class MigrationAssistantWindow : Window
{
    private readonly BackupService _backupService = new();
    private readonly DatabaseInitializer _initializer = new();

    public MigrationAssistantWindow()
    {
        InitializeComponent();
        CheckStatus();
    }

    private void CheckStatus()
    {
        try
        {
            using var db = DbContextFactory.Create();

            if (!db.Database.IsMySql())
            {
                lblStatus.Text =
                    "Dieser Assistent ist nur für MySQL-Datenbanken gedacht. " +
                    "SQLite und SQL Server aktualisieren ihr Schema automatisch beim Start.";
                btnRun.IsEnabled = false;
                return;
            }

            var pending = db.Database.GetPendingMigrations().ToList();
            var tablesOk = CoreTablesExist(db);

            if (pending.Count == 0 && tablesOk)
            {
                lblStatus.Text = "Die Datenbank ist bereits auf dem neuesten Stand.";
                btnRun.IsEnabled = false;
                return;
            }

            var sb = new StringBuilder();

            if (pending.Count > 0)
            {
                sb.AppendLine($"{pending.Count} Migration(en) laut Verlauf ausstehend:");
                sb.AppendLine(string.Join("\n", pending));
                sb.AppendLine();
            }

            if (!tablesOk)
            {
                sb.AppendLine(
                    "⚠️ Mindestens eine erwartete Tabelle fehlt in der Datenbank, " +
                    "obwohl der Migrationsverlauf sie als angewendet führt " +
                    "(z. B. nach versehentlichem Leeren/Truncate der Datenbank).");
                sb.AppendLine();
            }

            sb.AppendLine("Vor der Aktualisierung wird automatisch ein Backup erstellt.");

            lblStatus.Text = sb.ToString();
            btnRun.IsEnabled = true;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Status konnte nicht geprüft werden:\n" + ex.Message;
            btnRun.IsEnabled = false;
        }
    }

    private static bool CoreTablesExist(SerialDbContext db)
    {
        try
        {
            _ = db.Articles.Take(1).Count();
            _ = db.Machines.Take(1).Count();
            _ = db.SerialHistories.Take(1).Count();
            _ = db.Settings.Take(1).Count();
            _ = db.Users.Take(1).Count();
            _ = db.AuditLogEntries.Take(1).Count();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Es wird zuerst ein Backup erstellt und anschließend die Datenbank aktualisiert/repariert. Fortfahren?",
            "Migration ausführen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        btnRun.IsEnabled = false;
        progressPanel.Visibility = Visibility.Visible;
        progressBar.IsIndeterminate = false;
        progressBar.Value = 0;
        txtProgressStatus.Text = "Backup wird vorbereitet...";

        var progress = new Progress<BackupProgress>(p =>
        {
            progressBar.Value = p.Percent;
            txtProgressStatus.Text = p.Message;
        });

        string backupFile;

        try
        {
            backupFile = await Task.Run(() => _backupService.CreateBackup(progress));
        }
        catch (Exception ex)
        {
            progressPanel.Visibility = Visibility.Collapsed;
            MessageBox.Show(
                "Das Backup ist fehlgeschlagen, die Aktualisierung wurde nicht gestartet:\n\n" + ex.Message,
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            btnRun.IsEnabled = true;
            return;
        }

        progressBar.IsIndeterminate = true;
        txtProgressStatus.Text = "Datenbank wird aktualisiert/repariert...";

        bool success = false;
        string resultMessage = string.Empty;

        await Task.Run(() =>
        {
            success = _initializer.InitializeDatabase(out resultMessage);
        });

        progressPanel.Visibility = Visibility.Collapsed;

        if (success)
        {
            MessageBox.Show(
                $"{resultMessage}\n\nBackup vor der Aktualisierung:\n{backupFile}",
                "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

            CheckStatus();
        }
        else
        {
            MessageBox.Show(
                "Die Aktualisierung ist fehlgeschlagen. Das zuvor erstellte Backup kann über " +
                "'Backup wiederherstellen' zurückgespielt werden.\n\n" +
                $"Backup-Datei:\n{backupFile}\n\nFehler:\n{resultMessage}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

            btnRun.IsEnabled = true;
        }
    }
}