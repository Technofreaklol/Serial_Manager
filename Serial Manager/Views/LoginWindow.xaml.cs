using System.Windows;
using System.Windows.Input;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class LoginWindow : Window
{
    private readonly UserService _userService = new();
    private readonly ApplicationConfigurationService _configService = new();

    public User? AuthenticatedUser { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();

        var lastUsername = _configService.Load().LastUsername;

        if (!string.IsNullOrWhiteSpace(lastUsername))
        {
            txtUsername.Text = lastUsername;
            Loaded += (_, _) => txtPassword.Focus();
        }
        else
        {
            Loaded += (_, _) => txtUsername.Focus();
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void txtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryLogin();
    }

    private void TryLogin()
    {
        var user = _userService.Authenticate(txtUsername.Text.Trim(), txtPassword.Password);

        if (user == null)
        {
            lblError.Text = "Benutzername oder Passwort ist falsch.";
            txtPassword.Clear();
            txtPassword.Focus();
            return;
        }

        AuthenticatedUser = user;

        var config = _configService.Load();
        config.LastUsername = user.Username;
        _configService.Save(config);

        DialogResult = true;
        Close();
    }
}