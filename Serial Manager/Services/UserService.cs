using BCrypt.Net;
using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class UserService
{
    private readonly AuditLogService _auditLog = new();

    public bool HasUsers()
    {
        using var db = DbContextFactory.Create();
        return db.Users.Any();
    }

    public void CreateUser(
        string username,
        string password,
        string fullName,
        string role = "User")
    {
        using var db = DbContextFactory.Create();

        if (db.Users.Any(u => u.Username == username))
            throw new Exception("Benutzer existiert bereits.");

        db.Users.Add(new User
        {
            Username = username.Trim(),
            FullName = fullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            IsActive = true,
            Created = DateTime.Now
        });

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx &&
                                    DbExceptionHelper.IsUniqueConstraintViolation(dbEx))
        {
            throw new Exception(
                "Dieser Benutzername existiert bereits " +
                "(wurde gerade eben von einem anderen Administrator angelegt).");
        }
    }

    public User? Authenticate(string username, string password)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u =>
            u.Username == username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        user.LastLogin = DateTime.Now;
        db.SaveChanges();

        return user;
    }

    public List<User> GetUsers()
    {
        using var db = DbContextFactory.Create();
        return db.Users.OrderBy(u => u.Username).ToList();
    }

    public void SetActive(int userId, bool isActive)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new Exception("Benutzer wurde nicht gefunden.");

        user.IsActive = isActive;
        db.SaveChanges();

        _auditLog.Log(isActive ? "Benutzer aktiviert" : "Benutzer deaktiviert",
            $"Benutzername: {user.Username}");
    }

    public void UpdateUser(int userId, string fullName, string role)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new Exception("Benutzer wurde nicht gefunden.");

        user.FullName = fullName.Trim();
        user.Role = role;

        db.SaveChanges();
    }

    public void ResetPassword(int userId, string newPassword)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new Exception("Benutzer wurde nicht gefunden.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        db.SaveChanges();

        _auditLog.Log("Passwort zurückgesetzt (Administrator)", $"Benutzername: {user.Username}");
    }

    public void ChangeOwnPassword(int userId, string currentPassword, string newPassword)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new Exception("Benutzer wurde nicht gefunden.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new Exception("Das aktuelle Passwort ist falsch.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        db.SaveChanges();

        _auditLog.Log("Passwort geändert (selbst)", $"Benutzername: {user.Username}");
    }

    public void DeleteUser(int userId)
    {
        using var db = DbContextFactory.Create();

        var user = db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new Exception("Benutzer wurde nicht gefunden.");

        if (user.Role == "Administrator" &&
            db.Users.Count(u => u.Role == "Administrator") <= 1)
            throw new Exception("Der letzte Administrator kann nicht gelöscht werden.");

        db.Users.Remove(user);
        db.SaveChanges();

        _auditLog.Log("Benutzer gelöscht", $"Benutzername: {user.Username}");
    }
}