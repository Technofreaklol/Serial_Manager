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

    // Baut dasselbe Visual, das auch beim tatsächlichen Drucken verwendet
    // wird (siehe Print unten) - damit kann z. B. LabelPreviewWindow eine
    // echte "Seitenansicht" (WYSIWYG) am Bildschirm anzeigen, statt wie
    // bisher eine eigene, unabhängige Nachbildung des Layouts zu pflegen,
    // die vom tatsächlichen Ausdruck (Größe, Skalierung, Schriftgrößen,
    // Anordnung) abweichen kann.
    public static FrameworkElement CreateVisual(LabelData label)
    {
        var (widthDiu, heightDiu) = GetDiuSize(label);

        var visual = BuildPrintVisual(label, widthDiu, heightDiu);

        visual.Measure(new Size(widthDiu, heightDiu));
        visual.Arrange(new Rect(0, 0, widthDiu, heightDiu));
        visual.UpdateLayout();

        return visual;
    }

    private static (double WidthDiu, double HeightDiu) GetDiuSize(LabelData label)
    {
        double widthMm = Math.Max(10, label.WidthMm);
        double heightMm = Math.Max(10, label.HeightMm);

        return (widthMm * DiuPerMm, heightMm * DiuPerMm);
    }

    public void Print(LabelData label)
    {
        var dialog = new System.Windows.Controls.PrintDialog();

        if (dialog.ShowDialog() != true)
            return;

        var (widthDiu, heightDiu) = GetDiuSize(label);

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

        // Die Seriennummer wandert unter den QR-Code, wenn dieser überhaupt
        // angezeigt wird - ist kein QR-Code aktiviert, bleibt sie wie bisher
        // als Badge in der linken Textspalte.
        bool showSerialUnderQr = showQr && label.ShowSerialNumber &&
                                  !string.IsNullOrWhiteSpace(label.SerialNumber);

        if (showQr)
        {
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
            {
                Width = GridLength.Auto
            });
        }

        double availableHeight = Math.Max(10, heightDiu - 2 * margin);

        // Baut linke Textspalte und (falls aktiv) rechte QR-Spalte bei einem
        // gegebenen Skalierungsfaktor auf. Die QR-Größe hängt bewusst auch
        // vom selben Skalierungsfaktor ab, damit beim Schrumpfen (siehe
        // unten) QR-Code und Text proportional zueinander kleiner werden.
        (FrameworkElement Content, FrameworkElement? Right) Build(double s)
        {
            double qs = showQr
                ? Math.Max(10, Math.Min(31 * s * DiuPerMm, widthDiu * 0.42))
                : 0;

            double contentColumnWidth = showQr
                ? Math.Max(10, widthDiu - qs - margin)
                : widthDiu;

            var content = BuildContentPanel(label, s, accentBrush, !showSerialUnderQr);
            content.Margin = new Thickness(margin);
            content.Measure(new Size(contentColumnWidth, double.PositiveInfinity));

            FrameworkElement? right = null;

            if (showQr)
            {
                right = BuildRightColumn(label, s, qs, accentBrush, showSerialUnderQr);
                right.Margin = new Thickness(0, margin, margin, margin);
                right.Measure(new Size(qs + margin, double.PositiveInfinity));
            }

            return (content, right);
        }

        var built = Build(scale);

        // Passt die Schriftgröße (und damit alles andere, was proportional
        // dazu skaliert, inkl. QR-Größe) so lange nach unten an, bis BEIDE
        // Spalten tatsächlich in die konfigurierte Etikettenhöhe passen -
        // bei vielen aktivierten Feldern reicht der anfängliche, nur an der
        // Höhe ausgerichtete Skalierungsfaktor sonst nicht aus und der
        // untere Teil würde über den Etikettenrand hinaus- und damit
        // abgeschnitten werden.
        double neededHeight = built.Content.DesiredSize.Height;

        if (built.Right != null)
            neededHeight = Math.Max(neededHeight, built.Right.DesiredSize.Height);

        if (neededHeight > availableHeight && neededHeight > 0)
        {
            // Kleiner Sicherheitsabschlag (3%), damit nach dem Neu-Aufbau mit
            // der kleineren Schrift nicht durch Rundung/Zeilenumbrüche exakt
            // wieder an der Kante gedruckt wird.
            double shrinkFactor = availableHeight / neededHeight * 0.97;

            scale = Math.Max(0.15, scale * shrinkFactor);

            built = Build(scale);
        }

        // Stretch statt Center: die Textspalte füllt die komplette
        // Etikettenhöhe aus, damit der obere Block (Logo, Firmenname, ...)
        // ganz oben und Datum/Bearbeiter ganz unten sitzen können, mit dem
        // übrig bleibenden Platz dazwischen als Abstandshalter (siehe
        // BuildContentPanel).
        built.Content.VerticalAlignment = VerticalAlignment.Stretch;
        System.Windows.Controls.Grid.SetColumn(built.Content, 0);
        grid.Children.Add(built.Content);

        if (built.Right != null)
        {
            built.Right.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Controls.Grid.SetColumn(built.Right, 1);
            grid.Children.Add(built.Right);
        }

        return grid;
    }

    // Baut die rechte Spalte auf: QR-Code, darunter (falls aktiviert) die
    // Seriennummer als Bildunterschrift samt farbigem Badge - beides
    // horizontal zentriert übereinander.
    private static FrameworkElement BuildRightColumn(
        LabelData label,
        double scale,
        double qrSize,
        Brush accentBrush,
        bool showSerial)
    {
        var panel = new System.Windows.Controls.StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var qrImage = BuildQrImage(label.QrContent);

        panel.Children.Add(new System.Windows.Controls.Border
        {
            Width = qrSize,
            Height = qrSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new System.Windows.Controls.Image
            {
                Source = qrImage,
                Stretch = Stretch.Uniform
            }
        });

        if (showSerial)
        {
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Seriennummer",
                FontSize = 10 * scale,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4 * scale, 0, 1.5 * scale)
            });

            panel.Children.Add(new System.Windows.Controls.Border
            {
                Background = accentBrush,
                CornerRadius = new CornerRadius(2.5 * scale),
                Padding = new Thickness(6 * scale, 2.5 * scale, 6 * scale, 2.5 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new System.Windows.Controls.TextBlock
                {
                    Text = label.SerialNumber,
                    FontSize = 15 * scale,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            });
        }

        return panel;
    }

    // Baut die linke Textspalte (Logo, Firmenname, aktivierte Felder,
    // Seriennummer-Badge, Datum/Bearbeiter) bei einem bestimmten
    // Skalierungsfaktor auf. Wird ggf. zweimal aufgerufen: einmal mit der
    // anhand der Etikettenhöhe geschätzten Ausgangsgröße, und - falls das
    // zu groß war - ein zweites Mal mit einer passend verkleinerten (siehe
    // BuildPrintVisual).
    //
    // Logo & Co. sollen ganz oben sitzen, Datum/Bearbeiter ganz unten -
    // dazwischen liegt ein flexibler Abstandshalter (Star-Zeile), der den
    // übrig bleibenden Platz aufnimmt. Damit das wirkt, muss die Spalte in
    // BuildPrintVisual gestreckt (Stretch statt Center) über die volle
    // Etikettenhöhe dargestellt werden.
    private static FrameworkElement BuildContentPanel(
        LabelData label,
        double scale,
        Brush accentBrush,
        bool renderSerialHere)
    {
        var contentPanel = new System.Windows.Controls.StackPanel();

        // Nochmal größer als zuvor - der Schrumpfen-bis-es-passt-Mechanismus
        // in BuildPrintVisual sorgt trotzdem dafür, dass auch bei kleinen
        // Etiketten/vielen Feldern alles innerhalb der Etikettenhöhe bleibt.
        double captionSize = 10.5 * scale;
        double valueSize = 13.5 * scale;

        if (label.LogoBytes is { Length: > 0 })
        {
            var logoImage = TryLoadImage(label.LogoBytes);

            if (logoImage != null)
            {
                contentPanel.Children.Add(new System.Windows.Controls.Image
                {
                    Source = logoImage,
                    Stretch = Stretch.Uniform,
                    MaxHeight = 11 * scale * DiuPerMm,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 6 * scale)
                });
            }
        }

        if (label.ShowCompanyName && !string.IsNullOrWhiteSpace(label.CompanyName))
        {
            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label.CompanyName,
                FontSize = 19 * scale,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4 * scale)
            });
        }

        void AddField(string caption, string value, bool show)
        {
            if (!show || string.IsNullOrWhiteSpace(value))
                return;

            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = caption,
                FontSize = captionSize,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3 * scale, 0, 0)
            });

            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = value,
                FontSize = valueSize,
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Baut bei Bedarf eine Zeile mit zwei Feldern nebeneinander (statt
        // untereinander) - verwendet für Maschine+Artikel und Datum+
        // Bearbeiter, damit diese jeweils weniger vertikalen Platz
        // verbrauchen und der Rest größer dargestellt werden kann. Ist nur
        // eines der beiden Felder aktiv/gefüllt, nimmt es die ganze Breite
        // ein; sind beide leer, wird gar keine Zeile erzeugt.
        FrameworkElement? BuildSideBySideRow(
            string leftCaption, string leftValue, bool showLeft,
            string rightCaption, string rightValue, bool showRight)
        {
            bool hasLeft = showLeft && !string.IsNullOrWhiteSpace(leftValue);
            bool hasRight = showRight && !string.IsNullOrWhiteSpace(rightValue);

            if (!hasLeft && !hasRight)
                return null;

            var row = new System.Windows.Controls.Grid
            {
                Margin = new Thickness(0, 3 * scale, 0, 0)
            };

            if (hasLeft)
            {
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }

            if (hasRight)
            {
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }

            int column = 0;

            if (hasLeft)
            {
                var panel = new System.Windows.Controls.StackPanel();

                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = leftCaption,
                    FontSize = captionSize,
                    FontWeight = FontWeights.Bold
                });

                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = leftValue,
                    FontSize = valueSize,
                    TextWrapping = TextWrapping.Wrap
                });

                System.Windows.Controls.Grid.SetColumn(panel, column++);
                row.Children.Add(panel);
            }

            if (hasRight)
            {
                var panel = new System.Windows.Controls.StackPanel
                {
                    Margin = hasLeft ? new Thickness(4 * scale, 0, 0, 0) : new Thickness(0)
                };

                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = rightCaption,
                    FontSize = captionSize,
                    FontWeight = FontWeights.Bold
                });

                panel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = rightValue,
                    FontSize = valueSize,
                    TextWrapping = TextWrapping.Wrap
                });

                System.Windows.Controls.Grid.SetColumn(panel, column++);
                row.Children.Add(panel);
            }

            return row;
        }

        // Maschine links neben dem Artikel statt jeweils eigener Zeile.
        var machineArticleRow = BuildSideBySideRow(
            "Maschine", label.Machine, label.ShowMachine,
            "Artikel", label.ArticleNumber, label.ShowArticleNumber);

        if (machineArticleRow != null)
            contentPanel.Children.Add(machineArticleRow);

        AddField("Beschreibung", label.Description, label.ShowDescription);

        // Nur wenn kein QR-Code angezeigt wird, landet die Seriennummer
        // (wie bisher) hier als Badge - ansonsten sitzt sie stattdessen
        // unter dem QR-Code (siehe BuildRightColumn).
        if (renderSerialHere && label.ShowSerialNumber && !string.IsNullOrWhiteSpace(label.SerialNumber))
        {
            contentPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Seriennummer",
                FontSize = captionSize,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3 * scale, 0, 1 * scale)
            });

            var serialBorder = new System.Windows.Controls.Border
            {
                Background = accentBrush,
                CornerRadius = new CornerRadius(2.5 * scale),
                Padding = new Thickness(5 * scale, 2 * scale, 5 * scale, 2 * scale),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new System.Windows.Controls.TextBlock
                {
                    Text = label.SerialNumber,
                    FontSize = 15 * scale,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };

            contentPanel.Children.Add(serialBorder);
        }

        // Datum und Bearbeiter nebeneinander statt untereinander - und
        // (siehe unten) ganz unten in der Spalte statt direkt im Anschluss
        // an die restlichen Felder.
        var dateOperatorRow = BuildSideBySideRow(
            "Datum", label.Created.ToString("dd.MM.yyyy HH:mm"), label.ShowDate,
            "Bearbeiter", label.OperatorName, label.ShowOperator);

        if (dateOperatorRow == null)
            return contentPanel;

        var outer = new System.Windows.Controls.Grid();

        outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
        {
            Height = GridLength.Auto
        });

        outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        outer.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
        {
            Height = GridLength.Auto
        });

        System.Windows.Controls.Grid.SetRow(contentPanel, 0);
        outer.Children.Add(contentPanel);

        System.Windows.Controls.Grid.SetRow(dateOperatorRow, 2);
        outer.Children.Add(dateOperatorRow);

        return outer;
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