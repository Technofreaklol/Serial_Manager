using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class SettingsWindow : Window
{
    // Maximale Logo-Dateigröße: das Bild wird als Base64-Text in der
    // Settings-Tabelle der Datenbank gespeichert - ohne Obergrenze könnte
    // ein großes Foto die Datenbank unnötig aufblähen und jeden
    // Einstellungs-/Etikettenzugriff verlangsamen.
    private const int MaxLogoBytes = 500 * 1024;

    private readonly SettingsService _settingsService = new();
    private readonly LabelLayoutService _labelLayoutService = new();
    private readonly WindowTitleService _titleService = new();

    // Base64 des aktuell im Fenster ausgewählten Logos (noch nicht
    // gespeichert, bis auf "Speichern" geklickt wird). Leerer String =
    // kein Logo.
    private string _pendingLogoBase64 = "";

    // Kleine Auswahl an Vorlagenfarben, die als Klick-Vorschläge angezeigt
    // werden - keine feste Liste von Optionen, die eigene Hex-Eingabe im
    // Textfeld daneben funktioniert unabhängig davon mit jeder Farbe.
    private static readonly string[] ColorPresets =
    {
        ThemeService.DefaultButtonColorHex, // Blau (Standard)
        "#2E7D32", // Grün
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

        var labelLayout = _labelLayoutService.Load();

        chkLabelCompany.IsChecked = labelLayout.ShowCompanyName;
        chkLabelArticle.IsChecked = labelLayout.ShowArticleNumber;
        chkLabelDescription.IsChecked = labelLayout.ShowDescription;
        chkLabelSerial.IsChecked = labelLayout.ShowSerialNumber;
        chkLabelMachine.IsChecked = labelLayout.ShowMachine;
        chkLabelDate.IsChecked = labelLayout.ShowDate;
        chkLabelOperator.IsChecked = labelLayout.ShowOperator;
        chkLabelQr.IsChecked = labelLayout.ShowQrCode;

        txtLabelWidthMm.Text = labelLayout.WidthMm.ToString(System.Globalization.CultureInfo.CurrentCulture);
        txtLabelHeightMm.Text = labelLayout.HeightMm.ToString(System.Globalization.CultureInfo.CurrentCulture);

        _pendingLogoBase64 = labelLayout.LogoBase64;
        UpdateLogoPreview();
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

    private void BtnChooseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dialog.ShowDialog() != true)
            return;

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(dialog.FileName);
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Datei konnte nicht gelesen werden:\n{ex.Message}");
            return;
        }

        if (bytes.Length > MaxLogoBytes)
        {
            MessageBox.Show(
                $"Das Bild ist zu groß ({bytes.Length / 1024} KB). " +
                $"Bitte ein Bild mit maximal {MaxLogoBytes / 1024} KB wählen.");
            return;
        }

        _pendingLogoBase64 = Convert.ToBase64String(bytes);

        UpdateLogoPreview();
    }

    private void BtnRemoveLogo_Click(object sender, RoutedEventArgs e)
    {
        _pendingLogoBase64 = "";

        UpdateLogoPreview();
    }

    private void UpdateLogoPreview()
    {
        if (string.IsNullOrWhiteSpace(_pendingLogoBase64))
        {
            imgLogoPreview.Source = null;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(_pendingLogoBase64);

            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();

            imgLogoPreview.Source = image;
        }
        catch (FormatException)
        {
            imgLogoPreview.Source = null;
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

        if (!TryParseMillimeters(txtLabelWidthMm.Text, out double labelWidthMm) ||
            !TryParseMillimeters(txtLabelHeightMm.Text, out double labelHeightMm))
        {
            MessageBox.Show(
                "Bitte eine gültige Etikettenbreite/-höhe in mm eingeben (10 - 500).");

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

        _labelLayoutService.Save(new LabelLayoutOptions
        {
            ShowCompanyName = chkLabelCompany.IsChecked == true,
            ShowArticleNumber = chkLabelArticle.IsChecked == true,
            ShowDescription = chkLabelDescription.IsChecked == true,
            ShowSerialNumber = chkLabelSerial.IsChecked == true,
            ShowMachine = chkLabelMachine.IsChecked == true,
            ShowDate = chkLabelDate.IsChecked == true,
            ShowOperator = chkLabelOperator.IsChecked == true,
            ShowQrCode = chkLabelQr.IsChecked == true,
            WidthMm = labelWidthMm,
            HeightMm = labelHeightMm,
            LogoBase64 = _pendingLogoBase64
        });

        WindowTitleService.NotifyTitleChanged();

        DialogResult = true;

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // Akzeptiert sowohl "60,5" als auch "60.5" als Dezimaltrennzeichen,
    // unabhängig von der Windows-Ländereinstellung - Benutzer tippen je
    // nach Gewohnheit mal Komma, mal Punkt.
    private static bool TryParseMillimeters(string text, out double millimeters)
    {
        text = text.Trim();

        if (double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out millimeters) ||
            double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out millimeters))
        {
            return millimeters is >= 10 and <= 500;
        }

        return false;
    }
}