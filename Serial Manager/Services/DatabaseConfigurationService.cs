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

        var configuration = JsonSerializer.Deserialize<DatabaseConfiguration>(json)
                             ?? CreateDefault();

        // Alte database.json-Dateien (von vor der DPAPI-Verschlüsselung)
        // haben die Passwörter noch im Klartext gespeichert - das wird hier
        // erkannt und beim ersten Laden automatisch (einmalig) verschlüsselt
        // zurückgeschrieben, ohne dass der Benutzer die Zugangsdaten neu
        // eingeben muss. Rückgabewert dieser Methode ist IMMER Klartext -
        // Ver-/Entschlüsselung ist ein reines Speicherformat-Detail von
        // Save()/Load(), der Rest der Anwendung (Verbindungsaufbau usw.)
        // bekommt davon nichts mit.
        bool hadLegacyPlainTextPassword =
            (!string.IsNullOrEmpty(configuration.MySQL.Password) && !SecretProtector.IsProtected(configuration.MySQL.Password)) ||
            (!string.IsNullOrEmpty(configuration.MSSQL.Password) && !SecretProtector.IsProtected(configuration.MSSQL.Password));

        configuration.MySQL.Password = SecretProtector.Unprotect(configuration.MySQL.Password);
        configuration.MSSQL.Password = SecretProtector.Unprotect(configuration.MSSQL.Password);

        if (hadLegacyPlainTextPassword)
            Save(configuration);

        return configuration;
    }

    public void Save(DatabaseConfiguration configuration)
    {
        // Nur für die Datei auf der Platte verschlüsseln - das übergebene
        // Objekt bleibt für den Aufrufer unverändert im Klartext (z. B.
        // DatabaseSetupWindow arbeitet nach dem Speichern oft direkt weiter
        // damit, etwa für einen Verbindungstest).
        var toPersist = new DatabaseConfiguration
        {
            Provider = configuration.Provider,
            SQLite = configuration.SQLite,
            MySQL = new MySqlConfiguration
            {
                Server = configuration.MySQL.Server,
                Port = configuration.MySQL.Port,
                Database = configuration.MySQL.Database,
                Username = configuration.MySQL.Username,
                Password = SecretProtector.Protect(configuration.MySQL.Password),
                ServerVersion = configuration.MySQL.ServerVersion
            },
            MSSQL = new MsSqlConfiguration
            {
                Server = configuration.MSSQL.Server,
                Port = configuration.MSSQL.Port,
                Database = configuration.MSSQL.Database,
                Username = configuration.MSSQL.Username,
                Password = SecretProtector.Protect(configuration.MSSQL.Password)
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(toPersist, options);

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