using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class SettingsService
{
    public string GetValue(string key, string defaultValue = "")
    {
        using var db = DbContextFactory.Create();

        var setting = db.Settings
                        .FirstOrDefault(s => s.Key == key);

        return setting?.Value ?? defaultValue;
    }

    public void SetValue(string key, string value)
    {
        using var db = DbContextFactory.Create();

        var setting = db.Settings
                        .FirstOrDefault(s => s.Key == key);

        if (setting == null)
        {
            db.Settings.Add(new Setting
            {
                Key = key,
                Value = value
            });
        }
        else
        {
            setting.Value = value;
        }

        db.SaveChanges();
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        string value = GetValue(key, defaultValue.ToString());

        return bool.TryParse(value, out bool result)
            ? result
            : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        SetValue(key, value.ToString());
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        string value = GetValue(key, defaultValue.ToString());

        return int.TryParse(value, out int result)
            ? result
            : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        SetValue(key, value.ToString());
    }

    public Dictionary<string, string> GetAll()
    {
        using var db = DbContextFactory.Create();

        return db.Settings
                 .ToDictionary(s => s.Key, s => s.Value);
    }
}