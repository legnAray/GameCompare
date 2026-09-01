using System.Text;
using GameCompare.Core.Models;

namespace GameCompare.Core.Services;

public sealed record ObsidianNoteFileResult(
    string FullPath,
    string RelativePath,
    bool Created);

public sealed class ObsidianNoteFileService
{
    public const string DefaultNotesDirectory = "GameCompare/Games";

    private const int MaximumTitleLength = 80;

    public async Task<ObsidianNoteFileResult> EnsureNoteAsync(
        GameRecord game,
        string vaultPath,
        string notesDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        var vaultRoot = ResolveVaultRoot(vaultPath);
        var notesRoot = ResolveNotesRoot(vaultRoot, notesDirectory);
        Directory.CreateDirectory(notesRoot);

        var linkedPath = ResolveLinkedNotePath(vaultRoot, game.NotePath);
        if (linkedPath is not null && File.Exists(linkedPath))
        {
            return Result(vaultRoot, linkedPath, false);
        }

        var discoveredPath = await FindNoteByPlayniteIdAsync(
            notesRoot,
            game.PlayniteId,
            cancellationToken);
        if (discoveredPath is not null)
        {
            return Result(vaultRoot, discoveredPath, false);
        }

        var idPrefix = game.PlayniteId.ToString("N")[..8];
        var fileStem = $"{SanitizeFileName(game.Name)} [{idPrefix}]";
        var markdown = BuildMarkdown(game);

        for (var suffix = 1; suffix <= 999; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var suffixText = suffix == 1 ? string.Empty : $" ({suffix})";
            var fullPath = Path.Combine(notesRoot, $"{fileStem}{suffixText}.md");

            if (File.Exists(fullPath))
            {
                if (await IsNoteForGameAsync(fullPath, game.PlayniteId, cancellationToken))
                {
                    return Result(vaultRoot, fullPath, false);
                }

                continue;
            }

            try
            {
                await using var stream = new FileStream(
                    fullPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(markdown.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                return Result(vaultRoot, fullPath, true);
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                // Another process created the same name between our check and CreateNew.
            }
        }

        throw new IOException("无法为该游戏找到可用的 Markdown 文件名。");
    }

    public string ResolveNotesRoot(string vaultPath, string notesDirectory)
    {
        var vaultRoot = ResolveVaultRoot(vaultPath);
        if (string.IsNullOrWhiteSpace(notesDirectory))
        {
            throw new ArgumentException("请填写 Vault 内的游戏笔记子目录。", nameof(notesDirectory));
        }

        var trimmedDirectory = notesDirectory.Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(trimmedDirectory) || Path.IsPathRooted(trimmedDirectory))
        {
            throw new ArgumentException("游戏笔记目录必须是 Vault 内的相对路径。", nameof(notesDirectory));
        }

        var notesRoot = Path.GetFullPath(Path.Combine(vaultRoot, trimmedDirectory));
        if (!IsInsideDirectory(vaultRoot, notesRoot) || PathsEqual(vaultRoot, notesRoot))
        {
            throw new ArgumentException("游戏笔记目录不能离开 Vault，也不能直接使用 Vault 根目录。", nameof(notesDirectory));
        }

        return notesRoot;
    }

    private static string ResolveVaultRoot(string vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
        {
            throw new ArgumentException("请先在设置中选择 Obsidian Vault。", nameof(vaultPath));
        }

        var vaultRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultPath.Trim()));
        if (!Directory.Exists(vaultRoot))
        {
            throw new DirectoryNotFoundException("选择的 Obsidian Vault 不存在，请在设置中重新选择。");
        }

        return vaultRoot;
    }

    private static string? ResolveLinkedNotePath(string vaultRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        var platformPath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(vaultRoot, platformPath));
        return IsInsideDirectory(vaultRoot, fullPath) ? fullPath : null;
    }

    private static async Task<string?> FindNoteByPlayniteIdAsync(
        string notesRoot,
        Guid playniteId,
        CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(notesRoot, "*.md", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsNoteForGameAsync(path, playniteId, cancellationToken))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static async Task<bool> IsNoteForGameAsync(
        string path,
        Guid playniteId,
        CancellationToken cancellationToken)
    {
        var marker = $"playnite_id: \"{playniteId:D}\"";
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
            for (var lineNumber = 0; lineNumber < 80; lineNumber++)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null || lineNumber > 0 && line == "---")
                {
                    return false;
                }

                if (string.Equals(line.Trim(), marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    private static ObsidianNoteFileResult Result(string vaultRoot, string fullPath, bool created)
    {
        var canonicalPath = Path.GetFullPath(fullPath);
        var relativePath = Path.GetRelativePath(vaultRoot, canonicalPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return new ObsidianNoteFileResult(canonicalPath, relativePath, created);
    }

    private static string BuildMarkdown(GameRecord game)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        AppendProperty(builder, "gamecompare_id", game.Id.ToString("D"));
        AppendProperty(builder, "playnite_id", game.PlayniteId.ToString("D"));
        AppendProperty(builder, "title", game.Name);
        if (game.ReleaseDate is not null)
        {
            AppendProperty(builder, "release_date", game.ReleaseDate.Value.ToString("yyyy-MM-dd"));
        }

        AppendOptionalProperty(builder, "developers", game.Developers);
        AppendOptionalProperty(builder, "publishers", game.Publishers);
        AppendOptionalProperty(builder, "platforms", game.Platforms);
        AppendOptionalProperty(builder, "genres", game.Genres);
        AppendOptionalProperty(builder, "source", game.Source);
        builder.AppendLine("tags:");
        builder.AppendLine("  - game/completed");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.Append("# ").AppendLine(NormalizeHeading(game.Name));
        builder.AppendLine();
        builder.AppendLine("## 一句话评价");
        builder.AppendLine();
        builder.AppendLine("## 玩后感");
        builder.AppendLine();
        builder.AppendLine("## 最喜欢的部分");
        builder.AppendLine();
        builder.AppendLine("## 不足与遗憾");
        return builder.ToString();
    }

    private static void AppendOptionalProperty(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            AppendProperty(builder, name, value.Trim());
        }
    }

    private static void AppendProperty(StringBuilder builder, string name, string value) =>
        builder.Append(name).Append(": \"").Append(EscapeYaml(value)).AppendLine("\"");

    private static string EscapeYaml(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string NormalizeHeading(string value)
    {
        var normalized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "未命名游戏" : normalized;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var cleanedCharacters = value.Select(character =>
            invalidCharacters.Contains(character) || character is '#' or '^' || char.IsControl(character)
                ? ' '
                : character).ToArray();
        var cleaned = string.Join(
            ' ',
            new string(cleanedCharacters).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .TrimEnd('.');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "未命名游戏";
        }

        if (cleaned.Length > MaximumTitleLength)
        {
            cleaned = cleaned[..MaximumTitleLength].TrimEnd();
            if (char.IsHighSurrogate(cleaned[^1]))
            {
                cleaned = cleaned[..^1];
            }
        }

        return cleaned;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsInsideDirectory(string root, string candidate)
    {
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
