using System.Windows;
using SerialManager.Models;
using QRCoder;
using System.IO;
using System.Windows.Media.Imaging;

namespace SerialManager.Views;

public partial class LabelPreviewWindow : Window
{
    private readonly LabelData _label;

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
        GenerateQrCode();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Der Druck wird im nächsten Schritt implementiert.",
            "Etikett drucken",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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