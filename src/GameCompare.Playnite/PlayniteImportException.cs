namespace GameCompare.Playnite;

public sealed class PlayniteImportException : Exception
{
    public PlayniteImportException(string message)
        : base(message)
    {
    }

    public PlayniteImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
