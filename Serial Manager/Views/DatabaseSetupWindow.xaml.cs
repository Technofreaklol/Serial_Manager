using System.Windows;
using System.Windows.Controls;
using SerialManager.Models;
using SerialManager.Services;
using Microsoft.Win32;

namespace SerialManager.Views;

public partial class DatabaseSetupWindow : Window
{
    private readonly DatabaseConfigurationService _configService = new();
    private readonly BackupService _backupService = new();
    private readonly WindowTitleService _titleService = new();
    private readonly DatabaseInitializer _initializer = new();
    private readonly DatabaseDiagnosticService _diagnostic = new();

    private DatabaseConfiguration _config = new();

    public DatabaseSetupWindow()
    {
        InitializeComponent();

        Title = _titleService.GetTitle("Datenbank");

        LoadConfiguration();

        txtStatus.Text =
            "Status noch nicht geprüft.";
    }

    private void LoadConfiguration()
    {
        _config = _configService.Load();

        cmbProvider.SelectedIndex = _config.Provider switch
        {
            "MySQL" => 1,
            "MSSQL" => 2,
            _ => 0
        };

        txtSQLiteFile.Text = _config.SQLite.File;

        txtServer.Text = _config.MySQL.Server;
        txtPort.Text = _config.MySQL.Port.ToString();
        txtDatabase.Text = _config.MySQL.Database;
        txtUser.Text = _config.MySQL.Username;
        txtPassword.Password = _config.MySQL.Password;

        txtMsSqlServer.Text = _config.MSSQL.Server;
        txtMsSqlPort.Text = _config.MSSQL.Port > 0
            ? _config.MSSQL.Port.ToString()
            : "";
        txtMsSqlDatabase.Text = _config.MSSQL.Database;
        txtMsSqlUser.Text = _config.MSSQL.Username;
        txtMsSqlPassword.Password = _config.MSSQL.Password;

        UpdateControls();
    }

    private void SaveConfiguration()
    {
        _config.Provider = cmbProvider.SelectedIndex switch
        {
            1 => "MySQL",
            2 => "MSSQL",
            _ => "SQLite"
        };

        _config.SQLite.File = txtSQLiteFile.Text.Trim();

        _config.MySQL.Server = txtServer.Text.Trim();

        if (int.TryParse(txtPort.Text, out int port))
            _config.MySQL.Port = port;

        _config.MySQL.Database = txtDatabase.Text.Trim();
        _config.MySQL.Username = txtUser.Text.Trim();
        _config.MySQL.Password = txtPassword.Password;

        _config.MSSQL.Server = txtMsSqlServer.Text.Trim();

        _config.MSSQL.Port = int.TryParse(txtMsSqlPort.Text, out int mssqlPort)
            ? mssqlPort
            : 0;

        _config.MSSQL.Database = txtMsSqlDatabase.Text.Trim();
        _config.MSSQL.Username = txtMsSqlUser.Text.Trim();
        _config.MSSQL.Password = txtMsSqlPassword.Password;

        _configService.Save(_config);
    }

    private void UpdateControls()
    {
        int selected = cmbProvider.SelectedIndex;

        grpSQLite.Visibility = selected == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        grpMySql.Visibility = selected == 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        grpMsSql.Visibility = selected == 2
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void cmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateControls();
    }

