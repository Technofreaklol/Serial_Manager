using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class MachineWindow : Window
{
    private readonly MachineService _service = new();
    private readonly WindowTitleService _titleService = new();
    private Machine? _selectedMachine;

    public MachineWindow()
    {
        InitializeComponent();

        Title = _titleService.GetTitle("Maschinenverwaltung");
        LoadMachines();
    }

    private void LoadMachines()
    {
        dgMachines.ItemsSource = null;
        dgMachines.ItemsSource = _service.GetMachines();
    }

    private void ClearForm()
    {
        _selectedMachine = null;
        dgMachines.SelectedItem = null;

        txtMachine.Clear();
        txtMachine.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMachine.Text))
        {
            MessageBox.Show("Bitte einen Maschinennamen eingeben.");
            return;
        }

        if (_selectedMachine != null)
        {
            var confirm = MessageBox.Show(
                $"Maschine '{_selectedMachine.Name}' wirklich ändern?",
                "Änderung speichern",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;
        }

        try
        {
            _service.SaveMachine(_selectedMachine?.Id, txtMachine.Text.Trim());

            LoadMachines();
            ClearForm();
        }
        catch (DbUpdateConcurrencyException)
        {
            MessageBox.Show(
                "Diese Maschine wurde in der Zwischenzeit von einem anderen Benutzer geändert " +
                "oder gelöscht.\n\nDie Liste wird jetzt aktualisiert – bitte die Änderung " +
                "danach erneut vornehmen.",
                "Gleichzeitige Bearbeitung",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            LoadMachines();
            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMachine == null)
        {
            MessageBox.Show("Bitte zuerst eine Maschine auswählen.");
            return;
        }

        var result = MessageBox.Show(
            $"Maschine '{_selectedMachine.Name}' löschen?",
            "Löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _service.DeleteMachine(_selectedMachine.Id);

        LoadMachines();
        ClearForm();
    }

    private void dgMachines_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgMachines.SelectedItem is not Machine machine)
            return;

        _selectedMachine = machine;
        txtMachine.Text = machine.Name;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        LoadMachines();
        ClearForm();
    }
}