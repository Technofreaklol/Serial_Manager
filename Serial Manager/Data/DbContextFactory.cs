using Microsoft.EntityFrameworkCore;
using System.IO;
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
}