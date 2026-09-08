using System.Windows;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly WindowTitleService _titleService = new();


    public SettingsWindow()
    {
        InitializeComponent();

        var configService = new ApplicationConfigurationService();
        var config = configService.Load();

        Title = _titleService.GetTitle("Einstellungen");

        LoadSettings();
    }

    private void LoadSettings()
    {
        var configService = new ApplicationConfigurationService();
        var config = configService.Load();

        txtCompany.Text =
            config.CompanyName;

        txtBackupCount.Text =
            _settingsService.GetValue("BackupCount", "20");

        chkAutoBackup.IsChecked =
            _settingsService.GetBool("AutoBackup");

        chkShowLabelPreview.IsChecked =
            config.ShowLabelPreview;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtBackupCount.Text, out int backupCount) || backupCount < 1)
        {
            MessageBox.Show(
                "Bitte eine gültige Backup-Anzahl eingeben.");

            return;
        }
        var configService = new ApplicationConfigurationService();
        var config = configService.Load();

        config.CompanyName = txtCompany.Text.Trim();

        _settingsService.SetInt(
            "BackupCount",
            backupCount);

        _settingsService.SetBool(
            "AutoBackup",
            chkAutoBackup.IsChecked == true);

        config.ShowLabelPreview =
    chkShowLabelPreview.IsChecked == true;

        configService.Save(config);

        WindowTitleService.NotifyTitleChanged();

        DialogResult = true;

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}