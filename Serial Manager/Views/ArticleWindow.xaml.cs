using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class ArticleWindow : Window
{
    private readonly ArticleService _service = new();
    private readonly WindowTitleService _titleService = new();
    private Article? _selectedArticle;

    public ArticleWindow()
    {
        InitializeComponent();
        Title = _titleService.GetTitle("Artikelverwaltung");
        LoadArticles();
    }

    private void LoadArticles()
    {
        dgArticles.ItemsSource = null;
        dgArticles.ItemsSource = _service.GetAllArticles();
    }

    private void ClearForm()
    {
        _selectedArticle = null;
        dgArticles.SelectedItem = null;

        txtArticleNumber.Clear();
        txtDescription.Clear();

        txtArticleNumber.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtArticleNumber.Text))
        {
            MessageBox.Show("Bitte eine Artikelnummer eingeben.");
            return;
        }

        if (_selectedArticle != null)
        {
            var confirm = MessageBox.Show(
                $"Artikel '{_selectedArticle.ArticleNumber}' wirklich ändern?",
                "Änderung speichern",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;
        }

        try
        {
            _service.SaveArticle(
                _selectedArticle?.Id,
                txtArticleNumber.Text.Trim(),
                txtDescription.Text.Trim());

            LoadArticles();
            ClearForm();
        }
        catch (DbUpdateConcurrencyException)
        {
            MessageBox.Show(
                "Dieser Artikel wurde in der Zwischenzeit von einem anderen Benutzer geändert " +
                "oder gelöscht.\n\nDie Liste wird jetzt aktualisiert – bitte die Änderung " +
                "danach erneut vornehmen.",
                "Gleichzeitige Bearbeitung",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            LoadArticles();
            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentSession.IsAdmin)
        {
            MessageBox.Show("Löschen ist nur für Administratoren möglich.");
            return;
        }

        if (_selectedArticle == null)
        {
            MessageBox.Show("Bitte zuerst einen Artikel auswählen.");
            return;
        }

        var result = MessageBox.Show(
            $"Artikel '{_selectedArticle.ArticleNumber}' wirklich löschen?\n\n" +
            "Alle zugehörigen Seriennummern werden dabei ebenfalls unwiderruflich gelöscht.",
            "Löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _service.DeleteArticle(_selectedArticle.Id);

        LoadArticles();
        ClearForm();
    }

    private void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (!CurrentSession.IsAdmin)
        {
            MessageBox.Show("Diese Funktion ist nur für Administratoren verfügbar.");
            return;
        }

        if (_selectedArticle == null)
        {
            MessageBox.Show("Bitte zuerst einen Artikel auswählen.");
            return;
        }

        _service.SetActive(_selectedArticle.Id, !_selectedArticle.IsActive);

        LoadArticles();
        ClearForm();
    }

    private void dgArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgArticles.SelectedItem is not Article article)
            return;

        _selectedArticle = article;

        txtArticleNumber.Text = article.ArticleNumber;
        txtDescription.Text = article.Description;
    }
}