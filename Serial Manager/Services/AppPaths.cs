namespace SerialManager.Services;
using System.IO;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SerialManager");

    public static string ConfigFolder => Path.Combine(Root, "Config");
    public static string DatabaseConfigFile => Path.Combine(ConfigFolder, "database.json");
    public static string ApplicationConfigFile => Path.Combine(ConfigFolder, "appsettings.json");
    public static string BackupsFolder => Path.Combine(Root, "Backups");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ConfigFolder);
        Directory.CreateDirectory(BackupsFolder);
    }

    public static string GetDatabasePath(string file)
    {
        return Path.IsPathRooted(file)
            ? file
            : Path.Combine(Root, file);
    }
}
