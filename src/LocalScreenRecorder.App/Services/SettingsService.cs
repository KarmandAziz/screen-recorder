using System.Text.Json;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.App.Services;

public sealed class SettingsService(SettingsSerializer serializer, ILoggingService logger) : ISettingsService
{
    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalScreenRecorder",
        "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            using var _ = JsonDocument.Parse(json);
            return serializer.DeserializeOrDefault(json);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.Warn($"Settings could not be loaded and defaults will be used: {exception.Message}");
            TryPreserveCorruptFile();
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporary = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(temporary, serializer.Serialize(settings), cancellationToken);
        File.Move(temporary, SettingsPath, true);
    }

    private void TryPreserveCorruptFile()
    {
        try
        {
            var backup = Path.Combine(Path.GetDirectoryName(SettingsPath)!, $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(SettingsPath, backup, true);
        }
        catch
        {
            // A locked or inaccessible settings file is harmless; defaults are already loaded.
        }
    }
}
