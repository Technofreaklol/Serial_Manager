using SerialManager.Models;
using SerialManager.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SerialManager.Views;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows.Threading;
using Velopack;

namespace SerialManager;

public partial class MainWindow : Window
{
    private readonly SerialService _serialService = new();
    private readonly ArticleService _articleService = new();
    private readonly MachineService _machineService = new();
    private readonly BackupService _backupService = new();
    private readonly WindowTitleService _titleService = new();
    private readonly LabelService _labelService = new();
    private readonly PrinterService _printerService = new();
    private readonly UpdateService _updateService = new();

    private List<Article> _allArticles = new();
    private List<Article> _customerArticles = new();
    private bool _suppressTextChanged;

    public MainWindow()
    {
        InitializeComponent();
        RefreshWindowTitle();
        WindowTitleService.TitleChanged += RefreshWindowTitle;
        LoadArticleList();
        cmbMachines.ItemsSource = _machineService.GetMachines();
        RefreshDashboard();
        ApplyRolePermissions();
        lblCurrentUser.Text =
           $"Angemeldet als: {CurrentSession.CurrentUser?.FullName} ({CurrentSession.CurrentUser?.Role})";

        // Stille Update-Prüfung im Hintergrund - fällt still in sich
        // zusammen, wenn kein Internet vorhanden ist oder die
        // Anwendung nicht über den Installer läuft.
        _ = CheckForUpdatesAsync(showFeedbackWhenUpToDate: false);
    }

