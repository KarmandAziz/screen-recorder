using System.Text;

namespace LocalScreenRecorder.App.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly object _sync = new();

    public LoggingService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalScreenRecorder", "logs");
        Directory.CreateDirectory(folder);
        LogFilePath = Path.Combine(folder, $"recorder-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public string LogFilePath { get; }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_sync)
            {
                File.AppendAllText(LogFilePath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take down an active recording.
        }
    }
}
