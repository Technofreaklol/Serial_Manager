using System.IO;
using SerialManager.Services;

namespace SerialManager.Services;

public class BackupService
{
    private readonly DatabaseConfigurationService _databaseConfig = new();
    private readonly SettingsService _settings = new();

    private readonly string _backupPath;

    public BackupService()
    {
        AppPaths.EnsureDirectories();
        _backupPath = AppPaths.BackupsFolder;
    }

    public string CreateBackup()
    {
        var config = _databaseConfig.Load();

        if (config.Provider == "SQLite")
        {
            return BackupSQLite(config.SQLite.File);
        }

        throw new NotSupportedException(
            "MySQL-Backups werden im nächsten Schritt implementiert.");
    }

    private string BackupSQLite(string databaseFile)
    {
        string dbPath = AppPaths.GetDatabasePath(databaseFile);

        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Datenbank wurde nicht gefunden.");

        string destination = Path.Combine(
            _backupPath,
            $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db");

        File.Copy(dbPath, destination, true);

        Cleanup();

        return destination;
    }

    public void RestoreBackup(string backupFile)
    {
        var config = _databaseConfig.Load();

        if (config.Provider == "SQLite")
        {
            string dbPath = AppPaths.GetDatabasePath(
                config.SQLite.File);

            File.Copy(backupFile, dbPath, true);

            return;
        }

        throw new NotSupportedException(
            "MySQL-Wiederherstellung wird im nächsten Schritt implementiert.");
    }

    public List<FileInfo> GetBackups()
    {
        return new DirectoryInfo(_backupPath)
            .GetFiles("Backup_*.db")
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