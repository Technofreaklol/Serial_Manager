namespace SerialManager.Models;

public class BackupData
{
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Database { get; set; } = string.Empty;

    public List<string> Statements { get; set; } = new();
}