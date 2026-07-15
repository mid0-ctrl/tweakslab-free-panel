using System.IO;
using System.Text;

namespace TweaksLabPanel.Core;

public static class DiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TweaksLabPanel",
        "logs");

    public static string CurrentLogPath { get; private set; } = string.Empty;

    public static string LogDirectoryPath => LogDirectory;

    public static void Write(string category, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory, $"appearance-{DateTime.Now:yyyy-MM-dd}.log");
                CurrentLogPath = path;
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ne pas bloquer l'app si l'écriture échoue.
            }
        }

        System.Diagnostics.Debug.WriteLine(line);
    }

    public static void WriteException(string category, Exception exception, string? context = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(context))
            builder.Append(context).Append(" | ");

        builder.Append(exception.GetType().Name).Append(": ").Append(exception.Message);
        if (exception.StackTrace is not null)
            builder.Append(" | ").Append(exception.StackTrace.Replace('\n', ' ').Replace('\r', ' '));

        Write(category, builder.ToString());
    }
}
