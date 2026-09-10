namespace SerialManager.Models;

public class LabelData
{
    public string CompanyName { get; set; } = "";

    public string ArticleNumber { get; set; } = "";

    public string Description { get; set; } = "";

    public string SerialNumber { get; set; } = "";

    public string Machine { get; set; } = "";

    public DateTime Created { get; set; }

    public string OperatorName { get; set; } = "";

    // Layout-Einstellungen (siehe LabelLayoutService/LabelLayoutOptions):
    // welche Felder auf dem Etikett angezeigt werden sollen und ein
    // optionales Firmenlogo. Werden von LabelService.CreateLabel() aus den
    // gespeicherten, für alle Benutzer der Datenbank gültigen Einstellungen
    // befüllt. Der QR-Code-Inhalt (QrContent unten) enthält bewusst immer
    // ALLE Daten zur Rückverfolgbarkeit, auch wenn ein Feld auf dem
    // sichtbaren Etikett ausgeblendet ist.
    public bool ShowCompanyName { get; set; } = true;
    public bool ShowArticleNumber { get; set; } = true;
    public bool ShowDescription { get; set; } = true;
    public bool ShowSerialNumber { get; set; } = true;
    public bool ShowMachine { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowOperator { get; set; } = false;
    public bool ShowQrCode { get; set; } = true;

    public byte[]? LogoBytes { get; set; }

    // Physische Etikettengröße in Millimeter (siehe LabelLayoutOptions) -
    // wird von PrinterService beim Drucken als Seitengröße verwendet.
    public double WidthMm { get; set; } = 60;
    public double HeightMm { get; set; } = 40;

    public string QrContent =>
$"""
Firma={CompanyName}
Artikel={ArticleNumber}
Beschreibung={Description}
Serial={SerialNumber}
Maschine={Machine}
Datum={Created:dd.MM.yyyy - HH:mm}
""";
}