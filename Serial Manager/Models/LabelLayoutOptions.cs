namespace SerialManager.Models;

// Gespeicherte Einstellungen für das Aussehen des Etiketts: welche Felder
// gedruckt/angezeigt werden und ein optionales Firmenlogo. Wird über
// LabelLayoutService in der Datenbank (Settings-Tabelle) gespeichert, gilt
// also für alle Benutzer dieser Datenbank gemeinsam - genau wie die
// Button-Farbe ist das eine Firmen-/Layout-Vorgabe, keine persönliche
// Einstellung pro Rechner.
public class LabelLayoutOptions
{
    public bool ShowCompanyName { get; set; } = true;
    public bool ShowArticleNumber { get; set; } = true;
    public bool ShowDescription { get; set; } = true;
    public bool ShowSerialNumber { get; set; } = true;
    public bool ShowMachine { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowOperator { get; set; } = false;
    public bool ShowQrCode { get; set; } = true;

    // Physische Etikettengröße in Millimeter, wie sie auf dem verwendeten
    // Etikettendrucker eingelegt ist (z. B. 60x40mm-Rollenetiketten). Wird
    // beim Drucken als Seitengröße an den Druckertreiber übergeben (siehe
    // PrinterService) und bestimmt dort auch die Skalierung von Schrift/
    // QR-Code - unterschiedliche Datenbanken/Firmen können also
    // unterschiedliche Etikettengrößen verwenden.
    public double WidthMm { get; set; } = 60;
    public double HeightMm { get; set; } = 40;

    // Base64-kodiertes Bild (PNG/JPG), leer = kein Logo hinterlegt.
    public string LogoBase64 { get; set; } = "";

    public byte[]? GetLogoBytes()
    {
        if (string.IsNullOrWhiteSpace(LogoBase64))
            return null;

        try
        {
            return Convert.FromBase64String(LogoBase64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}