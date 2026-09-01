namespace GameCompare.Core.Models;

public sealed record ImportedGame(
    Guid PlayniteId,
    string Name,
    string? SortingName,
    DateOnly? ReleaseDate,
    string Developers,
    string Publishers,
    string Platforms,
    string Genres,
    string? Source,
    string? CoverPath,
    ulong PlaytimeSeconds,
    DateTimeOffset? LastActivityAt,
    int? PlayniteUserScore);

public sealed record PlayniteImportBatch(
    string SourcePath,
    string LibraryPath,
    IReadOnlyList<ImportedGame> Games,
    DateTimeOffset ImportedAt);
