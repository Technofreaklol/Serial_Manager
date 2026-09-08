using System.Windows;

using SerialManager.Models;

namespace SerialManager.Services;

public class PrinterService
{
    public void Print(LabelData label)
    {
        MessageBox.Show(
            $"Etikett wird gedruckt.\n\n" +
            $"Artikel: {label.ArticleNumber}\n" +
            $"Seriennummer: {label.SerialNumber}",
            "Druck",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
} 