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