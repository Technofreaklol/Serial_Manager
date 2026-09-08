using SerialManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SerialManager.Views
{
    /// <summary>
    /// Interaktionslogik für CreateAdminWindow.xaml
    /// </summary>
    public partial class CreateAdminWindow : Window
    {
        public CreateAdminWindow()
        {
            InitializeComponent();
        }

        private readonly UserService _userService = new();

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }

            if (txtPassword.Password != txtPassword2.Password)
            {
                MessageBox.Show("Die Passwörter stimmen nicht überein.");
                return;
            }

            _userService.CreateUser(
                txtUsername.Text,
                txtPassword.Password,
                txtName.Text,
                "Administrator");

            DialogResult = true;
            Close();
        }
    }

    
    }
