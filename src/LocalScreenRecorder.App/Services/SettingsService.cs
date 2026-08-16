using System.Text.Json;
using LocalScreenRecorder.Core.Models;
using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.App.Services;

public sealed class SettingsService(SettingsSerializer serializer, ILoggingService logger) : ISettingsService
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);

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
        await _saveGate.WaitAsync(cancellationToken);
        string? temporary = null;
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporary, serializer.Serialize(settings), cancellationToken);
            File.Move(temporary, SettingsPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Error("Settings could not be saved.", exception);
            throw new IOException("Settings could not be saved. Check access to the Local AppData folder.", exception);
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); } catch { }
            }
            _saveGate.Release();
        }
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
