using System.Windows;
using System.Windows.Controls;
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

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _selectedMachine = null;
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

        _service.SaveMachine(txtMachine.Text.Trim());

        LoadMachines();
        New_Click(sender, e);
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
        New_Click(sender, e);
    }

    private void dgMachines_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgMachines.SelectedItem is not Machine machine)
            return;

        _selectedMachine = machine;
        txtMachine.Text = machine.Name;
    }
}