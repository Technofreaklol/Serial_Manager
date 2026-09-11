namespace SerialManager.Services;

// Erkennt und zerlegt den QR-Code-Inhalt, der von LabelData.QrContent
// erzeugt wird (Format "SM1?Artikel=...&Serial=...&..."), damit ein am PC
// angeschlossener USB-/Bluetooth-Scanner direkt zum gescannten
// Artikel+Seriennummer springen kann (siehe MainWindow, Suchfeld
// "Seriennummer scannen"). Erkennt der Text das Format nicht (z. B. weil
// stattdessen einfach nur eine reine Seriennummer eingetippt/gescannt
// wurde), liefert TryParse false zurück - der Aufrufer kann dann auf die
// Seriennummer allein ausweichen.
public static class QrPayloadParser
{
    private const string Prefix = "SM1?";

    public static bool TryParse(string scannedText, out string articleNumber, out string serialNumber)
    {
        articleNumber = "";
        serialNumber = "";

        if (string.IsNullOrWhiteSpace(scannedText))
            return false;

        scannedText = scannedText.Trim();

        if (!scannedText.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string query = scannedText[Prefix.Length..];

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);

            if (parts.Length != 2)
                continue;

            try
            {
                values[parts[0]] = Uri.UnescapeDataString(parts[1]);
            }
            catch (UriFormatException)
            {
                // Beschädigter/ungültiger Prozent-Anteil - dieses Feld einfach überspringen.
            }
        }

        if (!values.TryGetValue("Artikel", out var article) || string.IsNullOrWhiteSpace(article))
            return false;

        if (!values.TryGetValue("Serial", out var serial) || string.IsNullOrWhiteSpace(serial))
            return false;

        articleNumber = article;
        serialNumber = serial;

        return true;
    }
}