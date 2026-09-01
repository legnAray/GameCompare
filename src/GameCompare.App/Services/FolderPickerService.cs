using System.IO;
using Microsoft.Win32;

namespace GameCompare.App.Services;

public sealed class FolderPickerService
{
    public string? PickPlayniteDirectory(string? initialDirectory)
        => PickDirectory("选择 Playnite 数据目录", initialDirectory);

    public string? PickObsidianVaultDirectory(string? initialDirectory)
        => PickDirectory("选择 Obsidian Vault 根目录", initialDirectory);

    private static string? PickDirectory(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
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
