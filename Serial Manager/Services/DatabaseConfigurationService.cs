using Microsoft.Extensions.Configuration;
using SerialManager.Models;
using System.Text.Json;
using System.IO;

namespace SerialManager.Services;

public class DatabaseConfigurationService
{
    private readonly string _configFolder;
    private readonly string _configFile;

    public DatabaseConfigurationService()
    {
        AppPaths.EnsureDirectories();

        _configFolder = AppPaths.ConfigFolder;
        _configFile = AppPaths.DatabaseConfigFile;
    }

    public DatabaseConfiguration Load()
    {
        if (!File.Exists(_configFile))
        {
            var config = CreateDefault();
            Save(config);
            return config;
        }

        var json = File.ReadAllText(_configFile);

        return JsonSerializer.Deserialize<DatabaseConfiguration>(json)
               ?? CreateDefault();
    }

    public void Save(DatabaseConfiguration configuration)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(configuration, options);

        File.WriteAllText(_configFile, json);
    }

    private static DatabaseConfiguration CreateDefault()
    {
        return new DatabaseConfiguration
        {
            Provider = "SQLite",
            SQLite = new SQLiteConfiguration
            {
                File = "serialmanager.db"
            },
            MySQL = new MySqlConfiguration
            {
                Server = "localhost",
                Port = 3306,
                Database = "serialmanager",
                Username = "root",
                Password = ""
            }
        };
    }
}