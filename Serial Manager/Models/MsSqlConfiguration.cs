namespace SerialManager.Models;

public class MsSqlConfiguration
{
    public string Server { get; set; } = "localhost";

    // 0 = Standardinstanz/-port (Port wird dann nicht in den
    // Verbindungsstring aufgenommen). Bei einer benannten Instanz
    // (z. B. "localhost\SQLEXPRESS") wird der Port normalerweise gar
    // nicht benötigt - SQL Server löst das selbst über den SQL Server
    // Browser-Dienst auf.
    public int Port { get; set; } = 0;

    public string Database { get; set; } = "serialmanager";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";
}
