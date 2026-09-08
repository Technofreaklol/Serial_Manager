using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class DatabaseInitializer
{
    private readonly DatabaseConfigurationService _configService = new();

    public bool TestServer(out string message)
    {
        try
        {
            var config = _configService.Load();

            if (config.Provider == "SQLite")
            {
                message = "SQLite benötigt keinen Server.";
                return true;
            }

            string connection =
                $"server={config.MySQL.Server};" +
                $"port={config.MySQL.Port};" +
                $"user={config.MySQL.Username};" +
                $"password={config.MySQL.Password};";

            using var conn = new MySqlConnection(connection);
            conn.Open();

            var version = ServerVersion.AutoDetect(connection);

            config.MySQL.ServerVersion = version.ToString();

            _configService.Save(config);

            message = $"Server erreichbar ({version}).";
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write("Datenbankoperation fehlgeschlagen.", ex);
            message = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    public bool CreateDatabase(out string message)
    {
        try
        {
            var config = _configService.Load();

            if (config.Provider == "SQLite")
            {
                message = "SQLite erstellt die Datenbank automatisch.";
                return true;
            }

            string connection =
                $"server={config.MySQL.Server};" +
                $"port={config.MySQL.Port};" +
                $"user={config.MySQL.Username};" +
                $"password={config.MySQL.Password};";

            using var conn = new MySqlConnection(connection);

            conn.Open();

            using var cmd = conn.CreateCommand();

            cmd.CommandText =
                $"CREATE DATABASE IF NOT EXISTS `{config.MySQL.Database}` " +
                $"CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

            cmd.ExecuteNonQuery();

            message = "Datenbank erfolgreich erstellt.";

            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write("Datenbankoperation fehlgeschlagen.", ex);
            message = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    public bool InitializeDatabase(out string message)
    {
        try
        {
            using var db = DbContextFactory.Create();

            if (db.Database.IsSqlite())
            {
                db.Database.EnsureCreated();
                EnsureArticleIsActiveColumn(db);
                EnsureAuditLogTable(db);
                RepairSerialHistoryIndex(db);

                message = "SQLite-Datenbank erfolgreich erstellt/aktualisiert.";

                return true;
            }

            if (db.Database.IsMySql())
            {
                db.Database.Migrate();
                RepairSerialHistoryIndex(db);

                message = "MySQL-Migration erfolgreich abgeschlossen.";

                return true;
            }

            message = "Unbekannter Datenbankprovider.";
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write("Datenbankoperation fehlgeschlagen.", ex);
            message = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    private static void EnsureArticleIsActiveColumn(SerialDbContext db)
    {
        if (!db.Database.IsSqlite())
            return;

        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info('Articles');";

        db.Database.OpenConnection();
        try
        {
            bool hasColumn = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.GetString(1) == "IsActive")
                    {
                        hasColumn = true;
                        break;
                    }
                }
            }

            if (!hasColumn)
            {
                db.Database.ExecuteSqlRaw(
                    "ALTER TABLE Articles ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;");
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static void EnsureAuditLogTable(SerialDbContext db)
    {
        if (!db.Database.IsSqlite())
            return;

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS AuditLogEntries (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Created TEXT NOT NULL,
                Username TEXT NOT NULL,
                Action TEXT NOT NULL,
                Details TEXT NOT NULL
            );");
    }

    private static void RepairSerialHistoryIndex(SerialDbContext db)
    {
        // Frühe Versionen hatten SerialNumber allein als UNIQUE-Index.
        // Dadurch konnte Artikel B keine 0001 erzeugen, wenn Artikel A bereits 0001 hatte.
        if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw(
                "DROP INDEX IF EXISTS IX_SerialHistories_SerialNumber;");

            db.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_SerialHistories_ArticleNumber_SerialNumber " +
                "ON SerialHistories (ArticleNumber, SerialNumber);");

            return;
        }

        if (db.Database.IsMySql())
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();

            try
            {
                using var check = connection.CreateCommand();
                check.CommandText = @"SELECT COUNT(*)
                                      FROM information_schema.statistics
                                      WHERE table_schema = DATABASE()
                                        AND table_name = 'SerialHistories'
                                        AND index_name = 'IX_SerialHistories_SerialNumber';";

                var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;

                if (exists)
                {
                    using var drop = connection.CreateCommand();
                    drop.CommandText =
                        "DROP INDEX `IX_SerialHistories_SerialNumber` ON `SerialHistories`;";
                    drop.ExecuteNonQuery();
                }

                using var create = connection.CreateCommand();
                create.CommandText = @"CREATE UNIQUE INDEX `IX_SerialHistories_ArticleNumber_SerialNumber`
                                       ON `SerialHistories` (`ArticleNumber`, `SerialNumber`);";

                try
                {
                    create.ExecuteNonQuery();
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1061)
                {
                    // Der korrekte Index existiert bereits.
                }
            }
            finally
            {
                connection.Close();
            }
        }
    }

    public bool Initialize(out string message)
    {
        if (!TestServer(out message))
            return false;

        if (!CreateDatabase(out message))
            return false;

        return InitializeDatabase(out message);
    }


    public DatabaseStatus GetStatus()
    {
        var status = new DatabaseStatus();

        try
        {
            var config = new DatabaseConfigurationService().Load();

            status.Provider = config.Provider;

            using var db = DbContextFactory.Create();

            status.CanConnect = db.Database.CanConnect();

            if (!status.CanConnect)
                return status;

            status.ArticleCount = db.Articles.Count();
            status.MachineCount = db.Machines.Count();
            status.HistoryCount = db.SerialHistories.Count();
            status.SettingsCount = db.Settings.Count();
        }
        catch
        {
            status.CanConnect = false;
        }

        return status;
    }

    public bool CanStartApplication(out string message)
    {
        var config = _configService.Load();

        // SQLite startet immer
        if (config.Provider == "SQLite")
        {
            message = "SQLite";
            return true;
        }

        try
        {
            string connection =
                $"server={config.MySQL.Server};" +
                $"port={config.MySQL.Port};" +
                $"database={config.MySQL.Database};" +
                $"user={config.MySQL.Username};" +
                $"password={config.MySQL.Password};";

            using var conn = new MySqlConnection(connection);
            conn.Open();

            message = "Verbindung erfolgreich.";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

}