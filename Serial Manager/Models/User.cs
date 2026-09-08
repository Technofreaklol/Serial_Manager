namespace SerialManager.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;

    public DateTime Created { get; set; } = DateTime.Now;

    public DateTime? LastLogin { get; set; }
}