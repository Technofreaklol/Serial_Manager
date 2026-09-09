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

    public void SaveArticle(int? id, string articleNumber, string description)
    {
        using var db = DbContextFactory.Create();

        if (id.HasValue)
        {
            var article = db.Articles.Find(id.Value)
                ?? throw new Exception("Artikel wurde nicht gefunden.");

            if (db.Articles.Any(a => a.Id != id.Value && a.ArticleNumber == articleNumber))
                throw new Exception("Diese Artikelnummer wird bereits von einem anderen Artikel verwendet.");

            var oldArticleNumber = article.ArticleNumber;

            article.ArticleNumber = articleNumber;
            article.Description = description;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
            {
                throw new Exception(
                    "Diese Artikelnummer wird inzwischen von einem anderen Artikel verwendet " +
                    "(wurde gerade eben von einem anderen Benutzer vergeben).");
            }

            // Historie ist nur über die Artikelnummer verknüpft (keine
            // echte Fremdschlüssel-Beziehung) – bei Umbenennung mitziehen.
            if (oldArticleNumber != articleNumber)
            {
                var relatedHistory = db.SerialHistories
                    .Where(h => h.ArticleNumber == oldArticleNumber)
                    .ToList();

                foreach (var entry in relatedHistory)
                    entry.ArticleNumber = articleNumber;

                db.SaveChanges();
            }
        }
        else
        {
            if (db.Articles.Any(a => a.ArticleNumber == articleNumber))
                throw new Exception("Diese Artikelnummer existiert bereits.");

            db.Articles.Add(new Article
            {
                ArticleNumber = articleNumber,
                Description = description
            });

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
            {
                throw new Exception(
                    "Diese Artikelnummer existiert bereits " +
                    "(wurde gerade eben von einem anderen Benutzer angelegt).");
            }
        }
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