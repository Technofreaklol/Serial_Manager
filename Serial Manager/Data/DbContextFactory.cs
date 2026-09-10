using Microsoft.EntityFrameworkCore;
using System.IO;
using SerialManager.Models;
using SerialManager.Services;

namespace SerialManager.Data;

public static class DbContextFactory
{
    public static SerialDbContext Create()
    {
        var configService = new DatabaseConfigurationService();
        var config = configService.Load();

        var builder = new DbContextOptionsBuilder<SerialDbContext>();

        switch (config.Provider)
        {
            case "MySQL":
                {
                    string connection =
                        $"server={config.MySQL.Server};" +
                        $"port={config.MySQL.Port};" +
                        $"database={config.MySQL.Database};" +
                        $"user={config.MySQL.Username};" +
                        $"password={config.MySQL.Password};";

                    ServerVersion serverVersion;

                    if (string.IsNullOrWhiteSpace(config.MySQL.ServerVersion))
                    {
                        serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
                    }
                    else
                    {
                        serverVersion = ServerVersion.Parse(config.MySQL.ServerVersion);
                    }

                    builder.UseMySql(connection, serverVersion);
                    break;
                }

            case "MSSQL":
                {
                    builder.UseSqlServer(BuildMsSqlConnectionString(config.MSSQL));
                    break;
                }

            case "SQLite":
            default:
                {
                    string sqlitePath = AppPaths.GetDatabasePath(
                        config.SQLite.File);

                    builder.UseSqlite($"Data Source={sqlitePath}");
                    break;
                }
        }

        return new SerialDbContext(builder.Options);
    }

    // Wird auch von DatabaseInitializer/BackupService für direkte
    // ADO.NET-Verbindungen (SqlConnection) verwendet, damit der
    // Verbindungsstring nur an einer Stelle gepflegt werden muss.
    //
    // TrustServerCertificate=True ist bei lokalen/firmeninternen SQL-
    // Server-Instanzen ohne "echtes" (von einer öffentlichen CA
    // signiertes) TLS-Zertifikat notwendig - ohne diese Option lehnt
    // Microsoft.Data.SqlClient die Verbindung standardmäßig ab
    // (Encrypt=True ist seit Version 3 Standard).
    public static string BuildMsSqlConnectionString(MsSqlConfiguration config)
    {
        string server = config.Port > 0
            ? $"{config.Server},{config.Port}"
            : config.Server;

        return $"Server={server};Database={config.Database};" +
               $"User Id={config.Username};Password={config.Password};" +
               "TrustServerCertificate=True;";
    }

    // Wie oben, aber ohne "Database=" - zum Prüfen der reinen
    // Servererreichbarkeit bzw. zum Anlegen der Datenbank, bevor sie
    // überhaupt existiert (siehe DatabaseInitializer.TestServer/
    // CreateDatabase).
    public static string BuildMsSqlServerOnlyConnectionString(MsSqlConfiguration config)
    {
        string server = config.Port > 0
            ? $"{config.Server},{config.Port}"
            : config.Server;

        return $"Server={server};" +
               $"User Id={config.Username};Password={config.Password};" +
               "TrustServerCertificate=True;";
    }
}