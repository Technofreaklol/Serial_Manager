using SerialManager.Models;
using System.IO;
using System.Reflection;

namespace SerialManager.Services;

public class ApplicationInfoService
{
    private readonly ApplicationConfigurationService _configService = new();
    private readonly ApplicationConfiguration _config;

    public ApplicationInfoService()
    {
        _config = _configService.Load();
    }

    public string CompanyName => _config.CompanyName;

    public string ApplicationName => _config.ApplicationName;

    public string FullApplicationName =>
        $"{CompanyName} {ApplicationName}";

    public string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            return version == null
                ? "1.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public string BuildDate
    {
        get
        {
            try
            {
                // Funktioniert auch bei einer Single-File-EXE.
                string executablePath =
                    Environment.ProcessPath
                    ?? Path.Combine(
                        AppContext.BaseDirectory,
                        "SerialManager.exe");

                if (File.Exists(executablePath))
                {
                    return File.GetLastWriteTime(executablePath)
                        .ToString("dd.MM.yyyy HH:mm");
                }

                return "Unbekannt";
            }
            catch
            {
                return "Unbekannt";
            }
        }
    }
}