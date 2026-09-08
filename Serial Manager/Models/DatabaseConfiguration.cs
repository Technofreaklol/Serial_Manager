namespace SerialManager.Models;

public class DatabaseConfiguration
{
    public string Provider { get; set; } = "SQLite";

    public SQLiteConfiguration SQLite { get; set; } = new();

    public MySqlConfiguration MySQL { get; set; } = new();
}