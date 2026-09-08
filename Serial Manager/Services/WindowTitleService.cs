namespace SerialManager.Services;

public class WindowTitleService
{
    private readonly ApplicationInfoService _appInfo = new();

    public static event Action? TitleChanged;

    public string GetTitle(string windowName)
    {
        return $"{_appInfo.CompanyName} - {windowName}";
    }

    public static void NotifyTitleChanged()
    {
        TitleChanged?.Invoke();
    }
}