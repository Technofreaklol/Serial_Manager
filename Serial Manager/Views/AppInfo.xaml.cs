using SerialManager.Services;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace SerialManager.Views;

public partial class AppInfo : Window
{
    private readonly WindowTitleService _titleService = new();
    private readonly ApplicationInfoService _appInfo = new();

    public AppInfo()
    {
        InitializeComponent();

        txtVersion.Text = $"Version {_appInfo.Version}";
        txtBuildDate.Text = _appInfo.BuildDate;

        Title = $"{_appInfo.CompanyName} - Über";
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}