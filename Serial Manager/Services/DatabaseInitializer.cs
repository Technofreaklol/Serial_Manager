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
                EnsureRowVersionColumn(db, "Articles");
                EnsureRowVersionColumn(db, "Machines");
                EnsureAuditLogTable(db);
                RepairSerialHistoryIndex(db);

                message = "SQLite-Datenbank erfolgreich erstellt/aktualisiert.";

                return true;
            }

            if (db.Database.IsMySql())
            {
                db.Database.Migrate();
                EnsureCoreTablesMySql(db);
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

        db.Database.OpenConnection();
        try
        {
            if (!SqliteHasColumn(db, "Articles", "IsActive"))
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

    // Ältere SQLite-Datenbanken wurden vor Einführung des RowVersion-Feldes
    // (Concurrency-Token, siehe SerialDbContext.OnModelCreating) angelegt und
    // haben deshalb weder in "Articles" noch in "Machines" diese Spalte.
    // Ohne sie meldet EF Core beim Lesen/Speichern "no such column: RowVersion"
    // ("es fehlen Felder"). EnsureCreated() legt diese Spalte NICHT nachträglich
    // an bestehenden Tabellen an – das übernimmt daher dieser Patch, analog zu
    // EnsureArticleIsActiveColumn oben.
    private static void EnsureRowVersionColumn(SerialDbContext db, string table)
    {
        if (!db.Database.IsSqlite())
            return;

        db.Database.OpenConnection();
        try
        {
            if (!SqliteHasColumn(db, table, "RowVersion"))
            {
                db.Database.ExecuteSqlRaw(
                    $"ALTER TABLE {table} ADD COLUMN RowVersion TEXT NOT NULL " +
                    "DEFAULT '0001-01-01 00:00:00';");
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static bool SqliteHasColumn(SerialDbContext db, string table, string column)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == column)
                return true;
        }

        return false;
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

    private static void EnsureCoreTablesMySql(SerialDbContext db)
    {
        if (!db.Database.IsMySql())
            return;

        // Sicherheitsnetz: __EFMigrationsHistory kann eine Migration als
        // "angewendet" führen, obwohl die zugehörige Tabelle fehlt
        // (z. B. nach versehentlichem Leeren/Truncate der Datenbank).
        // Migrate() würde diese Migration dann NICHT erneut ausführen,
        // da es sich rein auf die History verlässt. Deshalb hier direkt
        // und unabhängig vom Migrationsverlauf prüfen/anlegen.

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `Articles` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `ArticleNumber` varchar(255) NOT NULL,
                `Description` longtext NOT NULL,
                `CurrentSerialNumber` int NOT NULL,
                `RowVersion` timestamp(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
                `IsActive` tinyint(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_Articles_ArticleNumber` (`ArticleNumber`)
            ) CHARACTER SET=utf8mb4;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `Machines` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Name` varchar(255) NOT NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_Machines_Name` (`Name`)
            ) CHARACTER SET=utf8mb4;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `SerialHistories` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `ArticleNumber` varchar(255) NOT NULL,
                `SerialNumber` varchar(255) NOT NULL,
                `Created` datetime(6) NOT NULL,
                `Machine` longtext NOT NULL,
                `Operator` longtext NOT NULL,
                `Remark` longtext NULL,
                `LabelPrinted` tinyint(1) NOT NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_SerialHistories_ArticleNumber_SerialNumber` (`ArticleNumber`, `SerialNumber`)
            ) CHARACTER SET=utf8mb4;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `Settings` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Key` varchar(255) NOT NULL,
                `Value` longtext NOT NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_Settings_Key` (`Key`)
            ) CHARACTER SET=utf8mb4;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `Users` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Username` varchar(255) NOT NULL,
                `PasswordHash` longtext NOT NULL,
                `FullName` longtext NOT NULL,
                `Role` varchar(255) NOT NULL,
                `IsActive` tinyint(1) NOT NULL,
                `Created` datetime(6) NOT NULL,
                `LastLogin` datetime(6) NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_Users_Username` (`Username`)
            ) CHARACTER SET=utf8mb4;");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS `Machines` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Name` varchar(255) NOT NULL,
                `RowVersion` timestamp(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_Machines_Name` (`Name`)
            ) CHARACTER SET=utf8mb4;");

        // Falls Articles schon vor Einführung der IsActive-Spalte existierte.
        var connection = db.Database.GetDbConnection();
        connection.Open();

        try
        {
            using var check = connection.CreateCommand();
            check.CommandText = @"SELECT COUNT(*) FROM information_schema.columns
                                  WHERE table_schema = DATABASE()
                                    AND table_name = 'Articles'
                                    AND column_name = 'IsActive';";

            if (Convert.ToInt32(check.ExecuteScalar()) == 0)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText =
                    "ALTER TABLE `Articles` ADD COLUMN `IsActive` tinyint(1) NOT NULL DEFAULT 1;";
                alter.ExecuteNonQuery();
            }

            using var checkMachineRowVersion = connection.CreateCommand();
            checkMachineRowVersion.CommandText = @"SELECT COUNT(*) FROM information_schema.columns
                                  WHERE table_schema = DATABASE()
                                    AND table_name = 'Machines'
                                    AND column_name = 'RowVersion';";

            if (Convert.ToInt32(checkMachineRowVersion.ExecuteScalar()) == 0)
            {
                using var alterMachine = connection.CreateCommand();
                alterMachine.CommandText =
                    "ALTER TABLE `Machines` ADD COLUMN `RowVersion` timestamp(6) " +
                    "NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);";
                alterMachine.ExecuteNonQuery();
            }
        }
        finally
        {
            connection.Close();
        }
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