using System.Windows;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class LabelPreviewWindow : Window
{
    private readonly LabelData _label;
    private readonly PrinterService _printerService = new();

    public LabelPreviewWindow(LabelData label)
    {
        InitializeComponent();

        _label = label;

        // Echte "Seitenansicht": zeigt dasselbe Visual, das PrinterService
        // beim tatsächlichen Drucken erzeugt (gleiche Größe, Skalierung,
        // Schriftgrößen und Anordnung) - statt wie bisher eine eigene,
        // unabhängige Nachbildung des Layouts, die vom echten Ausdruck
        // abweichen konnte.
        previewViewbox.Child = PrinterService.CreateVisual(_label);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _printerService.Print(_label);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Das Etikett konnte nicht gedruckt werden.\n\n" + ex.Message,
                "Etikett drucken",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}