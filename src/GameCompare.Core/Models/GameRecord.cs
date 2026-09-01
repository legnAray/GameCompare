namespace GameCompare.Core.Models;

public sealed class GameRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlayniteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? SortingName { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public string Developers { get; set; } = string.Empty;

    public string Publishers { get; set; } = string.Empty;

    public string Platforms { get; set; } = string.Empty;

    public string Genres { get; set; } = string.Empty;

    public string? Source { get; set; }

    public string? CoverPath { get; set; }

    public ulong PlaytimeSeconds { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    public int? PlayniteUserScore { get; set; }

    public bool IsActiveCompleted { get; set; } = true;

    public DateTimeOffset LastSyncedAt { get; set; }

    public string? NotePath { get; set; }
}
