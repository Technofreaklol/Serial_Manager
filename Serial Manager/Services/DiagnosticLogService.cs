using System.IO;
using System.Text;

namespace SerialManager.Services;

public static class DiagnosticLogService
{
    private static readonly object Sync = new();

    public static string LogFolder
    {
        get
        {
            AppPaths.EnsureDirectories();
            var path = Path.Combine(AppPaths.Root, "Logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LogFile => Path.Combine(LogFolder, "SerialManager.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
            sb.AppendLine(message);
            if (exception != null)
            {
                sb.AppendLine(exception.ToString());
            }

            lock (Sync)
            {
                File.AppendAllText(LogFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging darf niemals selbst die Anwendung zum Absturz bringen.
        }
    }
}
