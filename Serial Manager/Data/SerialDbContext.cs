using Microsoft.EntityFrameworkCore;
using SerialManager.Models;

namespace SerialManager.Data;

public class SerialDbContext : DbContext
{
    public DbSet<SerialHistory> SerialHistories => Set<SerialHistory>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public SerialDbContext(DbContextOptions<SerialDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Einzigartige Artikelnummer
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.ArticleNumber)
            .IsUnique();

        // Einzigartiger Maschinenname
        modelBuilder.Entity<Machine>()
            .HasIndex(m => m.Name)
            .IsUnique();

        // Einzigartiger Einstellungs-Schlüssel
        modelBuilder.Entity<Setting>()
            .HasIndex(s => s.Key)
            .IsUnique();

        // Seriennummer je Artikel eindeutig
        modelBuilder.Entity<SerialHistory>()
            .HasIndex(h => new
            {
                h.ArticleNumber,
                h.SerialNumber
            })
            .IsUnique();

        modelBuilder.Entity<User>()
.HasIndex(u => u.Username)
.IsUnique();

        // Concurrency-Tokens: EF Core macht daraus nicht automatisch
        // einen Concurrency-Check nur weil die Property "RowVersion" heißt –
        // das muss explizit konfiguriert werden, sonst wird die Spalte
        // beim Speichern nur mitgeschrieben, aber nie auf Konflikte geprüft.
        modelBuilder.Entity<Article>()
            .Property(a => a.RowVersion)
            .IsConcurrencyToken();

        modelBuilder.Entity<Machine>()
            .Property(m => m.RowVersion)
            .IsConcurrencyToken();

        // MySQL erzeugt RowVersion serverseitig selbst (siehe DatabaseInitializer:
        // "timestamp(6) ... DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)"),
        // deshalb dort ValueGeneratedOnAddOrUpdate (EF liest den Wert nach dem
        // Speichern zurück, statt ihn selbst zu setzen).
        //
        // SQLite kennt so einen automatisch aktualisierten Zeitstempel NICHT.
        // Mit ValueGeneratedOnAddOrUpdate würde EF die Spalte beim INSERT/UPDATE
        // einfach weglassen und den in der Datenbank stehenden (nie gesetzten)
        // Wert für den Concurrency-Check verwenden – der weicht dann vom
        // ursprünglich geladenen Wert ab und jede erste Bearbeitung eines
        // Datensatzes wirkt wie "wurde von einem anderen Benutzer bearbeitet",
        // obwohl niemand sonst etwas geändert hat. Für SQLite setzt daher die
        // Anwendung selbst einen neuen Wert (siehe SaveChanges-Override unten).
        if (Database.IsSqlite())
        {
            modelBuilder.Entity<Article>()
                .Property(a => a.RowVersion)
                .ValueGeneratedNever();

            modelBuilder.Entity<Machine>()
                .Property(m => m.RowVersion)
                .ValueGeneratedNever();
        }
        else
        {
            modelBuilder.Entity<Article>()
                .Property(a => a.RowVersion)
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<Machine>()
                .Property(m => m.RowVersion)
                .ValueGeneratedOnAddOrUpdate();
        }
    }

    // Siehe Kommentar in OnModelCreating: Bei SQLite gibt es keine
    // serverseitige Generierung von RowVersion, deshalb setzt die Anwendung
    // vor jedem Speichern selbst einen neuen Zeitstempel auf alle
    // hinzugefügten/geänderten Artikel und Maschinen.
    private void ApplySqliteRowVersionIfNeeded()
    {
        if (!Database.IsSqlite())
            return;

        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Article>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.RowVersion = now;
        }

        foreach (var entry in ChangeTracker.Entries<Machine>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.RowVersion = now;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySqliteRowVersionIfNeeded();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override int SaveChanges()
    {
        ApplySqliteRowVersionIfNeeded();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplySqliteRowVersionIfNeeded();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySqliteRowVersionIfNeeded();
        return base.SaveChangesAsync(cancellationToken);
    }
}