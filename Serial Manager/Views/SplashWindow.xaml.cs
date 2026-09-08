using SerialManager.Services;
using System.Windows;
using System.Windows.Media.Animation;

namespace SerialManager.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opacity = 0;
        Loaded += SplashWindow_Loaded;
        txtVersion.Text = $"Version {new ApplicationInfoService().Version}";
    }

    private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindResource("FadeInStoryboard") is Storyboard storyboard)
            storyboard.Begin(this);
    }

    public void SetStatus(string status)
    {
        txtStatus.Text = status;
    }

    public async Task CloseAnimatedAsync()
    {
        if (FindResource("FadeOutStoryboard") is not Storyboard storyboard)
        {
            Close();
            return;
        }

        storyboard.Begin(this);
        await Task.Delay(220);
        Close();
    }
}
