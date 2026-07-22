namespace O11yParty.Services;

// BuzzedAtUtcMs is the server-received time, which is what buzz arbitration uses —
// a client-supplied timestamp would let a misbehaving buzzer spoof an earlier time and
// always win. ClientBuzzedAtUtcMs is kept only for latency diagnostics.
public sealed record BuzzNotification(string Name, long BuzzedAtUtcMs, long ClientBuzzedAtUtcMs);

public interface IBuzzNotifier
{
    event Action<BuzzNotification>? BuzzReceived;
    void Publish(BuzzNotification notification);
}
