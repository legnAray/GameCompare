using System.IO;
using Microsoft.Win32;

namespace GameCompare.App.Services;

public sealed class FolderPickerService
{
    public string? PickPlayniteDirectory(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 Playnite 数据目录",
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog(System.Windows.Application.Current.MainWindow) == true
            ? dialog.FolderName
            : null;
    }
}
