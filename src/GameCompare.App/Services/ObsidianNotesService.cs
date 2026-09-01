using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;
using GameCompare.Core.Services;

namespace GameCompare.App.Services;

public sealed class ObsidianNotesService(
    ObsidianNoteFileService noteFiles,
    IGameRepository repository,
    AppSettingsService settingsService,
    ObsidianUriLauncher launcher)
{
    public async Task<ObsidianNoteFileResult> OpenOrCreateAsync(
        GameRecord game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        var settings = await settingsService.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ObsidianVaultPath))
        {
            throw new InvalidOperationException("请先到设置页选择 Obsidian Vault，然后再写玩后感。");
        }

        var result = await noteFiles.EnsureNoteAsync(
            game,
            settings.ObsidianVaultPath,
            settings.ObsidianNotesDirectory,
            cancellationToken);

        if (!string.Equals(game.NotePath, result.RelativePath, StringComparison.Ordinal))
        {
            await repository.SetGameNotePathAsync(game.Id, result.RelativePath, cancellationToken);
            game.NotePath = result.RelativePath;
        }

        try
        {
            launcher.OpenPath(result.FullPath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"笔记已保存在“{result.FullPath}”，但没有成功打开。{exception.Message}",
                exception);
        }

        return result;
    }
}
