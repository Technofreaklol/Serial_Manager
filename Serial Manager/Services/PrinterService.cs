using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using SerialManager.Models;

namespace SerialManager.Services;

// Druckt ein Etikett über den normalen Windows-Druckdialog. Es wird bewusst
// KEIN druckerspezifisches Rohformat (z. B. ZPL/EPL für Zebra-Drucker)
// erzeugt - stattdessen wird, wie bei jeder anderen Windows-Anwendung auch,
// über den ganz normalen Druckertreiber gedruckt. Das funktioniert mit
// praktisch jedem als Windows-Drucker eingerichteten Etikettendrucker
// (Zebra/DYMO/Brother QL & Co. liefern dafür einen eigenen Treiber mit) und
// genauso mit einem ganz normalen Büro-Drucker.
//
// Die eigentliche Etikettengröße (siehe Einstellungen -> Etikett) wird dem
// Treiber als gewünschte Seitengröße mitgeteilt; Schriftgrößen und QR-Code
// werden proportional dazu skaliert, damit das Etikett bei jeder
// eingestellten Größe sinnvoll aussieht.
public class PrinterService
{
    // WPF rechnet intern durchgehend in "Device Independent Units": 96 davon
    // ergeben 1 Zoll (2,54 cm), unabhängig von Bildschirm-/Drucker-DPI.
    private const double DiuPerMm = 96.0 / 25.4;

    // Referenzhöhe, auf die die Basis-Schriftgrößen unten abgestimmt sind -
    // bei einer anderen eingestellten Etikettenhöhe wird proportional dazu
    // hoch-/runterskaliert.
    private const double ReferenceHeightMm = 40;

    public void Print(LabelData label)
    {
        var dialog = new System.Windows.Controls.PrintDialog();

        if (dialog.ShowDialog() != true)
            return;

        double widthMm = Math.Max(10, label.WidthMm);
        double heightMm = Math.Max(10, label.HeightMm);

        double widthDiu = widthMm * DiuPerMm;
        double heightDiu = heightMm * DiuPerMm;

        try
        {
            dialog.PrintTicket.PageMediaSize =
                new System.Printing.PageMediaSize(widthDiu, heightDiu);
        }
        catch
        {
            // Der Treiber kennt evtl. nur feste Formate und lehnt eine frei
            // gewählte Seitengröße ab - dann wird einfach mit dessen
            // aktuell eingestelltem Format weitergedruckt, statt den
            // gesamten Druckvorgang abzubrechen.
        }

        // Jeder Drucker hat einen nicht bedruckbaren Rand (Hardware-Rand des
        // Druckkopfs) - die bedruckbare Fläche beginnt deshalb NICHT bei
        // (0,0) der physischen Seite, sondern etwas versetzt davon. Ohne
        // diesen Versatz würde unser Etikett genau im linken/oberen
        // Rand-Bereich beginnen und dort vom Drucker abgeschnitten - genau
        // das war das gemeldete Problem ("wird links abgeschnitten").
        // PrintCapabilities.PageImageableArea liefert diesen Versatz
        // (OriginWidth/OriginHeight); ist er nicht ermittelbar, wird
        // ersatzweise ohne Versatz gedruckt statt den Druck abzubrechen.
        double originX = 0;
        double originY = 0;

        try
        {
            var capabilities = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket);

            if (capabilities.PageImageableArea != null)
            {
                originX = capabilities.PageImageableArea.OriginWidth;
                originY = capabilities.PageImageableArea.OriginHeight;
            }
        }
        catch
        {
            // Treiber liefert keine Fähigkeiten-Informationen - ohne Versatz
            // weiterdrucken statt abzubrechen.
        }

        var visual = BuildPrintVisual(label, widthDiu, heightDiu);

        visual.Measure(new Size(widthDiu, heightDiu));
        visual.Arrange(new Rect(originX, originY, widthDiu, heightDiu));
        visual.UpdateLayout();

