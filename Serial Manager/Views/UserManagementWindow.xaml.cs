using System.Windows;
using System.Windows.Controls;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class UserManagementWindow : Window
{
    private readonly UserService _userService = new();
    private User? _selectedUser;

    public UserManagementWindow()
    {
        InitializeComponent();
        LoadUsers();
        ResetForm();
    }

    private void LoadUsers()
    {
        dgUsers.ItemsSource = null;
        dgUsers.ItemsSource = _userService.GetUsers();
    }

    private void dgUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedUser = dgUsers.SelectedItem as User;

        if (_selectedUser == null)
            return;

        txtName.Text = _selectedUser.FullName;
        txtUsername.Text = _selectedUser.Username;
        txtUsername.IsEnabled = false;
        txtPassword.Clear();

        cmbRole.SelectedItem = cmbRole.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Content == _selectedUser.Role);

        btnSave.Content = "Speichern";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        txtName.Focus();
    }

    private void ResetForm()
    {
        _selectedUser = null;
        dgUsers.SelectedItem = null;

        txtName.Clear();
        txtUsername.Clear();
        txtUsername.IsEnabled = true;
        txtPassword.Clear();
        cmbRole.SelectedIndex = 0;

        btnSave.Content = "Neu anlegen";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text) ||
            string.IsNullOrWhiteSpace(txtUsername.Text))
        {
            MessageBox.Show("Bitte Name und Benutzername ausfüllen.");
            return;
        }

        var role = (cmbRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "User";

        try
        {
            if (_selectedUser == null)
            {
                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("Bitte ein Passwort vergeben.");
                    return;
                }

                _userService.CreateUser(txtUsername.Text.Trim(), txtPassword.Password, txtName.Text.Trim(), role);
            }
            else
            {
                _userService.UpdateUser(_selectedUser.Id, txtName.Text.Trim(), role);

                if (!string.IsNullOrWhiteSpace(txtPassword.Password))
                    _userService.ResetPassword(_selectedUser.Id, txtPassword.Password);
            }

            ResetForm();
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageBox.Show("Bitte zuerst einen Benutzer auswählen.");
            return;
        }

        if (_selectedUser.Id == CurrentSession.CurrentUser?.Id)
        {
            MessageBox.Show("Der eigene Benutzer kann nicht deaktiviert werden.");
            return;
        }

        _userService.SetActive(_selectedUser.Id, !_selectedUser.IsActive);
        LoadUsers();
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null)
        {
            MessageBox.Show("Bitte zuerst einen Benutzer auswählen.");
            return;
        }

        if (_selectedUser.Id == CurrentSession.CurrentUser?.Id)
        {
            MessageBox.Show("Der eigene Benutzer kann nicht gelöscht werden.");
            return;
        }

        var result = MessageBox.Show(
            $"Benutzer '{_selectedUser.Username}' wirklich unwiderruflich löschen?",
            "Löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _userService.DeleteUser(_selectedUser.Id);
            ResetForm();
            LoadUsers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}