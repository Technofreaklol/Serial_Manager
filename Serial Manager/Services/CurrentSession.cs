using SerialManager.Models;

namespace SerialManager.Services;

public static class CurrentSession
{
    public static User? CurrentUser { get; set; }

    public static bool IsAdmin =>
        CurrentUser?.Role == "Administrator";
}