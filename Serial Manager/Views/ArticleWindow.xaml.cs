using System.Windows;
using System.Windows.Controls;
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
        New_Click(sender, e);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _selectedArticle = null;

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

        _service.SaveArticle(
            txtArticleNumber.Text.Trim(),
            txtDescription.Text.Trim());

        LoadArticles();

        New_Click(sender, e);
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
            $"Artikel '{_selectedArticle.ArticleNumber}' wirklich löschen?",
            "Löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _service.DeleteArticle(_selectedArticle.Id);

        LoadArticles();

        New_Click(sender, e);
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