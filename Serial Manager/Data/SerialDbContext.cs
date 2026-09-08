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
    }
}