    private void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        if (_initializer.TestServer(out string message))
            ShowSuccess(message);
        else
            ShowError(message);
    }

    private void CreateDatabase_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        if (_initializer.CreateDatabase(out string message))
            ShowSuccess(message);
        else
            ShowError(message);
    }

    private void Migrate_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        if (_initializer.InitializeDatabase(out string message))
            ShowSuccess(message);
        else
            ShowError(message);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Diese Funktion wird als Nächstes implementiert.",
            "SQLite → MySQL",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BrowseSQLite_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQLite Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*",
            CheckFileExists = false
        };

        if (dialog.ShowDialog() == true)
        {
            txtSQLiteFile.Text = dialog.FileName;
        }
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();
        RefreshDiagnosticStatus();
    }

    private void RefreshDiagnosticStatus()
    {
        txtStatus.Clear();

        try
        {
            var status = _diagnostic.GetStatus();

            ShowInfo($"Provider:        {status.Provider}");
            ShowInfo($"Server:          {status.Server}");
            ShowInfo($"Datenbank:       {status.DatabaseName}");
            ShowInfo($"Verbindung:      {(status.CanConnect ? "🟢 Verbunden" : "🔴 Keine Verbindung")}");
            ShowInfo($"Tabellen:        {(status.TablesOk ? "🟢 OK" : "🔴 Fehler")}");
            ShowInfo($"Seriennummern-Index: {(status.SerialIndexOk ? "🟢 OK" : "🔴 Fehlt")}");
            ShowInfo($"Alter globaler Index: {(status.LegacySerialIndexFound ? "⚠️ Vorhanden" : "🟢 Nicht vorhanden")}");
            ShowInfo($"Migrationen:     {status.MigrationStatus}");
            ShowInfo("");
            ShowInfo($"Artikel:          {status.ArticleCount}");
            ShowInfo($"Maschinen:        {status.MachineCount}");
            ShowInfo($"Historie:         {status.HistoryCount}");
            ShowInfo($"Einstellungen:    {status.SettingsCount}");

            if (!string.IsNullOrWhiteSpace(status.Error))
            {
                ShowInfo("");
                ShowInfo("Fehler:");
                ShowInfo(status.Error);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write("Datenbankdiagnose fehlgeschlagen.", ex);
            ShowError(ex.Message);
        }
    }

    private void RepairDatabase_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        var result = MessageBox.Show(
            "Der Seriennummern-Index wird geprüft und bei Bedarf repariert.\n\nFortfahren?",
            "Datenbank reparieren",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        if (_diagnostic.Repair(out string message))
        {
            DiagnosticLogService.Write("Datenbankreparatur erfolgreich: " + message);
            ShowSuccess("✅ " + message);
            RefreshDiagnosticStatus();
        }
        else
        {
            DiagnosticLogService.Write("Datenbankreparatur fehlgeschlagen: " + message);
            ShowError(message);
        }
    }

    private void CopyDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(txtStatus.Text);
            MessageBox.Show(
                "Die Diagnose wurde in die Zwischenablage kopiert.",
                "Datenbankdiagnose",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write("Diagnose konnte nicht kopiert werden.", ex);
            ShowError(ex.Message);
        }
    }


    private void ShowSuccess(string message)
    {
        txtStatus.Clear();
        txtStatus.AppendText("✅ " + message);
    }

    private void ShowError(string message)
    {
        txtStatus.Clear();
        txtStatus.AppendText("❌ " + message);
    }

    private void ShowInfo(string message)
    {
        txtStatus.AppendText(message + Environment.NewLine);
        txtStatus.ScrollToEnd();
    }

    private async void Initialize_Click(object sender, RoutedEventArgs e)
    {
        SaveConfiguration();

        btnInitialize.IsEnabled = false;
        progressPanel.Visibility = Visibility.Visible;
        progressBar.IsIndeterminate = false;
        progressBar.Value = 0;
        txtProgressStatus.Text = "Prüfe vorhandenen Datenbestand...";

        string? backupFile = null;

        try
        {
            if (_backupService.HasExistingDatabase())
            {
                var progress = new Progress<BackupProgress>(p =>
                {
                    progressBar.Value = p.Percent;
                    txtProgressStatus.Text = p.Message;
                });

                backupFile = await Task.Run(() => _backupService.CreateBackup(progress));
            }
            else
            {
                txtProgressStatus.Text = "Kein bestehender Datenbestand gefunden – kein Backup nötig.";
            }
        }
        catch (Exception ex)
        {
            progressPanel.Visibility = Visibility.Collapsed;
            btnInitialize.IsEnabled = true;
            ShowError("Backup fehlgeschlagen, Einrichtung wurde abgebrochen:\n\n" + ex.Message);
            return;
        }

        progressBar.IsIndeterminate = true;
        txtProgressStatus.Text = "Datenbank wird eingerichtet/aktualisiert...";

        bool success = false;
        string message = string.Empty;

        await Task.Run(() =>
        {
            success = _initializer.Initialize(out message);
        });

        progressPanel.Visibility = Visibility.Collapsed;
        btnInitialize.IsEnabled = true;

        string backupNote = backupFile != null
            ? $"\n\nBackup vor der Aktualisierung:\n{backupFile}"
            : "";

        if (success)
        {
            ShowSuccess(message + backupNote);
            RefreshStatus_Click(sender, e);
        }
        else
        {
            ShowError(message + backupNote);
        }
    }
}
