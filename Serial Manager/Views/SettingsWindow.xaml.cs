using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly WindowTitleService _titleService = new();

    // Kleine Auswahl an Vorlagenfarben, die als Klick-Vorschläge angezeigt
    // werden - keine feste Liste von Optionen, die eigene Hex-Eingabe im
    // Textfeld daneben funktioniert unabhängig davon mit jeder Farbe.
    private static readonly string[] ColorPresets =
    {
        ThemeService.DefaultButtonColorHex, // Blau (Standard)
        "#E32D8C", // Pink
        "#C62828", // Rot
        "#F57C00", // Orange
        "#6A1B9A", // Lila
        "#00838F", // Türkis
        "#616161"  // Grau
    };

    public SettingsWindow()
    {
        InitializeComponent();

        var configService = new ApplicationConfigurationService();
        var config = configService.Load();

        Title = _titleService.GetTitle("Einstellungen");

        BuildColorPresets();
        LoadSettings();
    }

    private void BuildColorPresets()
    {
        foreach (var hex in ColorPresets)
        {
            if (!ThemeService.TryParseColor(hex, out var color))
                continue;

            var swatch = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(color),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Tag = hex,
                ToolTip = hex
            };

            swatch.Click += (_, _) => txtButtonColor.Text = hex;

            panelColorPresets.Children.Add(swatch);
        }
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

        txtButtonColor.Text =
            ThemeService.GetSavedButtonColorHex();

        UpdateColorPreview();
    }

    private void txtButtonColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
    }

    // Öffnet den nativen Windows-Farbauswahldialog. System.Windows.Forms wird
    // hier bewusst nur voll qualifiziert verwendet (nicht per "using"), weil
    // sonst "Button" und andere Typennamen mit System.Windows.Controls
    // kollidieren würden.
    private void BtnPickColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            AnyColor = true,
        };

        if (ThemeService.TryParseColor(txtButtonColor.Text, out var current))
        {
            dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var picked = dialog.Color;

        txtButtonColor.Text = $"#{picked.R:X2}{picked.G:X2}{picked.B:X2}";
    }

    private void UpdateColorPreview()
    {
        if (ThemeService.TryParseColor(txtButtonColor.Text, out var color))
        {
            borderColorPreview.Background = new SolidColorBrush(color);
        }
        else
        {
            borderColorPreview.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtBackupCount.Text, out int backupCount) || backupCount < 1)
        {
            MessageBox.Show(
                "Bitte eine gültige Backup-Anzahl eingeben.");

            return;
        }

        var buttonColorHex = txtButtonColor.Text.Trim();

        if (!ThemeService.TryParseColor(buttonColorHex, out _))
        {
            MessageBox.Show(
                "Bitte eine gültige Farbe im Format #RRGGBB eingeben (z. B. #1976D2).");

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

        ThemeService.SaveButtonColor(buttonColorHex);
        ThemeService.ApplyButtonColor(buttonColorHex);

        WindowTitleService.NotifyTitleChanged();

        DialogResult = true;

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}