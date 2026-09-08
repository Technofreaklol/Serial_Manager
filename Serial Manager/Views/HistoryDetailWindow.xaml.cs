using SerialManager.Models;
using SerialManager.Services;
using System.Windows;

namespace SerialManager.Views;

public partial class HistoryDetailWindow : Window
{
    private readonly WindowTitleService _titleService = new();

    public HistoryDetailWindow(SerialHistory history)
    {
        InitializeComponent();
        Title = _titleService.GetTitle("Historie");

        lblArticle.Text = history.ArticleNumber;
        lblSerial.Text = history.SerialNumber;
        lblMachine.Text = history.Machine;
        lblCreated.Text = history.Created.ToString("dd.MM.yyyy HH:mm:ss");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}