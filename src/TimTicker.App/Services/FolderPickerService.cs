using System.IO;
using Microsoft.Win32;

namespace TimTicker.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string initialDirectory)
    {
        OpenFolderDialog dialog = new()
        {
            Title = "选择 TimTicker 本地存储位置",
            Multiselect = false,
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
