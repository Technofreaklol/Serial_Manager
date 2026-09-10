using SerialManager.Helpers;
using SerialManager.Models;
using SerialManager.Services;
using System.Windows;
using System.Windows.Controls;

namespace SerialManager.Views;

public partial class BatchSerialWindow : Window
{
    private readonly ArticleService _articleService = new();
    private readonly MachineService _machineService = new();
    private readonly SerialService _serialService = new();
    private readonly WindowTitleService _titleService = new();
    private List<Article> _allArticles = new();

    public BatchSerialWindow()
    {
        InitializeComponent();

        Title = _titleService.GetTitle("Batch Seriennummer");

        _allArticles = _articleService.GetArticles();
        cmbCustomer.ItemsSource = ArticleGroupingHelper.GetCustomerNumbers(_allArticles);
        cmbCustomer.SelectedIndex = 0;

        cmbMachine.ItemsSource = _machineService.GetMachines();

        UpdatePreview();
    }

    private void cmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = cmbCustomer.SelectedItem as string;
        cmbArticle.ItemsSource = ArticleGroupingHelper.FilterByCustomer(_allArticles, customer);
        UpdatePreview();
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (cmbArticle.SelectedItem is not Article selected)
            return;

        var article = _articleService.GetArticle(selected.ArticleNumber);

        if (article == null)
            return;

        if (!int.TryParse(txtQuantity.Text, out int quantity))
            return;

        int start = article.CurrentSerialNumber + 1;
        int end = article.CurrentSerialNumber + quantity;

        txtPreviewStart.Text = $"Start: {start:D4}";
        txtPreviewEnd.Text = $"Ende:  {end:D4}";
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (cmbArticle.SelectedItem is not Article article)
        {
            MessageBox.Show("Bitte einen Artikel auswählen.");
            return;
        }

        if (cmbMachine.SelectedItem is not Machine machine)
        {
            MessageBox.Show("Bitte eine Maschine auswählen.");
            return;
        }

        if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
        {
            MessageBox.Show("Ungültige Anzahl.");
            return;
        }

        try
        {
            var serials = _serialService.CreateBatch(
                article.ArticleNumber,
                machine.Name,
                quantity);

            MessageBox.Show(
                $"{serials.Count} Seriennummern wurden erfolgreich erzeugt.\n\n" +
                $"Von: {serials.First()}\n" +
                $"Bis: {serials.Last()}",
                "Batch erfolgreich",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            cmbArticle.ItemsSource = _articleService.GetArticles();
            cmbArticle.SelectedIndex = 0;
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Die Seriennummern konnten nicht erzeugt werden.\n\n" +
                ex.Message,
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}