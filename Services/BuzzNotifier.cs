namespace O11yParty.Services;

public sealed class BuzzNotifier : IBuzzNotifier
{
    // event keyword provides built-in thread-safe add/remove via lock on the delegate field.
    public event Action<BuzzNotification>? BuzzReceived;

    public void Publish(BuzzNotification notification)
    {
        // Capture the delegate reference atomically so a concurrent unsubscribe cannot
        // null it between the null-check and the invocation.
        BuzzReceived?.Invoke(notification);
    }
}
