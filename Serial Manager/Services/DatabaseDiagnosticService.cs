using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using SerialManager.Data;

namespace SerialManager.Services;

public class DatabaseDiagnosticService
{
    public DatabaseStatus GetStatus()
    {
        var status = new DatabaseStatus();

        try
        {
            var config = new DatabaseConfigurationService().Load();
            status.Provider = config.Provider;

            if (config.Provider == "SQLite")
            {
                status.DatabaseName = config.SQLite.File;
                status.Server = "Lokal";
            }
            else if (config.Provider == "MSSQL")
            {
                status.DatabaseName = config.MSSQL.Database;
                status.Server = config.MSSQL.Port > 0
                    ? $"{config.MSSQL.Server},{config.MSSQL.Port}"
                    : config.MSSQL.Server;
            }
            else
            {
                status.DatabaseName = config.MySQL.Database;
                status.Server = $"{config.MySQL.Server}:{config.MySQL.Port}";
            }

            using var db = DbContextFactory.Create();
            status.CanConnect = db.Database.CanConnect();

            if (!status.CanConnect)
            {
                status.Error = "Keine Verbindung zur Datenbank möglich.";
                return status;
            }

            CheckTables(db, status);
            CheckSerialIndex(db, status);
            CheckMigrations(db, status);

            status.ArticleCount = db.Articles.Count();
            status.MachineCount = db.Machines.Count();
            status.HistoryCount = db.SerialHistories.Count();
            status.SettingsCount = db.Settings.Count();
        }
        catch (Exception ex)
        {
            status.Error = GetFullMessage(ex);
        }

        return status;
    }

    public bool Repair(out string message)
    {
        try
        {
            using var db = DbContextFactory.Create();
            if (!db.Database.CanConnect())
            {
                message = "Keine Verbindung zur Datenbank möglich.";
                return false;
            }

            if (db.Database.IsSqlite() || db.Database.IsSqlServer())
                db.Database.EnsureCreated();
            else if (db.Database.IsMySql())
                db.Database.Migrate();

            RepairSerialHistoryIndex(db);
            message = "Datenbankprüfung und Reparatur erfolgreich abgeschlossen.";
            return true;
        }
        catch (Exception ex)
        {
            message = GetFullMessage(ex);
            return false;
        }
    }

    private static void CheckTables(SerialDbContext db, DatabaseStatus status)
    {
        try
        {
            // Die Abfragen erzwingen eine echte Prüfung der wichtigsten Tabellen.
            _ = db.Articles.Take(1).Count();
            _ = db.Machines.Take(1).Count();
            _ = db.SerialHistories.Take(1).Count();
            _ = db.Settings.Take(1).Count();
            _ = db.Users.Take(1).Count();
            status.TablesOk = true;
        }
        catch
        {
            status.TablesOk = false;
        }
    }

