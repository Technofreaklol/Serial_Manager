using SerialManager.Models;

namespace SerialManager.Services;

public class LabelService
{
    private readonly ApplicationInfoService _appInfo = new();

    public LabelData CreateLabel(
        Article article,
        string serialNumber,
        Machine machine)
    {
        return new LabelData
        {
            CompanyName = _appInfo.CompanyName,
            ArticleNumber = article.ArticleNumber,
            Description = article.Description,
            SerialNumber = serialNumber,
            Machine = machine.Name,
            Created = DateTime.Now,
            OperatorName = CurrentSession.CurrentUser?.FullName ?? Environment.UserName
        };
    }
}