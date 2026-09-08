namespace SerialManager.Services;

public class DatabaseStatus
{
    public string Provider { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public bool TablesOk { get; set; }
    public bool SerialIndexOk { get; set; }
    public bool LegacySerialIndexFound { get; set; }
    public string MigrationStatus { get; set; } = "Nicht geprüft";
    public int ArticleCount { get; set; }
    public int MachineCount { get; set; }
    public int HistoryCount { get; set; }
    public int SettingsCount { get; set; }
    public string Error { get; set; } = string.Empty;
}
