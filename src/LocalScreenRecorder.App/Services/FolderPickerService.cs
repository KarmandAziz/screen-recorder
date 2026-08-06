namespace LocalScreenRecorder.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string currentFolder)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where MP4 recordings will be saved",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(currentFolder) ? currentFolder : string.Empty
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
