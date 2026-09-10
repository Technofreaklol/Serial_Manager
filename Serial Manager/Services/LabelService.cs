using SerialManager.Models;

namespace SerialManager.Services;

public class LabelService
{
    private readonly ApplicationInfoService _appInfo = new();
    private readonly LabelLayoutService _layoutService = new();

    public LabelData CreateLabel(
        Article article,
        string serialNumber,
        Machine machine)
    {
        var layout = _layoutService.Load();

        return new LabelData
        {
            CompanyName = _appInfo.CompanyName,
            ArticleNumber = article.ArticleNumber,
            Description = article.Description,
            SerialNumber = serialNumber,
            Machine = machine.Name,
            Created = DateTime.Now,
            OperatorName = CurrentSession.CurrentUser?.FullName ?? Environment.UserName,

            ShowCompanyName = layout.ShowCompanyName,
            ShowArticleNumber = layout.ShowArticleNumber,
            ShowDescription = layout.ShowDescription,
            ShowSerialNumber = layout.ShowSerialNumber,
            ShowMachine = layout.ShowMachine,
            ShowDate = layout.ShowDate,
            ShowOperator = layout.ShowOperator,
            ShowQrCode = layout.ShowQrCode,
            LogoBytes = layout.GetLogoBytes(),
            WidthMm = layout.WidthMm,
            HeightMm = layout.HeightMm
        };
    }
}