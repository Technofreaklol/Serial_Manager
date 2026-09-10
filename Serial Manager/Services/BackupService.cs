using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SerialManager.Data;
using SerialManager.Models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SerialManager.Services;

public class BackupService
{
    private readonly DatabaseConfigurationService _databaseConfig = new();
    private readonly SettingsService _settings = new();

    private static readonly string[] Tables =
    {
        "Articles", "Machines", "SerialHistories", "Settings", "Users", "AuditLogEntries"
    };

    private readonly string _backupPath;

    public BackupService()
    {
        AppPaths.EnsureDirectories();
        _backupPath = AppPaths.BackupsFolder;
    }

    public string CreateBackup(IProgress<BackupProgress>? progress = null)
    {
        var config = _databaseConfig.Load();

        return config.Provider switch
        {
            "SQLite" => BackupSQLite(config.SQLite.File, progress),
            "MSSQL" => BackupMsSql(config.MSSQL, progress),
            _ => BackupMySql(config.MySQL, progress)
        };
    }

    public bool HasExistingDatabase()
    {
        var config = _databaseConfig.Load();

        if (config.Provider == "SQLite")
            return File.Exists(AppPaths.GetDatabasePath(config.SQLite.File));

        try
        {
            using var db = DbContextFactory.Create();

            if (!db.Database.CanConnect())
                return false;

            // MySQL nutzt echte EF-Migrationen (Migrate()), daher zeigt eine
            // vorhandene Migrationshistorie zuverlässig eine bereits
            // eingerichtete Datenbank an. SQL Server nutzt wie SQLite bewusst
            // KEINE Migrationen (siehe DatabaseInitializer), daher wäre
            // GetAppliedMigrations() dort immer leer - selbst wenn die
            // Datenbank längst Daten enthält. Dort stattdessen direkt prüfen,
            // ob die wichtigsten Tabellen tatsächlich lesbar sind.
            if (db.Database.IsSqlServer())
            {
                _ = db.Articles.Take(1).Count();
                _ = db.Machines.Take(1).Count();
                _ = db.SerialHistories.Take(1).Count();
                _ = db.Settings.Take(1).Count();
                _ = db.Users.Take(1).Count();
                return true;
            }

            return db.Database.GetAppliedMigrations().Any();
        }
        catch
        {
            return false;
        }
    }
    public void RestoreBackup(string backupFile)
    {
        var config = _databaseConfig.Load();

        if (config.Provider == "SQLite")
        {
            string dbPath = AppPaths.GetDatabasePath(config.SQLite.File);
            File.Copy(backupFile, dbPath, true);
            return;
        }

        if (config.Provider == "MSSQL")
        {
            RestoreMsSql(backupFile, config.MSSQL);
            return;
        }

        RestoreMySql(backupFile, config.MySQL);
    }

