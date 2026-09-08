using System.Text.Json;
using SerialManager.Models;
using System.IO;

namespace SerialManager.Services;

public class ApplicationConfigurationService
{
    private readonly string _file = AppPaths.ApplicationConfigFile;

    public ApplicationConfigurationService()
    {
        AppPaths.EnsureDirectories();
    }

    public ApplicationConfiguration Load()
    {
        if (!File.Exists(_file))
        {
            var config = new ApplicationConfiguration();
            Save(config);
            return config;
        }

        string json = File.ReadAllText(_file);

        return JsonSerializer.Deserialize<ApplicationConfiguration>(json)
               ?? new ApplicationConfiguration();
    }

    public void Save(ApplicationConfiguration config)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_file, json);
    }
}