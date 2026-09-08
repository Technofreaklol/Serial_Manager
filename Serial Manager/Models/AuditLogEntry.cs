namespace SerialManager.Models;

public class AuditLogEntry
{
    public int Id { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;

    public string Username { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}