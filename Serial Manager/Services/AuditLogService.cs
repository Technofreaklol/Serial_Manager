using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class AuditLogService
{
    public void Log(string action, string details = "")
    {
        try
        {
            using var db = DbContextFactory.Create();

            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Created = DateTime.Now,
                Username = CurrentSession.CurrentUser?.Username ?? "Unbekannt",
                Action = action,
                Details = details
            });

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagenes Audit-Log darf die eigentliche Aktion nicht verhindern.
            DiagnosticLogService.Write("Audit-Log konnte nicht geschrieben werden.", ex);
        }
    }

    public List<AuditLogEntry> GetEntries()
    {
        using var db = DbContextFactory.Create();

        return db.AuditLogEntries
                 .OrderByDescending(a => a.Created)
                 .ToList();
    }
}