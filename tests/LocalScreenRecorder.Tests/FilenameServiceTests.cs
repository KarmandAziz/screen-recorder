using LocalScreenRecorder.Core.Services;

namespace LocalScreenRecorder.Tests;

public sealed class FilenameServiceTests
{
    [Fact]
    public void CreatePaths_UsesRequiredFormatAndNeverOverwrites()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"screen-recorder-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var service = new FilenameService();
            var now = new DateTimeOffset(2026, 8, 5, 14, 7, 9, TimeSpan.Zero);
            var first = service.CreatePaths(folder, now);
            File.WriteAllText(first.FinalPath, "existing");
            var second = service.CreatePaths(folder, now);

            Assert.Equal("Recording_2026-08-05_14-07-09.mp4", Path.GetFileName(first.FinalPath));
            Assert.Equal("Recording_2026-08-05_14-07-09_2.mp4", Path.GetFileName(second.FinalPath));
            Assert.EndsWith(".partial.mp4", second.TemporaryPath);
            Assert.NotEqual(second.FinalPath, second.TemporaryPath);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
