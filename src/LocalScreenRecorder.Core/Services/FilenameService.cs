using LocalScreenRecorder.Core.Models;

namespace LocalScreenRecorder.Core.Services;

public sealed class FilenameService
{
    public RecordingPaths CreatePaths(string outputFolder, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        Directory.CreateDirectory(outputFolder);

        var stem = $"Recording_{now:yyyy-MM-dd_HH-mm-ss}";
        var suffix = 1;
        string finalPath;
        do
        {
            finalPath = Path.Combine(outputFolder, suffix == 1 ? $"{stem}.mp4" : $"{stem}_{suffix}.mp4");
            suffix++;
        }
        while (File.Exists(finalPath));

        var temporaryPath = Path.Combine(outputFolder, $".{Path.GetFileNameWithoutExtension(finalPath)}.{Guid.NewGuid():N}.partial.mp4");
        return new RecordingPaths(finalPath, temporaryPath);
    }
}
