namespace SerialManager.Models;

public class ApplicationConfiguration
{
    public string CompanyName { get; set; } = "Stewe";

    public string ApplicationName { get; set; } = "Serial Manager";
    public bool ShowLabelPreview { get; set; } = true;
    public string LastUsername { get; set; } = string.Empty;

}