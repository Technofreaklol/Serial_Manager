using System.Windows;
using SerialManager.Services;

namespace SerialManager.Views;

public partial class AuditLogWindow : Window
{
    private readonly AuditLogService _auditLog = new();

    public AuditLogWindow()
    {
        InitializeComponent();
        LoadEntries();
    }

    private void LoadEntries()
    {
        dgAudit.ItemsSource = null;
        dgAudit.ItemsSource = _auditLog.GetEntries();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadEntries();
    }
}