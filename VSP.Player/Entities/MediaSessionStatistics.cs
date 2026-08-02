namespace VSP.Player.Entities;

public sealed class MediaSessionStatistics
{
    public DateTimeOffset? ConnectedSince { get; init; }

    public int ReconnectAttempts { get; init; }

    public DateTimeOffset? LastErrorAt { get; init; }

    public MediaError? LastError { get; init; }

    public TimeSpan TotalConnectedDuration { get; init; }

    public static MediaSessionStatistics Empty { get; } = new();
}
