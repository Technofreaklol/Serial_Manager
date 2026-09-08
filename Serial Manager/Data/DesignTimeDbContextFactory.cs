using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SerialManager.Services;
using System.IO;


namespace SerialManager.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SerialDbContext>
{
    public SerialDbContext CreateDbContext(string[] args)
    {
        var config = new DatabaseConfigurationService().Load();

        var optionsBuilder = new DbContextOptionsBuilder<SerialDbContext>();

        if (config.Provider == "MySQL")
        {
            string connection =
                $"server={config.MySQL.Server};" +
                $"port={config.MySQL.Port};" +
                $"database={config.MySQL.Database};" +
                $"user={config.MySQL.Username};" +
                $"password={config.MySQL.Password};";

            optionsBuilder.UseMySql(
                connection,
                ServerVersion.AutoDetect(connection));
        }
        else
        {
            optionsBuilder.UseSqlite(
                $"Data Source={AppPaths.GetDatabasePath(config.SQLite.File)}");
        }

        return new SerialDbContext(optionsBuilder.Options);
    }
}