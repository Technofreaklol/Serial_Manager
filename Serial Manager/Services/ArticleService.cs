using Microsoft.EntityFrameworkCore;
using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class ArticleService
{
    private readonly AuditLogService _auditLog = new();

    public List<Article> GetArticles()
    {
        using var db = DbContextFactory.Create();

        return db.Articles
                 .Where(a => a.IsActive)
                 .OrderBy(a => a.ArticleNumber)
                 .ToList();
    }

    public List<Article> GetAllArticles()
    {
        using var db = DbContextFactory.Create();

        return db.Articles
                 .OrderBy(a => a.ArticleNumber)
                 .ToList();
    }

    public Article? GetArticle(string articleNumber)
    {
        using var db = DbContextFactory.Create();

        return db.Articles
                 .FirstOrDefault(a => a.ArticleNumber == articleNumber);
    }

    public Article? GetArticle(int id)
    {
        using var db = DbContextFactory.Create();

        return db.Articles
                 .FirstOrDefault(a => a.Id == id);
    }

    public void SaveArticle(string articleNumber, string description)
    {
        using var db = DbContextFactory.Create();

        var article = db.Articles
                        .FirstOrDefault(a => a.ArticleNumber == articleNumber);

        if (article == null)
        {
            article = new Article
            {
                ArticleNumber = articleNumber,
                Description = description
            };

            db.Articles.Add(article);
        }
        else
        {
            article.Description = description;
        }

        db.SaveChanges();
    }

    public void DeleteArticle(int id)
    {
        using var db = DbContextFactory.Create();

        var article = db.Articles.Find(id);

        if (article == null)
            return;

        var relatedHistory = db.SerialHistories
            .Where(h => h.ArticleNumber == article.ArticleNumber)
            .ToList();

        db.SerialHistories.RemoveRange(relatedHistory);
        db.Articles.Remove(article);

        db.SaveChanges();

        _auditLog.Log("Artikel gelöscht",
            $"Artikelnummer: {article.ArticleNumber}, {relatedHistory.Count} Seriennummer(n) mitgelöscht");
    }

    public void SetActive(int id, bool isActive)
    {
        using var db = DbContextFactory.Create();

        var article = db.Articles.Find(id)
            ?? throw new Exception("Artikel wurde nicht gefunden.");

        article.IsActive = isActive;
        db.SaveChanges();

        _auditLog.Log(isActive ? "Artikel aktiviert" : "Artikel deaktiviert",
            $"Artikelnummer: {article.ArticleNumber}");
    }

    public void SetCurrentSerial(int articleId, int serial)
    {
        using var db = DbContextFactory.Create();

        var article = db.Articles
                        .FirstOrDefault(a => a.Id == articleId);

        if (article == null)
            throw new Exception("Artikel wurde nicht gefunden.");

        article.CurrentSerialNumber = serial;

        db.SaveChanges();
    }
}