    // -------------------------------------------------------------
    // Update-Prüfung (Velopack)
    // -------------------------------------------------------------
    private async void MenuCheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(showFeedbackWhenUpToDate: true);
    }

    private async Task CheckForUpdatesAsync(bool showFeedbackWhenUpToDate)
    {
        if (!_updateService.IsInstalled)
        {
            if (showFeedbackWhenUpToDate)
            {
                MessageBox.Show(
                    "Die Update-Suche steht nur in der installierten Version " +
                    "zur Verfügung (nicht im Entwicklungsbetrieb).",
                    "Nach Updates suchen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return;
        }

        UpdateInfo? update = await _updateService.CheckForUpdatesAsync();

        if (update == null)
        {
            if (showFeedbackWhenUpToDate)
            {
                MessageBox.Show(
                    "Sie verwenden bereits die aktuelle Version.",
                    "Nach Updates suchen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return;
        }

        var result = MessageBox.Show(
            $"Eine neue Version ({update.TargetFullRelease.Version}) ist verfügbar.\n\n" +
            "Soll das Update jetzt heruntergeladen werden? " +
            "Die Anwendung wird danach automatisch neu gestartet.",
            "Update verfügbar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            await _updateService.DownloadUpdateAsync(update);
            _updateService.ApplyUpdateAndRestart(update);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Das Update konnte nicht heruntergeladen/installiert werden:\n\n" +
                ex.Message,
                "Fehler beim Update",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyRolePermissions()
    {
        var visibility = CurrentSession.IsAdmin
            ? Visibility.Visible
            : Visibility.Collapsed;

        miBackup.Visibility = visibility;
        miRestore.Visibility = visibility;
        miMachines.Visibility = visibility;
        miResetSerial.Visibility = visibility;
        miSettings.Visibility = visibility;
        miDatabase.Visibility = visibility;
        miMigrationAssistant.Visibility = visibility;
        miUsers.Visibility = visibility;
        miAuditLog.Visibility = visibility;
        btnMachinesToolbar.Visibility = visibility;

        sepDateiTop.Visibility = visibility;
        sepDateiBottom.Visibility = visibility;
        sepSerienTop.Visibility = visibility;
        sepExtrasTop.Visibility = visibility;
        sepExtrasMid.Visibility = visibility;
    }

    private bool RequireAdmin()
    {
        if (CurrentSession.IsAdmin)
            return true;

        MessageBox.Show(
            "Diese Funktion ist nur für Administratoren verfügbar.",
            "Keine Berechtigung",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    private void MenuUsers_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        new UserManagementWindow { Owner = this }.ShowDialog();
    }

    private void cmbArticles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
            return;

        lblDescription.Text = article.Description;

        lblCurrentSerial.Text =
            _serialService.GetCurrentSerial(article.ArticleNumber);

        dgHistory.ItemsSource =
            _serialService.GetHistory(article.ArticleNumber);

        lblStatus.Text =
    $"Artikel {article.ArticleNumber} ausgewählt ({DateTime.Now:HH:mm:ss})";
        RefreshDashboard();
    }

    private void btnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
        {
            MessageBox.Show("Bitte einen Artikel auswählen.");
            return;
        }

        if (cmbMachines.SelectedItem is not Machine machine)
        {
            MessageBox.Show("Bitte eine Maschine auswählen.");
            return;
        }

        try
        {
            string serialNumber = _serialService.GetNext(
                article.ArticleNumber,
                machine.Name);

            lblCurrentSerial.Text = serialNumber;
            lblStatus.Text = $"Seriennummer {serialNumber} erfolgreich erzeugt.";

            dgHistory.ItemsSource =
                _serialService.GetHistory(article.ArticleNumber);

            var label = _labelService.CreateLabel(
                article,
                serialNumber,
                machine);

            var config = new ApplicationConfigurationService().Load();

            if (config.ShowLabelPreview)
            {
                var preview = new LabelPreviewWindow(label)
                {
                    Owner = this
                };

                preview.ShowDialog();
            }

            RefreshDashboard();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Die Seriennummer konnte nicht erzeugt werden.\n\n" +
                ex.Message,
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuArticles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ArticleWindow
        {
            Owner = this
        };

        window.ShowDialog();

        // Artikelliste neu laden
        LoadArticleList();
        RefreshDashboard();

    }

    private void MenuMachines_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        var window = new MachineWindow
        {
            Owner = this
        };

        window.ShowDialog();

        cmbMachines.ItemsSource = null;
        cmbMachines.ItemsSource = _machineService.GetMachines();
        RefreshDashboard();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var window2 = new AppInfo
        {
            Owner = this
        };

        window2.ShowDialog();
    }


    private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
            return;

        var history = _serialService.GetHistory(article.ArticleNumber);

        string search = txtSearch.Text.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            history = history
                .Where(h =>
                    h.SerialNumber.Contains(search) ||
                    h.Machine.Contains(search,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        dgHistory.ItemsSource = history;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshData();

        lblStatus.Text = "Daten wurden aktualisiert.";
    }


    private void RefreshDashboard()
    {
        var articles = _articleService.GetArticles();
        var machines = _machineService.GetMachines();

        lblArticleCount.Text = articles.Count.ToString();
        lblMachineCount.Text = machines.Count.ToString();

        if (cmbArticles.SelectedItem is Article article)
        {
            lblTodayCount.Text = _serialService
                .GetHistory(article.ArticleNumber)
                .Count(h => h.Created.Date == DateTime.Today)
                .ToString();
        }
        else
        {
            lblTodayCount.Text = "0";
        }
    }

    private void dgHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgHistory.SelectedItem is not SerialHistory history)
            return;

        var window = new HistoryDetailWindow(history)
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void MenuExport_Click(object sender, RoutedEventArgs e)
    {
        if (cmbArticles.SelectedItem is not Article article)
        {
            MessageBox.Show("Bitte zuerst einen Artikel auswählen.");
            return;
        }

        var history = _serialService.GetHistory(article.ArticleNumber);

        if (!history.Any())
        {
            MessageBox.Show("Keine Daten vorhanden.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV-Datei (*.csv)|*.csv",
            FileName = $"Historie_{article.ArticleNumber}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        var sb = new StringBuilder();

        sb.AppendLine("Artikel;Seriennummer;Maschine;Datum");

        foreach (var item in history)
        {
            sb.AppendLine(
                $"{item.ArticleNumber};{item.SerialNumber};{item.Machine};{item.Created:dd.MM.yyyy HH:mm:ss}");
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);

        lblStatus.Text = "Historie erfolgreich exportiert.";
    }

    private void MenuBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        try
        {
            string file = _backupService.CreateBackup();

            lblStatus.Text = "Backup erstellt.";

            MessageBox.Show(
                $"Backup erfolgreich erstellt.\n\n{file}",
                "Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MenuRestore_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        var provider = new DatabaseConfigurationService().Load().Provider;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            InitialDirectory = AppPaths.BackupsFolder,
            Filter = provider == "SQLite"
                ? "SQLite Datenbank (*.db)|*.db"
                : "SerialManager-Backup (*.smbak)|*.smbak"
        };

        if (dialog.ShowDialog() != true)
            return;

        if (MessageBox.Show(
            "Die aktuelle Datenbank wird überschrieben.\n\nFortfahren?",
            "Wiederherstellen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _backupService.RestoreBackup(dialog.FileName);

        MessageBox.Show(
            "Backup erfolgreich wiederhergestellt.\nBitte Anwendung neu starten.");

        Application.Current.Shutdown();
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            lblStatus.Text = "Einstellungen gespeichert.";
        }
    }
    public void RefreshWindowTitle()
    {
        var titleService = new WindowTitleService();
        Title = titleService.GetTitle("Seriennummer Verwaltung");
    }
    protected override void OnClosed(EventArgs e)
    {
        WindowTitleService.TitleChanged -= RefreshWindowTitle;
        base.OnClosed(e);
    }
    private void MenuResetSerial_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        var window = new ResetSerialWindow
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            RefreshData();

            lblStatus.Text = "Seriennummer erfolgreich geändert.";
        }
    }

    private void RefreshData()
    {
        var selectedArticle = cmbArticles.SelectedItem as Article;
        var selectedMachine = cmbMachines.SelectedItem as Machine;

        // Den kompletten Artikel-Neuladevorgang (inkl. ItemsSource-Reset)
        // vor dem Filter-Handler abschirmen – sonst öffnet ein kurzzeitig
        // ungültiger Zwischenzustand (Text noch alt, SelectedItem schon
        // null) versehentlich das Dropdown.
        _suppressTextChanged = true;

        LoadArticleList();

        var machines = _machineService.GetMachines();
        cmbMachines.ItemsSource = machines;

        if (selectedArticle != null)
        {
            var article = _customerArticles
                .FirstOrDefault(a => a.Id == selectedArticle.Id);

            if (article != null)
            {
                cmbArticles.SelectedItem = article;
            }
        }

        cmbArticles.IsDropDownOpen = false;
        _suppressTextChanged = false;

        if (selectedMachine != null)
        {
            // Wichtig: aus derselben Liste nachschlagen, die auch als
            // ItemsSource gesetzt wurde – sonst findet die ComboBox das
            // Element nicht (andere Objektinstanz) und die Auswahl bleibt leer.
            var machine = machines.FirstOrDefault(m => m.Id == selectedMachine.Id);

            if (machine != null)
            {
                cmbMachines.SelectedItem = machine;
            }
        }

        dgHistory.ItemsSource = cmbArticles.SelectedItem is Article selected
            ? _serialService.GetHistory(selected.ArticleNumber)
            : null;

        RefreshDashboard();
    }

    private void ReloadHistory()
    {
        if (cmbArticles.SelectedItem is not Article article)
            return;

        var history = _serialService.GetHistory(article.ArticleNumber);

        string search = txtSearch.Text.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            history = history
                .Where(h =>
                    h.SerialNumber.Contains(search) ||
                    h.Machine.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        dgHistory.ItemsSource = history;

        RefreshDashboard();
    }
    private void MenuPreviewLabel_Click(object sender, RoutedEventArgs e)
    {
        if (dgHistory.SelectedItem is not SerialHistory history)
            return;

        var article = _articleService
            .GetArticles()
            .FirstOrDefault(a => a.ArticleNumber == history.ArticleNumber);

        if (article == null)
        {
            MessageBox.Show("Artikel wurde nicht gefunden.");
            return;
        }

        var machine = new Machine
        {
            Name = history.Machine
        };

        var label = _labelService.CreateLabel(
            article,
            history.SerialNumber,
            machine);



        var preview = new LabelPreviewWindow(label)
        {
            Owner = this
        };

        preview.ShowDialog();
    }

    private void MenuPrintLabel_Click(object sender, RoutedEventArgs e)
    {
        if (dgHistory.SelectedItem is not SerialHistory history)
            return;

        var article = _articleService
            .GetArticles()
            .FirstOrDefault(a => a.ArticleNumber == history.ArticleNumber);

        if (article == null)
        {
            MessageBox.Show("Artikel wurde nicht gefunden.");
            return;
        }

        var machine = new Machine
        {
            Name = history.Machine
        };

        var label = _labelService.CreateLabel(
            article,
            history.SerialNumber,
            machine);

        _printerService.Print(label);
    }

    private void MenuCopySerial_Click(object sender, RoutedEventArgs e)
    {
        if (dgHistory.SelectedItem is not SerialHistory history)
            return;

        Clipboard.SetText(history.SerialNumber);

        lblStatus.Text = "Seriennummer wurde in die Zwischenablage kopiert.";
    }

    private void MenuDetails_Click(object sender, RoutedEventArgs e)
    {
        if (dgHistory.SelectedItem is not SerialHistory history)
            return;

        new HistoryDetailWindow(history)
        {
            Owner = this
        }.ShowDialog();
    }
    private void MenuDeleteSerial_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        if (dgHistory.SelectedItem is not SerialHistory history)
        {
            MessageBox.Show("Bitte zuerst eine Seriennummer auswählen.");
            return;
        }

        var result = MessageBox.Show(
            $"Seriennummer '{history.SerialNumber}' (Artikel {history.ArticleNumber}) wirklich unwiderruflich löschen?",
            "Löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _serialService.DeleteHistoryEntry(history.Id);

        ReloadHistory();

        lblStatus.Text = "Seriennummer wurde gelöscht.";
    }


    private void Database_Click(object sender, RoutedEventArgs e)
    {
        var window = new DatabaseSetupWindow
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            lblStatus.Text = "Einstellungen gespeichert.";
        }
    }
    private void MenuLogout_Click(object sender, RoutedEventArgs e)
    {
        Hide();

        var login = new LoginWindow();

        if (login.ShowDialog() != true)
        {
            // Ohne neue Anmeldung: Hauptfenster wieder anzeigen,
            // damit der Benutzer nicht ohne jedes Fenster dasteht.
            Show();
            return;
        }

        CurrentSession.CurrentUser = login.AuthenticatedUser;

        ApplyRolePermissions();

        lblCurrentUser.Text =
            $"Angemeldet als: {CurrentSession.CurrentUser?.FullName} ({CurrentSession.CurrentUser?.Role})";

        RefreshData();

        lblStatus.Text = $"Angemeldet als {CurrentSession.CurrentUser?.FullName}.";

        Show();
    }

    private void BatchSerial_Click(object sender, RoutedEventArgs e)
    {
        var window = new BatchSerialWindow
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void LoadArticleList()
    {
        _allArticles = _articleService.GetArticles();

        var previousCustomer = cmbCustomer.SelectedItem as string;
        var customers = ArticleGroupingHelper.GetCustomerNumbers(_allArticles);

        cmbCustomer.ItemsSource = customers;
        cmbCustomer.SelectedItem =
            previousCustomer != null && customers.Contains(previousCustomer)
                ? previousCustomer
                : ArticleGroupingHelper.AllCustomers;

        ApplyCustomerFilter();
    }

    private void ApplyCustomerFilter()
    {
        var customer = cmbCustomer.SelectedItem as string;
        _customerArticles = ArticleGroupingHelper.FilterByCustomer(_allArticles, customer);
        cmbArticles.ItemsSource = _customerArticles;
    }

    private void cmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyCustomerFilter();
    }

    private void MenuAuditLog_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        new AuditLogWindow { Owner = this }.ShowDialog();
    }
    private void MenuMigrationAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;

        new MigrationAssistantWindow { Owner = this }.ShowDialog();
    }
    private void cmbArticles_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
            return;

        if (e.OriginalSource is not TextBox editableTextBox)
            return;

        // WICHTIG: ItemsSource/Dropdown NICHT synchron im selben Moment
        // wie die Texteingabe selbst ändern. Das kollidiert mit WPFs
        // eigener interner Verarbeitung des gerade getippten Zeichens
        // (Caret-Update, Rendering) und führte dazu, dass insbesondere
        // das erste eingegebene Zeichen wieder verschwand/überschrieben
        // wurde. Stattdessen wird die eigentliche Filterung erst NACH
        // Abschluss der aktuellen Texteingabe ausgeführt (über den
        // Dispatcher mit Input-Priorität), zu dem Zeitpunkt liest sie
        // dann ganz normal den aktuellen (fertig verarbeiteten) Text.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => ApplyArticleFilter(editableTextBox)));
    }

    private void ApplyArticleFilter(TextBox editableTextBox)
    {
        if (_suppressTextChanged)
            return;

        var typedText = editableTextBox.Text;
        var caretIndex = editableTextBox.CaretIndex;
        var filter = typedText.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _customerArticles
            : _customerArticles
                .Where(a =>
                    a.ArticleNumber.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    a.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _suppressTextChanged = true;

        cmbArticles.ItemsSource = filtered;

        // Das Setzen von ItemsSource kann SelectedItem auf null springen
        // lassen (falls das bisher ausgewählte Element im gefilterten
        // Ergebnis fehlt), wodurch die editierbare ComboBox ihren Text
        // zurücksetzen kann - deshalb hier explizit mit dem tatsächlich
        // getippten Text (nicht dem getrimmten Filter) wiederherstellen,
        // direkt auf derselben TextBox-Instanz statt über einen erneuten
        // FindName-Lookup.
        editableTextBox.Text = typedText;
        editableTextBox.CaretIndex = caretIndex;

        bool isExactSelectedMatch =
            cmbArticles.SelectedItem is Article selected &&
            string.Equals(selected.ArticleNumber, filter, StringComparison.OrdinalIgnoreCase);

        cmbArticles.IsDropDownOpen = filtered.Count > 0 && !isExactSelectedMatch;
        _suppressTextChanged = false;
    }
    private void MenuChangePassword_Click(object sender, RoutedEventArgs e)
    {
        new ChangePasswordWindow { Owner = this }.ShowDialog();
    }
}