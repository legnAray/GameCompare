using System.Diagnostics;
using System.Globalization;
using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;
using LiteDB;

namespace GameCompare.Playnite;

public sealed class PlayniteImporter : IPlayniteImporter
{
    private static readonly (string FileName, string CollectionName)[] LookupDatabases =
    [
        ("companies.db", "Company"),
        ("platforms.db", "Platform"),
        ("genres.db", "Genre"),
        ("sources.db", "GameSource")
    ];

    private static readonly string[] PlayniteProcessNames =
    [
        "Playnite",
        "Playnite.DesktopApp",
        "Playnite.FullscreenApp"
    ];

    public Task<PlayniteImportBatch> ImportCompletedGamesAsync(
        string playnitePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ImportCore(playnitePath, cancellationToken), cancellationToken);

    private static PlayniteImportBatch ImportCore(
        string playnitePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playnitePath))
        {
            throw new PlayniteImportException("请先在设置中选择 Playnite 数据目录。");
        }

        var sourceRoot = Path.GetFullPath(playnitePath.Trim());
        if (!PlaynitePathDetector.IsPlayniteDataDirectory(sourceRoot))
        {
            throw new PlayniteImportException(
                $"没有在该目录找到完整的 Playnite 数据库：{sourceRoot}");
        }

        EnsurePlayniteIsClosed();

        var sourceLibrary = Path.Combine(sourceRoot, "library");
        var snapshotRoot = Path.Combine(
            Path.GetTempPath(),
            "GameCompare",
            "PlayniteSnapshot",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(snapshotRoot);
            CopyDatabase(sourceLibrary, snapshotRoot, "games.db");
            CopyDatabase(sourceLibrary, snapshotRoot, "completionstatuses.db");
            foreach (var (fileName, _) in LookupDatabases)
            {
                CopyDatabase(sourceLibrary, snapshotRoot, fileName);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var completedStatusId = ReadCompletedStatusId(snapshotRoot);
            var companies = ReadNameLookup(snapshotRoot, "companies.db", "Company");
            var platforms = ReadNameLookup(snapshotRoot, "platforms.db", "Platform");
            var genres = ReadNameLookup(snapshotRoot, "genres.db", "Genre");
            var sources = ReadNameLookup(snapshotRoot, "sources.db", "GameSource");
            var games = ReadCompletedGames(
                snapshotRoot,
                sourceLibrary,
                completedStatusId,
                companies,
                platforms,
                genres,
                sources,
                cancellationToken);
            var importedAt = DateTimeOffset.UtcNow;

            return new PlayniteImportBatch(sourceRoot, sourceLibrary, games, importedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PlayniteImportException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PlayniteImportException(
                "读取 Playnite 数据失败。请确认 Playnite 已完全关闭，且数据库没有损坏。",
                exception);
        }
        finally
        {
            TryDeleteSnapshot(snapshotRoot);
        }
    }

    private static void EnsurePlayniteIsClosed()
    {
        foreach (var processName in PlayniteProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            try
            {
                if (processes.Any(process => !process.HasExited))
                {
                    throw new PlayniteImportException(
                        "检测到 Playnite 仍在运行。请先完全关闭桌面模式和全屏模式，再点击同步。");
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }

    private static void CopyDatabase(string sourceLibrary, string snapshotRoot, string fileName)
    {
        var sourcePath = Path.Combine(sourceLibrary, fileName);
        if (!File.Exists(sourcePath))
        {
            throw new PlayniteImportException($"Playnite 数据库缺少文件：{fileName}");
        }

        File.Copy(sourcePath, Path.Combine(snapshotRoot, fileName), overwrite: false);
    }

    private static Guid ReadCompletedStatusId(string snapshotRoot)
    {
        using var database = OpenSnapshotDatabase(snapshotRoot, "completionstatuses.db");
        var completed = database.GetCollection("CompletionStatus")
            .FindAll()
            .FirstOrDefault(document => string.Equals(
                ReadString(document, "Name"),
                "Completed",
                StringComparison.OrdinalIgnoreCase));

        if (completed is null || !TryReadGuid(completed, "_id", out var statusId))
        {
            throw new PlayniteImportException(
                "Playnite 的完成状态中没有找到名为 “Completed” 的状态。");
        }

        return statusId;
    }

    private static Dictionary<Guid, string> ReadNameLookup(
        string snapshotRoot,
        string fileName,
        string collectionName)
    {
        using var database = OpenSnapshotDatabase(snapshotRoot, fileName);
        var result = new Dictionary<Guid, string>();
        foreach (var document in database.GetCollection(collectionName).FindAll())
        {
            if (TryReadGuid(document, "_id", out var id) &&
                ReadString(document, "Name") is { Length: > 0 } name)
            {
                result[id] = name;
            }
        }

        return result;
    }

    private static IReadOnlyList<ImportedGame> ReadCompletedGames(
        string snapshotRoot,
        string sourceLibrary,
        Guid completedStatusId,
        IReadOnlyDictionary<Guid, string> companies,
        IReadOnlyDictionary<Guid, string> platforms,
        IReadOnlyDictionary<Guid, string> genres,
        IReadOnlyDictionary<Guid, string> sources,
        CancellationToken cancellationToken)
    {
        using var database = OpenSnapshotDatabase(snapshotRoot, "games.db");
        var result = new List<ImportedGame>();

        foreach (var document in database.GetCollection("Game").FindAll())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadGuid(document, "CompletionStatusId", out var statusId) ||
                statusId != completedStatusId ||
                !TryReadGuid(document, "_id", out var gameId))
            {
                continue;
            }

            var name = ReadString(document, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "未命名游戏";
            }

            string? source = null;
            if (TryReadGuid(document, "SourceId", out var sourceId))
            {
                sources.TryGetValue(sourceId, out source);
            }

            result.Add(new ImportedGame(
                gameId,
                name,
                ReadString(document, "SortingName"),
                ReadReleaseDate(document),
                JoinNames(document, "DeveloperIds", companies),
                JoinNames(document, "PublisherIds", companies),
                JoinNames(document, "PlatformIds", platforms),
                JoinNames(document, "GenreIds", genres),
                source,
                ResolveCoverPath(sourceLibrary, ReadString(document, "CoverImage")),
                ReadUnsignedLong(document, "Playtime"),
                ReadDateTimeOffset(document, "LastActivity"),
                ReadNullableInt(document, "UserScore")));
        }

        return result
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static LiteDatabase OpenSnapshotDatabase(string snapshotRoot, string fileName) =>
        new($"Filename={Path.Combine(snapshotRoot, fileName)};Mode=Exclusive;Cache Size=0");

    private static string JoinNames(
        BsonDocument document,
        string fieldName,
        IReadOnlyDictionary<Guid, string> lookup)
    {
        var value = document[fieldName];
        if (!value.IsArray)
        {
            return string.Empty;
        }

        return string.Join(", ", value.AsArray
            .Where(item => item.IsGuid && lookup.ContainsKey(item.AsGuid))
            .Select(item => lookup[item.AsGuid])
            .Distinct(StringComparer.CurrentCultureIgnoreCase));
    }

    private static DateOnly? ReadReleaseDate(BsonDocument document)
    {
        var raw = ReadString(document, "ReleaseDate");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string? ResolveCoverPath(string sourceLibrary, string? coverValue)
    {
        if (string.IsNullOrWhiteSpace(coverValue))
        {
            return null;
        }

        if (Uri.TryCreate(coverValue, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return coverValue;
        }

        if (Path.IsPathRooted(coverValue))
        {
            return File.Exists(coverValue) ? Path.GetFullPath(coverValue) : null;
        }

        var filesRoot = Path.GetFullPath(Path.Combine(sourceLibrary, "files"));
        var candidate = Path.GetFullPath(Path.Combine(filesRoot, coverValue));
        var containedPrefix = filesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(containedPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
            ? candidate
            : null;
    }

    private static string? ReadString(BsonDocument document, string fieldName)
    {
        var value = document[fieldName];
        return value.IsString ? value.AsString : null;
    }

    private static bool TryReadGuid(BsonDocument document, string fieldName, out Guid value)
    {
        var bsonValue = document[fieldName];
        if (bsonValue.IsGuid)
        {
            value = bsonValue.AsGuid;
            return true;
        }

        value = Guid.Empty;
        return false;
    }

    private static ulong ReadUnsignedLong(BsonDocument document, string fieldName)
    {
        var value = document[fieldName];
        if (value.IsInt64 && value.AsInt64 > 0)
        {
            return (ulong)value.AsInt64;
        }

        if (value.IsInt32 && value.AsInt32 > 0)
        {
            return (ulong)value.AsInt32;
        }

        return 0;
    }

    private static int? ReadNullableInt(BsonDocument document, string fieldName)
    {
        var value = document[fieldName];
        if (value.IsInt32)
        {
            return value.AsInt32;
        }

        if (value.IsInt64 && value.AsInt64 is >= int.MinValue and <= int.MaxValue)
        {
            return (int)value.AsInt64;
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(BsonDocument document, string fieldName)
    {
        var value = document[fieldName];
        if (!value.IsDateTime)
        {
            return null;
        }

        var dateTime = value.AsDateTime;
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        return new DateTimeOffset(dateTime).ToUniversalTime();
    }

    private static void TryDeleteSnapshot(string snapshotRoot)
    {
        try
        {
            var expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "GameCompare",
                "PlayniteSnapshot"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(snapshotRoot);
            if (candidate.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(candidate))
            {
                Directory.Delete(candidate, recursive: true);
            }
        }
        catch
        {
            // A stale temp snapshot is harmless; the next OS cleanup can remove it.
        }
    }
}
