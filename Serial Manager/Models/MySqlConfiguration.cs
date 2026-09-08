namespace SerialManager.Models;

public class MySqlConfiguration
{
    public string Server { get; set; } = "localhost";

    public int Port { get; set; } = 3306;

    public string Database { get; set; } = "serialmanager";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";
    public string ServerVersion { get; set; } = "";
}