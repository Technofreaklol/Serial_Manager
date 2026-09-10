using System.Windows;
using System.Windows.Media;

namespace SerialManager.Services;

// Wendet eine vom Benutzer gewählte Button-Farbe global auf die ganze
// Anwendung an. Buttons haben in Resources/Styles.xaml EINEN einzigen,
// impliziten Stil (kein x:Key, gilt also automatisch für jeden Button in
// der App), der über die Ressourcen "PrimaryBrush" (Hintergrund, Normal-
// zustand), "PrimaryDarkBrush" (Rahmen sowie Hover-/Pressed-Hintergrund)
// und "PrimaryForegroundBrush" (Textfarbe) eingefärbt wird. Diese Klasse
// ersetzt genau diese drei Ressourcen zur Laufzeit.
//
// Wichtig: Die in Styles.xaml deklarierten Brushes werden von WPF
// automatisch "eingefroren" (Freezable-Optimierung), weil sie dort nur
// statisch/literal definiert sind. Ein eingefrorener Brush lässt sich nicht
// mehr verändern (Color = ... würde eine InvalidOperationException werfen).
// Deshalb wird hier IMMER ein komplett neuer SolidColorBrush erzeugt und der
// Ressourcen-Eintrag ersetzt, statt den bestehenden Brush zu mutieren.
//
// Der Button-Stil verweist über DynamicResource (nicht StaticResource) auf
// diese drei Ressourcen, daher wirkt ein Austausch sofort auf ALLE Buttons -
// auch in bereits geöffneten Fenstern, ganz ohne Neustart.
public static class ThemeService
{
    public const string DefaultButtonColorHex = "#1976D2";

    private const string SettingsKey = "ButtonColor";

    // Beim Programmstart aufgerufen (vor dem ersten sichtbaren Fenster),
    // damit eine gespeicherte Benutzerfarbe von Anfang an gilt.
    public static void ApplyPersistedButtonColor()
    {
        var hex = new SettingsService().GetValue(SettingsKey, "");

        if (string.IsNullOrWhiteSpace(hex))
            return;

        ApplyButtonColor(hex);
    }

    public static string GetSavedButtonColorHex()
    {
        var hex = new SettingsService().GetValue(SettingsKey, "");

        return string.IsNullOrWhiteSpace(hex)
            ? DefaultButtonColorHex
            : hex;
    }

    public static void SaveButtonColor(string hex)
    {
        new SettingsService().SetValue(SettingsKey, hex);
    }

    // true = gültige Farbe, wurde angewendet. false = ungültiges Format,
    // nichts wurde geändert.
    public static bool ApplyButtonColor(string hex)
    {
        if (!TryParseColor(hex, out var color))
            return false;

        var primary = new SolidColorBrush(color);
        primary.Freeze();

        var darker = new SolidColorBrush(Darken(color, 0.15));
        darker.Freeze();

        // Text bleibt lesbar: je nach Helligkeit der gewählten Farbe wird
        // automatisch heller oder dunkler Text verwendet (z. B. dunkler Text
        // auf einem hellen Gelb, weißer Text auf einem dunklen Blau).
        var foreground = new SolidColorBrush(GetReadableForeground(color));
        foreground.Freeze();

        Application.Current.Resources["PrimaryBrush"] = primary;
        Application.Current.Resources["PrimaryDarkBrush"] = darker;
        Application.Current.Resources["PrimaryForegroundBrush"] = foreground;

        return true;
    }

    // Ermittelt anhand der wahrgenommenen Helligkeit (Luminanz) der
    // Hintergrundfarbe, ob dunkler oder heller Text besser lesbar ist.
    // Gewichtung nach dem menschlichen Helligkeitsempfinden (Grün wirkt
    // heller als Rot, Rot wieder heller als Blau).
    private static Color GetReadableForeground(Color background)
    {
        double luminance =
            (0.299 * background.R + 0.587 * background.G + 0.114 * background.B);

        return luminance > 150
            ? Color.FromRgb(0x21, 0x21, 0x21) // dunkler Text für helle Hintergründe
            : Colors.White;                   // heller Text für dunkle Hintergründe
    }

    public static bool TryParseColor(string hex, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(hex))
            return false;

        try
        {
            if (ColorConverter.ConvertFromString(hex.Trim()) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
            // Ungültiges Format (z. B. kein #RRGGBB) - unten false zurückgeben.
        }

        return false;
    }

    private static Color Darken(Color color, double amount)
    {
        byte Reduce(byte channel) =>
            (byte)Math.Max(0, channel - channel * amount);

        return Color.FromRgb(Reduce(color.R), Reduce(color.G), Reduce(color.B));
    }
}