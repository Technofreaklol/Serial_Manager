using System.Windows;
using SerialManager.Models;
using SerialManager.Services;
using QRCoder;
using System.IO;
using System.Windows.Media.Imaging;

namespace SerialManager.Views;

public partial class LabelPreviewWindow : Window
{
    private readonly LabelData _label;
    private readonly PrinterService _printerService = new();

    public LabelPreviewWindow(LabelData label)
    {
        InitializeComponent();

        _label = label;

        LoadLabel();
    }

    private void LoadLabel()
    {
        lblCompany.Text = _label.CompanyName;
        lblArticle.Text = _label.ArticleNumber;
        lblDescription.Text = _label.Description;
        lblSerial.Text = _label.SerialNumber;
        lblMachine.Text = _label.Machine;
        lblDate.Text = _label.Created.ToString("dd.MM.yyyy HH:mm:ss");
        lblOperator.Text = _label.OperatorName;

        // Sichtbarkeit je nach Etiketten-Layout-Einstellungen (Einstellungen
        // -> Etikett). lblCompany selbst bleibt immer im Baum, wird aber
        // ausgeblendet, wenn die Firma nicht angezeigt werden soll.
        lblCompany.Visibility = ToVisibility(_label.ShowCompanyName);
        panelArticle.Visibility = ToVisibility(_label.ShowArticleNumber);
        panelDescription.Visibility = ToVisibility(_label.ShowDescription);
        panelSerial.Visibility = ToVisibility(_label.ShowSerialNumber);
        panelMachine.Visibility = ToVisibility(_label.ShowMachine);
        panelDate.Visibility = ToVisibility(_label.ShowDate);
        panelOperator.Visibility = ToVisibility(_label.ShowOperator);

        LoadLogo();

        if (_label.ShowQrCode)
        {
            GenerateQrCode();
        }
        else
        {
            panelQr.Visibility = Visibility.Collapsed;
            colQr.Width = new GridLength(0);
        }
    }

    private static Visibility ToVisibility(bool show) =>
        show ? Visibility.Visible : Visibility.Collapsed;

    private void LoadLogo()
    {
        if (_label.LogoBytes == null || _label.LogoBytes.Length == 0)
        {
            imgLogo.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            using var stream = new MemoryStream(_label.LogoBytes);

            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();

            imgLogo.Source = image;
            imgLogo.Visibility = Visibility.Visible;
        }
        catch
        {
            // Beschädigtes/ungültiges Logo-Bild - Etikett trotzdem ohne Logo anzeigen.
            imgLogo.Visibility = Visibility.Collapsed;
        }
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
    private void GenerateQrCode()
    {
        string text = _label.QrContent;

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

        imgQrCode.Source = image;
    }
}