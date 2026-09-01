namespace GameCompare.Core.Models;

public sealed record SyncResult(
    int ImportedCount,
    int UpdatedCount,
    int DeactivatedCount,
    int FailedCount,
    DateTimeOffset FinishedAt,
    string? ErrorMessage = null)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(ErrorMessage);
}
