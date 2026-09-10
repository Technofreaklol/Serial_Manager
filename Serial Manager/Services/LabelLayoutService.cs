using SerialManager.Models;

namespace SerialManager.Services;

// Lädt/speichert die Etiketten-Layout-Einstellungen (welche Felder gedruckt
// werden, Firmenlogo) über SettingsService - also in der Settings-Tabelle
// der eigentlichen Datenbank, nicht in einer lokalen Datei. Dadurch sehen
// automatisch alle Benutzer derselben Datenbank dasselbe Etiketten-Layout,
// genau wie bei der Button-Farbe.
public class LabelLayoutService
{
    private const string KeyShowCompanyName = "Label_ShowCompanyName";
    private const string KeyShowArticleNumber = "Label_ShowArticleNumber";
    private const string KeyShowDescription = "Label_ShowDescription";
    private const string KeyShowSerialNumber = "Label_ShowSerialNumber";
    private const string KeyShowMachine = "Label_ShowMachine";
    private const string KeyShowDate = "Label_ShowDate";
    private const string KeyShowOperator = "Label_ShowOperator";
    private const string KeyShowQrCode = "Label_ShowQrCode";
    private const string KeyLogoBase64 = "Label_LogoBase64";
    private const string KeyWidthMm = "Label_WidthMm";
    private const string KeyHeightMm = "Label_HeightMm";

    // Schutz gegen eine versehentlich winzige/riesige oder ungültige
    // (z. B. manuell in der DB verstellte) Etikettengröße - ohne diese
    // Grenzen könnte PrinterService eine ungültige Seitengröße an den
    // Druckertreiber übergeben.
    private const double MinDimensionMm = 10;
    private const double MaxDimensionMm = 500;

    private readonly SettingsService _settings = new();

    public LabelLayoutOptions Load()
    {
        var defaults = new LabelLayoutOptions();

        return new LabelLayoutOptions
        {
            ShowCompanyName = _settings.GetBool(KeyShowCompanyName, defaults.ShowCompanyName),
            ShowArticleNumber = _settings.GetBool(KeyShowArticleNumber, defaults.ShowArticleNumber),
            ShowDescription = _settings.GetBool(KeyShowDescription, defaults.ShowDescription),
            ShowSerialNumber = _settings.GetBool(KeyShowSerialNumber, defaults.ShowSerialNumber),
            ShowMachine = _settings.GetBool(KeyShowMachine, defaults.ShowMachine),
            ShowDate = _settings.GetBool(KeyShowDate, defaults.ShowDate),
            ShowOperator = _settings.GetBool(KeyShowOperator, defaults.ShowOperator),
            ShowQrCode = _settings.GetBool(KeyShowQrCode, defaults.ShowQrCode),
            WidthMm = Clamp(_settings.GetDouble(KeyWidthMm, defaults.WidthMm)),
            HeightMm = Clamp(_settings.GetDouble(KeyHeightMm, defaults.HeightMm)),
            LogoBase64 = _settings.GetValue(KeyLogoBase64, "")
        };
    }

    public void Save(LabelLayoutOptions options)
    {
        _settings.SetBool(KeyShowCompanyName, options.ShowCompanyName);
        _settings.SetBool(KeyShowArticleNumber, options.ShowArticleNumber);
        _settings.SetBool(KeyShowDescription, options.ShowDescription);
        _settings.SetBool(KeyShowSerialNumber, options.ShowSerialNumber);
        _settings.SetBool(KeyShowMachine, options.ShowMachine);
        _settings.SetBool(KeyShowDate, options.ShowDate);
        _settings.SetBool(KeyShowOperator, options.ShowOperator);
        _settings.SetBool(KeyShowQrCode, options.ShowQrCode);
        _settings.SetDouble(KeyWidthMm, Clamp(options.WidthMm));
        _settings.SetDouble(KeyHeightMm, Clamp(options.HeightMm));
        _settings.SetValue(KeyLogoBase64, options.LogoBase64 ?? "");
    }

    private static double Clamp(double mm) =>
        Math.Clamp(mm, MinDimensionMm, MaxDimensionMm);
}