        dialog.PrintVisual(visual, $"Etikett {label.SerialNumber}");
    }

    private static FrameworkElement BuildPrintVisual(LabelData label, double widthDiu, double heightDiu)
    {
        double scale = Math.Max(0.4, heightDiu / (ReferenceHeightMm * DiuPerMm));
        double margin = Math.Max(2.0, Math.Min(widthDiu, heightDiu) * 0.05);

        var accentBrush = GetAccentBrush();

        var grid = new System.Windows.Controls.Grid
        {
            Width = widthDiu,
            Height = heightDiu,
            Background = Brushes.White,
            // Sicherheitsnetz: falls der Inhalt trotz Schrumpfen unten noch
            // nicht ganz passt (siehe ShrinkToFit unten), lieber sauber am
            // Etikettenrand abschneiden als über den Rand hinaus auf das
            // nächste Etikett "bluten" zu lassen.
            ClipToBounds = true
        };

        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        bool showQr = label.ShowQrCode;
        double qrSize = 0;

        if (showQr)
        {
            qrSize = Math.Max(10, Math.Min(heightDiu - 2 * margin, widthDiu * 0.35));

            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
            {
                Width = GridLength.Auto
            });
        }

        double contentColumnWidth = showQr
            ? Math.Max(10, widthDiu - qrSize - margin)
            : widthDiu;

        // Passt die Schriftgröße (und damit alles andere, was proportional
        // dazu skaliert) so lange nach unten an, bis der Inhalt tatsächlich
        // in die konfigurierte Etikettenhöhe passt - bei vielen aktivierten
        // Feldern (Logo + Firmenname + 6 Felder) auf einem kleinen Etikett
        // reicht der anfängliche, nur an der Höhe ausgerichtete Skalierungs-
        // faktor sonst nicht aus und der untere Teil (z. B. "Datum") würde
        // sonst über den Etikettenrand hinaus- und damit abgeschnitten
        // werden.
        var contentPanel = BuildContentPanel(label, scale, accentBrush);
        contentPanel.Margin = new Thickness(margin);

        contentPanel.Measure(new Size(contentColumnWidth, double.PositiveInfinity));

        if (contentPanel.DesiredSize.Height > heightDiu && contentPanel.DesiredSize.Height > 0)
        {
            // Kleiner Sicherheitsabschlag (3%), damit nach dem Neu-Aufbau mit
            // der kleineren Schrift nicht durch Rundung/Zeilenumbrüche exakt
            // wieder an der Kante gedruckt wird.
            double shrinkFactor = heightDiu / contentPanel.DesiredSize.Height * 0.97;

            scale = Math.Max(0.15, scale * shrinkFactor);

            contentPanel = BuildContentPanel(label, scale, accentBrush);
            contentPanel.Margin = new Thickness(margin);
        }

        contentPanel.VerticalAlignment = VerticalAlignment.Center;

        System.Windows.Controls.Grid.SetColumn(contentPanel, 0);
        grid.Children.Add(contentPanel);

        if (showQr)
        {
            var qrImage = BuildQrImage(label.QrContent);

            var qrBorder = new System.Windows.Controls.Border
            {
                Width = qrSize,
                Height = qrSize,
                Margin = new Thickness(0, 0, margin, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Controls.Image
                {
                    Source = qrImage,
                    Stretch = Stretch.Uniform
                }
            };

            System.Windows.Controls.Grid.SetColumn(qrBorder, 1);
            grid.Children.Add(qrBorder);
        }

        return grid;
    }

    // Baut die linke Textspalte (Logo, Firmenname, aktivierte Felder,
    // Seriennummer-Badge) bei einem bestimmten Skalierungsfaktor auf. Wird
    // ggf. zweimal aufgerufen: einmal mit der anhand der Etikettenhöhe
    // geschätzten Ausgangsgröße, und - falls das zu groß war - ein zweites
    // Mal mit einer passend verkleinerten (siehe BuildPrintVisual).
    private static System.Windows.Controls.StackPanel BuildContentPanel(
        LabelData label,
        double scale,
        Brush accentBrush)
    {
        var contentPanel = new System.Windows.Controls.StackPanel();

        if (label.LogoBytes is { Length: > 0 })
        {
            var logoImage = TryLoadImage(label.LogoBytes);

            if (logoImage != null)
            {
                contentPanel.Children.Add(new System.Windows.Controls.Image
                {
                    Source = logoImage,
                    Stretch = Stretch.Uniform,
                    MaxHeight = 7 * scale * DiuPerMm,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 2 * scale)
                });
            }
        }

        if (label.ShowCompanyName && !string.IsNullOrWhiteSpace(label.CompanyName))
        {
            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label.CompanyName,
                FontSize = 11 * scale,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3 * scale)
            });
        }

        void AddField(string caption, string value, bool show)
        {
            if (!show || string.IsNullOrWhiteSpace(value))
                return;

            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = caption,
                FontSize = 6.5 * scale,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2 * scale, 0, 0)
            });

            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = value,
                FontSize = 7.5 * scale,
                TextWrapping = TextWrapping.Wrap
            });
        }

        AddField("Artikel", label.ArticleNumber, label.ShowArticleNumber);
        AddField("Beschreibung", label.Description, label.ShowDescription);

        if (label.ShowSerialNumber && !string.IsNullOrWhiteSpace(label.SerialNumber))
        {
            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Seriennummer",
                FontSize = 6.5 * scale,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3 * scale, 0, 1 * scale)
            });

            var serialBorder = new System.Windows.Controls.Border
            {
                Background = accentBrush,
                CornerRadius = new CornerRadius(2 * scale),
                Padding = new Thickness(4 * scale, 1.5 * scale, 4 * scale, 1.5 * scale),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new System.Windows.Controls.TextBlock
                {
                    Text = label.SerialNumber,
                    FontSize = 10 * scale,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };

            contentPanel.Children.Add(serialBorder);
        }

        AddField("Maschine", label.Machine, label.ShowMachine);

        if (label.ShowDate)
        {
            AddField("Datum", label.Created.ToString("dd.MM.yyyy HH:mm"), true);
        }

        AddField("Bearbeiter", label.OperatorName, label.ShowOperator);

        return contentPanel;
    }

    // Verwendet, falls vorhanden, dieselbe Farbe wie die selbst gewählte
    // Button-Farbe (Einstellungen -> Button-Farbe) - so passt sich auch das
    // gedruckte Etikett an die Firmenfarbe an, statt fest auf Blau zu
    // stehen.
    private static Brush GetAccentBrush()
    {
        if (Application.Current?.Resources["PrimaryBrush"] is Brush brush)
            return brush;

        return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
    }

    private static BitmapImage? TryLoadImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage BuildQrImage(string text)
    {
        using var generator = new QRCodeGenerator();

        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);

        var qrCode = new PngByteQRCode(data);

        byte[] bytes = qrCode.GetGraphic(20);

        using var stream = new MemoryStream(bytes);

        var image = new BitmapImage();

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }
}