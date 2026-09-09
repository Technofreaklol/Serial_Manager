using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class ResetSerialWindow : Window
{
    private readonly ArticleService _articleService = new();
    private readonly AuditLogService _auditLog = new();
    private List<Article> _allArticles = new();

    private readonly WindowTitleService _titleService = new();

    public ResetSerialWindow()
    {
        InitializeComponent();

        Title = _titleService.GetTitle("Seriennummer bearbeiten");

        _allArticles = _articleService.GetArticles();
        cmbCustomer.ItemsSource = ArticleGroupingHelper.GetCustomerNumbers(_allArticles);
        cmbCustomer.SelectedIndex = 0;
    }

    private void cmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var customer = cmbCustomer.SelectedItem as string;
        cmbArticles.ItemsSource = ArticleGroupingHelper.FilterByCustomer(_allArticles, customer);

        if (cmbArticles.Items.Count > 0)
            cmbArticles.SelectedIndex = 0;
    }

    private void cmbArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
            return;

        lblCurrentSerial.Text = article.CurrentSerialNumber.ToString("D4");
        txtNewSerial.Text = article.CurrentSerialNumber.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
        {
            MessageBox.Show("Bitte einen Artikel auswählen.");
            return;
        }

        if (!int.TryParse(txtNewSerial.Text, out int newSerial))
        {
            MessageBox.Show("Bitte eine gültige Seriennummer eingeben.");
            return;
        }

        if (MessageBox.Show(
            $"Die Seriennummer für\n\n{article.ArticleNumber}\n\n" +
            $"wird von {article.CurrentSerialNumber:D4} auf {newSerial:D4} geändert.\n\n" +
            "Möchten Sie fortfahren?",
            "Seriennummer zurücksetzen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }


        int oldSerial = article.CurrentSerialNumber;
        string reason = txtReason.Text.Trim();

        try
        {
            _articleService.SetCurrentSerial(article.Id, newSerial);
        }
        catch (DbUpdateConcurrencyException)
        {
            MessageBox.Show(
                "Der Artikel wurde in der Zwischenzeit von einem anderen Benutzer geändert " +
                "(z. B. wurde gerade eine Seriennummer erzeugt).\n\n" +
                "Bitte dieses Fenster schließen und erneut öffnen.",
                "Gleichzeitige Bearbeitung",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var details =
            $"Artikelnummer: {article.ArticleNumber}, {oldSerial:D4} → {newSerial:D4}";

        if (!string.IsNullOrWhiteSpace(reason))
            details += $", Grund: {reason}";

        _auditLog.Log("Seriennummer manuell angepasst", details);

        DialogResult = true;
        Close();

    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}