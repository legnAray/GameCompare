using System.IO;
using System.Text.Json;
using GameCompare.Core.Services;
using GameCompare.Persistence;
using GameCompare.Playnite;

namespace GameCompare.App.Services;

public sealed record AppSettings
{
    public string PlaynitePath { get; init; } = string.Empty;

    public string ObsidianVaultPath { get; init; } = string.Empty;

    public string ObsidianNotesDirectory { get; init; } = ObsidianNoteFileService.DefaultNotesDirectory;
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(AppDataPaths.SettingsPath))
            {
                return CreateDefault();
            }

            await using var stream = File.OpenRead(AppDataPaths.SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                JsonOptions,
                cancellationToken);
            return Normalize(settings ?? CreateDefault());
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(AppDataPaths.RootDirectory);
            await using var stream = File.Create(AppDataPaths.SettingsPath);
            await JsonSerializer.SerializeAsync(
                stream,
                Normalize(settings),
                JsonOptions,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static AppSettings CreateDefault() => new()
    {
        PlaynitePath = PlaynitePathDetector.DetectDefaultPath() ?? string.Empty
    };

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        PlaynitePath = settings.PlaynitePath ?? string.Empty,
        ObsidianVaultPath = settings.ObsidianVaultPath ?? string.Empty,
        ObsidianNotesDirectory = string.IsNullOrWhiteSpace(settings.ObsidianNotesDirectory)
            ? ObsidianNoteFileService.DefaultNotesDirectory
            : settings.ObsidianNotesDirectory
    };
}