    private string BackupSQLite(string databaseFile, IProgress<BackupProgress>? progress)
    {
        progress?.Report(new BackupProgress { Percent = 0, Message = "Datenbankdatei wird kopiert..." });

        string dbPath = AppPaths.GetDatabasePath(databaseFile);

        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Datenbank wurde nicht gefunden.");

        string destination = Path.Combine(
            _backupPath,
            $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db");

        File.Copy(dbPath, destination, true);

        Cleanup();

        progress?.Report(new BackupProgress { Percent = 100, Message = "Backup abgeschlossen." });

        return destination;
    }

    private string BackupMySql(MySqlConfiguration config, IProgress<BackupProgress>? progress)
    {
        var statements = new List<string>
        {
            "SET FOREIGN_KEY_CHECKS=0;"
        };

        using (var connection = OpenConnection(config))
        {
            for (int i = 0; i < Tables.Length; i++)
            {
                var table = Tables[i];

                progress?.Report(new BackupProgress
                {
                    Percent = (int)((double)i / Tables.Length * 90),
                    Message = $"Sichere Tabelle '{table}' ({i + 1}/{Tables.Length})..."
                });

                statements.Add($"DELETE FROM `{table}`;");
                statements.AddRange(BuildInsertStatements(connection, table));
            }
        }

        statements.Add("SET FOREIGN_KEY_CHECKS=1;");

        progress?.Report(new BackupProgress { Percent = 95, Message = "Backup-Datei wird geschrieben..." });

        var data = new BackupData
        {
            CreatedAt = DateTime.Now,
            Database = config.Database,
            Statements = statements
        };

        string destination = Path.Combine(
            _backupPath,
            $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.smbak");

        File.WriteAllText(destination, JsonSerializer.Serialize(data), Encoding.UTF8);

        Cleanup();

        progress?.Report(new BackupProgress { Percent = 100, Message = "Backup abgeschlossen." });

        return destination;
    }

    private void RestoreMySql(string backupFile, MySqlConfiguration config)
    {
        var json = File.ReadAllText(backupFile);

        var data = JsonSerializer.Deserialize<BackupData>(json)
            ?? throw new Exception("Die Backup-Datei konnte nicht gelesen werden.");

        using var connection = OpenConnection(config);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var statement in data.Statements)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private string BackupMsSql(MsSqlConfiguration config, IProgress<BackupProgress>? progress)
    {
        var statements = new List<string>();

        using (var connection = OpenMsSqlConnection(config))
        {
            for (int i = 0; i < Tables.Length; i++)
            {
                var table = Tables[i];

                progress?.Report(new BackupProgress
                {
                    Percent = (int)((double)i / Tables.Length * 90),
                    Message = $"Sichere Tabelle '{table}' ({i + 1}/{Tables.Length})..."
                });

                // Alle Tabellen haben eine IDENTITY-Id-Spalte. Damit die
                // Wiederherstellung dieselben Id-Werte wie im Original
                // vergeben kann (Historie/Verweise bleiben sonst nicht
                // konsistent), muss IDENTITY_INSERT je Tabelle kurz
                // aktiviert werden - SQL Server erlaubt das immer nur für
                // eine Tabelle gleichzeitig pro Verbindung.
                statements.Add($"SET IDENTITY_INSERT [{table}] ON;");
                statements.Add($"DELETE FROM [{table}];");
                statements.AddRange(BuildInsertStatementsMsSql(connection, table));
                statements.Add($"SET IDENTITY_INSERT [{table}] OFF;");
            }
        }

        progress?.Report(new BackupProgress { Percent = 95, Message = "Backup-Datei wird geschrieben..." });

        var data = new BackupData
        {
            CreatedAt = DateTime.Now,
            Database = config.Database,
            Statements = statements
        };

        string destination = Path.Combine(
            _backupPath,
            $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.smbak");

        File.WriteAllText(destination, JsonSerializer.Serialize(data), Encoding.UTF8);

        Cleanup();

        progress?.Report(new BackupProgress { Percent = 100, Message = "Backup abgeschlossen." });

        return destination;
    }

    private void RestoreMsSql(string backupFile, MsSqlConfiguration config)
    {
        var json = File.ReadAllText(backupFile);

        var data = JsonSerializer.Deserialize<BackupData>(json)
            ?? throw new Exception("Die Backup-Datei konnte nicht gelesen werden.");

        using var connection = OpenMsSqlConnection(config);
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var statement in data.Statements)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static SqlConnection OpenMsSqlConnection(MsSqlConfiguration config)
    {
        var connection = new SqlConnection(
            DbContextFactory.BuildMsSqlConnectionString(config));

        connection.Open();
        return connection;
    }

    private static List<string> BuildInsertStatementsMsSql(SqlConnection connection, string table)
    {
        var statements = new List<string>();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM [{table}];";

        using var reader = command.ExecuteReader();

        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();

        while (reader.Read())
        {
            var values = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                values.Add(ToSqlLiteralMsSql(value));
            }

            var columnList = string.Join(", ", columnNames.Select(c => $"[{c}]"));
            var valueList = string.Join(", ", values);

            statements.Add($"INSERT INTO [{table}] ({columnList}) VALUES ({valueList});");
        }

        return statements;
    }

    private static MySqlConnection OpenConnection(MySqlConfiguration config)
    {
        var connectionString =
            $"Server={config.Server};Port={config.Port};Database={config.Database};" +
            $"User={config.Username};Password={config.Password};";

        var connection = new MySqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static List<string> BuildInsertStatements(MySqlConnection connection, string table)
    {
        var statements = new List<string>();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM `{table}`;";

        using var reader = command.ExecuteReader();

        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();

        while (reader.Read())
        {
            var values = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                values.Add(ToSqlLiteral(value));
            }

            var columnList = string.Join(", ", columnNames.Select(c => $"`{c}`"));
            var valueList = string.Join(", ", values);

            statements.Add($"INSERT INTO `{table}` ({columnList}) VALUES ({valueList});");
        }

        return statements;
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value is null)
            return "NULL";

        return value switch
        {
            bool b => b ? "1" : "0",
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
            float or double or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => $"'{EscapeString(value.ToString() ?? string.Empty)}'"
        };
    }

    private static string EscapeString(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\0", "");
    }

    // T-SQL kennt KEIN Backslash-Escaping (anders als MySQL) - ein
    // Backslash ist dort ein ganz normales Zeichen. Ein einzelnes
    // Anführungszeichen wird stattdessen verdoppelt ('' statt \').
    // Deshalb eine eigene Literal-/Escape-Funktion für SQL Server statt
    // ToSqlLiteral/EscapeString wiederzuverwenden - sonst würde z. B. ein
    // Apostroph in einer Artikelbeschreibung ("O'Brien") ein syntaktisch
    // ungültiges T-SQL-Statement erzeugen.
    private static string ToSqlLiteralMsSql(object? value)
    {
        if (value is null)
            return "NULL";

        return value switch
        {
            bool b => b ? "1" : "0",
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
            float or double or decimal =>
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => $"'{EscapeStringMsSql(value.ToString() ?? string.Empty)}'"
        };
    }

    private static string EscapeStringMsSql(string input)
    {
        return input
            .Replace("'", "''")
            .Replace("\0", "");
    }

    public List<FileInfo> GetBackups()
    {
        var provider = _databaseConfig.Load().Provider;

        // MySQL und MSSQL nutzen dasselbe generische .smbak-Format
        // (Liste von SQL-Anweisungen als JSON) - nur der Inhalt der
        // Anweisungen unterscheidet sich je nach Provider-Syntax.
        string pattern = provider == "SQLite" ? "Backup_*.db" : "Backup_*.smbak";

        return new DirectoryInfo(_backupPath)
            .GetFiles(pattern)
            .OrderByDescending(f => f.CreationTime)
            .ToList();
    }

    private void Cleanup()
    {
        int keep = _settings.GetInt("BackupCount", 20);

        foreach (var file in GetBackups().Skip(keep))
        {
            file.Delete();
        }
    }
}