using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace SerialManager.Services;

internal static class DbExceptionHelper
{
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException switch
        {
            MySqlException mysql => mysql.Number == 1062,
            SqliteException sqlite => sqlite.SqliteErrorCode == 19, // SQLITE_CONSTRAINT
            _ => false
        };
    }
}