    private static void CheckSerialIndex(SerialDbContext db, DatabaseStatus status)
    {
        if (db.Database.IsSqlite())
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA index_list('SerialHistories');";
            db.Database.OpenConnection();
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(1);
                    if (name == "IX_SerialHistories_SerialNumber")
                        status.LegacySerialIndexFound = true;
                    if (name == "IX_SerialHistories_ArticleNumber_SerialNumber")
                        status.SerialIndexOk = true;
                }
            }
            finally
            {
                db.Database.CloseConnection();
            }
            return;
        }

        if (db.Database.IsMySql())
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT index_name, GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',')
                                        FROM information_schema.statistics
                                        WHERE table_schema = DATABASE()
                                          AND table_name = 'SerialHistories'
                                        GROUP BY index_name;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var columns = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (name == "IX_SerialHistories_SerialNumber")
                        status.LegacySerialIndexFound = true;
                    if (name == "IX_SerialHistories_ArticleNumber_SerialNumber" &&
                        columns.Equals("ArticleNumber,SerialNumber", StringComparison.OrdinalIgnoreCase))
                        status.SerialIndexOk = true;
                }
            }
            finally
            {
                connection.Close();
            }

            return;
        }

        if (db.Database.IsSqlServer())
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT i.name, c.name
                                        FROM sys.indexes i
                                        JOIN sys.index_columns ic
                                            ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                                        JOIN sys.columns c
                                            ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                        WHERE i.object_id = OBJECT_ID('dbo.SerialHistories')
                                        ORDER BY i.name, ic.key_ordinal;";
                using var reader = command.ExecuteReader();

                var columnsByIndex = new Dictionary<string, List<string>>();

                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var column = reader.GetString(1);

                    if (!columnsByIndex.TryGetValue(name, out var columns))
                        columnsByIndex[name] = columns = new List<string>();

                    columns.Add(column);
                }

                foreach (var (name, columns) in columnsByIndex)
                {
                    if (name == "IX_SerialHistories_SerialNumber")
                        status.LegacySerialIndexFound = true;

                    if (name == "IX_SerialHistories_ArticleNumber_SerialNumber" &&
                        columns.Count == 2 &&
                        columns[0] == "ArticleNumber" && columns[1] == "SerialNumber")
                        status.SerialIndexOk = true;
                }
            }
            finally
            {
                connection.Close();
            }
        }
    }

    private static void CheckMigrations(SerialDbContext db, DatabaseStatus status)
    {
        // SQLite und SQL Server nutzen bewusst keine EF-Migrationen (siehe
        // DatabaseInitializer: EnsureCreated() + manuelle Spalten-Patches
        // statt Migrate()). Die __EFMigrationsHistory-Tabelle wird dabei nie
        // angelegt/befüllt, daher würde GetPendingMigrations() hier IMMER
        // alle Migrationen als "ausstehend" melden – unabhängig davon, ob das
        // Schema aktuell ist. Das wäre bei jeder solchen Datenbank
        // irreführend, deshalb hier gar nicht erst prüfen.
        if (db.Database.IsSqlite() || db.Database.IsSqlServer())
        {
            status.MigrationStatus = "Nicht zutreffend (automatische Schemaprüfung statt Migrationen)";
            return;
        }

        try
        {
            var pending = db.Database.GetPendingMigrations().ToList();
            status.MigrationStatus = pending.Count == 0
                ? "Aktuell"
                : $"{pending.Count} Migration(en) ausstehend";
        }
        catch (Exception ex)
        {
            status.MigrationStatus = "Nicht prüfbar: " + ex.Message;
        }
    }

    private static void RepairSerialHistoryIndex(SerialDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_SerialHistories_SerialNumber;");
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
                check.CommandText = @"SELECT COUNT(*) FROM information_schema.statistics
                                      WHERE table_schema = DATABASE()
                                        AND table_name = 'SerialHistories'
                                        AND index_name = 'IX_SerialHistories_SerialNumber';";
                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                {
                    using var drop = connection.CreateCommand();
                    drop.CommandText = "DROP INDEX `IX_SerialHistories_SerialNumber` ON `SerialHistories`;";
                    drop.ExecuteNonQuery();
                }

                using var create = connection.CreateCommand();
                create.CommandText = @"CREATE UNIQUE INDEX `IX_SerialHistories_ArticleNumber_SerialNumber`
                                       ON `SerialHistories` (`ArticleNumber`, `SerialNumber`);";
                try
                {
                    create.ExecuteNonQuery();
                }
                catch (MySqlException ex) when (ex.Number == 1061)
                {
                    // Index existiert bereits.
                }
            }
            finally
            {
                connection.Close();
            }

            return;
        }

        if (db.Database.IsSqlServer())
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();
            try
            {
                using var check = connection.CreateCommand();
                check.CommandText = @"SELECT COUNT(*) FROM sys.indexes
                                      WHERE object_id = OBJECT_ID('dbo.SerialHistories')
                                        AND name = 'IX_SerialHistories_SerialNumber';";

                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                {
                    using var drop = connection.CreateCommand();
                    drop.CommandText =
                        "DROP INDEX [IX_SerialHistories_SerialNumber] ON [dbo].[SerialHistories];";
                    drop.ExecuteNonQuery();
                }

                using var checkNew = connection.CreateCommand();
                checkNew.CommandText = @"SELECT COUNT(*) FROM sys.indexes
                                         WHERE object_id = OBJECT_ID('dbo.SerialHistories')
                                           AND name = 'IX_SerialHistories_ArticleNumber_SerialNumber';";

                if (Convert.ToInt32(checkNew.ExecuteScalar()) == 0)
                {
                    using var create = connection.CreateCommand();
                    create.CommandText = @"CREATE UNIQUE INDEX [IX_SerialHistories_ArticleNumber_SerialNumber]
                                           ON [dbo].[SerialHistories] ([ArticleNumber], [SerialNumber]);";
                    create.ExecuteNonQuery();
                }
            }
            finally
            {
                connection.Close();
            }
        }
    }

    private static string GetFullMessage(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(Environment.NewLine + "→ ", messages.Distinct());
    }
}
