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

    // Bewusst EINZEILIG (kein Zeilenumbruch!) und mit Prozent-Kodierung der
    // Werte: ein am PC angeschlossener USB-/Bluetooth-Scanner "tippt" den
    // gescannten QR-Inhalt wie eine Tastatur in das gerade fokussierte Feld.
    // Enthielte der Inhalt Zeilenumbrüche, würde jeder davon als Enter-Taste
    // ankommen und z. B. ein einzeiliges Suchfeld (siehe MainWindow,
    // "Seriennummer scannen") vorzeitig abschicken, bevor der Rest getippt
    // ist. "SM1?" am Anfang ist eine feste Kennung, an der QrPayloadParser
    // erkennt, dass es sich um einen von dieser App erzeugten QR-Code
    // handelt (und nicht z. B. um eine manuell eingetippte Seriennummer).
    // Jeder Wert wird einzeln kodiert (Uri.EscapeDataString), damit z. B. ein
    // "&" oder "=" in der Beschreibung das Format nicht zerstört.
    public string QrContent =>
        "SM1?Artikel=" + Uri.EscapeDataString(ArticleNumber) +
        "&Serial=" + Uri.EscapeDataString(SerialNumber) +
        "&Maschine=" + Uri.EscapeDataString(Machine) +
        "&Firma=" + Uri.EscapeDataString(CompanyName) +
        "&Beschreibung=" + Uri.EscapeDataString(Description) +
        "&Datum=" + Uri.EscapeDataString(Created.ToString("dd.MM.yyyy"));
}