using System.Windows;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly UserService _userService = new();

    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtCurrentPassword.Password) ||
            string.IsNullOrWhiteSpace(txtNewPassword.Password))
        {
            lblError.Text = "Bitte alle Felder ausfüllen.";
            return;
        }

        if (txtNewPassword.Password != txtNewPassword2.Password)
        {
            lblError.Text = "Die neuen Passwörter stimmen nicht überein.";
            return;
        }

        if (CurrentSession.CurrentUser == null)
        {
            lblError.Text = "Kein Benutzer angemeldet.";
            return;
        }

        try
        {
            _userService.ChangeOwnPassword(
                CurrentSession.CurrentUser.Id,
                txtCurrentPassword.Password,
                txtNewPassword.Password);

            MessageBox.Show("Passwort wurde geändert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
        }
    }
}