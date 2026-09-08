using SerialManager.Data;
using SerialManager.Models;
using Microsoft.EntityFrameworkCore;

namespace SerialManager.Services;

public class SerialService
{
    private readonly AuditLogService _auditLog = new();

    public string GetNext(string articleNumber, string machine)
    {
        return CreateBatch(articleNumber, machine, 1).First();
    }

    public string GetCurrentSerial(string articleNumber)
    {
        using var db = DbContextFactory.Create();

        var article = db.Articles
                        .FirstOrDefault(a => a.ArticleNumber == articleNumber);

        if (article == null)
            return "0000";

        return article.CurrentSerialNumber.ToString("D4");
    }

    public List<SerialHistory> GetHistory(string articleNumber)
    {
        using var db = DbContextFactory.Create();

        return db.SerialHistories
                 .Where(h => h.ArticleNumber == articleNumber)
                 .OrderByDescending(h => h.Created)
                 .ToList();
    }

    public List<SerialHistory> GetHistory()
    {
        using var db = DbContextFactory.Create();

        return db.SerialHistories
                 .OrderByDescending(h => h.Created)
                 .ToList();
    }

    public SerialHistory? GetHistoryEntry(string serialNumber)
    {
        using var db = DbContextFactory.Create();

        return db.SerialHistories
                 .FirstOrDefault(h => h.SerialNumber == serialNumber);
    }

    public void DeleteHistoryEntry(int id)
    {
        using var db = DbContextFactory.Create();

        var entry = db.SerialHistories.Find(id)
            ?? throw new Exception("Der Eintrag wurde nicht gefunden.");

        db.SerialHistories.Remove(entry);
        db.SaveChanges();

        _auditLog.Log("Seriennummer gelöscht",
            $"Artikelnummer: {entry.ArticleNumber}, Seriennummer: {entry.SerialNumber}");
    }

    public bool SerialExists(string articleNumber, string serialNumber)
    {
        using var db = DbContextFactory.Create();

        return db.SerialHistories
                 .Any(h => h.ArticleNumber == articleNumber &&
                           h.SerialNumber == serialNumber);
    }

    public int GetTodayCount()
    {
        using var db = DbContextFactory.Create();

        return db.SerialHistories
                 .Count(h => h.Created.Date == DateTime.Today);
    }


    public List<string> CreateBatch(
    string articleNumber,
    string machine,
    int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Die Anzahl muss größer als 0 sein.");

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var db = DbContextFactory.Create();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var article = db.Articles
                    .SingleOrDefault(a => a.ArticleNumber == articleNumber);

                if (article == null)
                    throw new Exception("Artikel wurde nicht gefunden.");

                var serials = new List<string>();

                for (int i = 0; i < quantity; i++)
                {
                    article.CurrentSerialNumber++;

                    string serial = article.CurrentSerialNumber.ToString("D4");

                    // Seriennummern sind pro Artikel eindeutig.
                    // Damit darf z. B. Artikel A 0001 und Artikel B ebenfalls 0001 haben.
                    if (db.SerialHistories.Any(h =>
                        h.ArticleNumber == article.ArticleNumber &&
                        h.SerialNumber == serial))
                    {
                        throw new InvalidOperationException(
                            $"Die Seriennummer {serial} ist für den Artikel {article.ArticleNumber} bereits vorhanden.");
                    }

                    serials.Add(serial);

                    db.SerialHistories.Add(new SerialHistory
                    {
                        ArticleNumber = article.ArticleNumber,
                        SerialNumber = serial,
                        Machine = machine,
                        Created = DateTime.Now,
                        Operator = CurrentSession.CurrentUser?.FullName ?? Environment.UserName
                    });
                }

                db.SaveChanges();

                transaction.Commit();

                return serials;
            }
            catch (DbUpdateConcurrencyException)
            {
                transaction.Rollback();

                if (attempt == maxRetries)
                    throw new Exception("Die Seriennummer konnte wegen eines gleichzeitigen Zugriffs nicht erzeugt werden.");
            }
            catch (DbUpdateException ex)
            {
                transaction.Rollback();

                throw new InvalidOperationException(
                    "Die Seriennummer konnte nicht gespeichert werden. Bitte prüfen Sie die Datenbank und die Seriennummern-Einstellungen.",
                    ex);
            }
        }

        throw new Exception("Die Seriennummer konnte nicht erzeugt werden.");
    }
}