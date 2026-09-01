namespace GameCompare.Core.Models;

public sealed class SyncRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public int ImportedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int DeactivatedCount { get; set; }

    public int FailedCount { get; set; }

    public string? ErrorMessage { get; set; }

    public string SourcePath { get; set; } = string.Empty;
}
