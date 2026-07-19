namespace O11yParty.Services;

public sealed record BuzzNotification(string Name, long BuzzedAtUtcMs);

public interface IBuzzNotifier
{
    event Action<BuzzNotification>? BuzzReceived;
    void Publish(BuzzNotification notification);
}
