using System.IO;
using System.Text.Json;
using GameCompare.Persistence;
using GameCompare.Playnite;

namespace GameCompare.App.Services;

public sealed record AppSettings(string PlaynitePath);

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
            return await JsonSerializer.DeserializeAsync<AppSettings>(
                       stream,
                       JsonOptions,
                       cancellationToken) ?? CreateDefault();
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
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static AppSettings CreateDefault() =>
        new(PlaynitePathDetector.DetectDefaultPath() ?? string.Empty);
}
