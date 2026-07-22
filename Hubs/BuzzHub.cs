using Microsoft.AspNetCore.SignalR;
using O11yParty.Services;

namespace O11yParty.Hubs;

public sealed class BuzzHub : Hub
{
    private readonly IBuzzNotifier _notifier;
    private readonly string _sharedSecret;
    private readonly IHostEnvironment _env;
    private readonly ILogger<BuzzHub> _logger;

    public BuzzHub(IBuzzNotifier notifier, IConfiguration configuration, IHostEnvironment env, ILogger<BuzzHub> logger)
    {
        _notifier = notifier;
        _env = env;
        _logger = logger;
        _sharedSecret = configuration["BuzzHub:SharedSecret"] ?? string.Empty;
    }

    public override Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        // Try query-string first (SignalR's AccessTokenProvider sends it here for WebSocket),
        // then fall back to the Authorization: Bearer header.
        string? token = httpContext?.Request.Query["access_token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            var authHeader = httpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader is not null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(_sharedSecret))
        {
            // Fail open ONLY in Development; fail closed everywhere else so a misconfigured
            // production deploy is never an unauthenticated, publicly reachable hub.
            if (_env.IsDevelopment())
            {
                _logger.LogWarning("BuzzHub: SharedSecret not configured — accepting connection {ConnectionId} WITHOUT authentication (Development only).", Context.ConnectionId);
                return base.OnConnectedAsync();
            }

            _logger.LogError("BuzzHub: SharedSecret is not configured outside Development — rejecting connection {ConnectionId}. Set BuzzHub:SharedSecret.", Context.ConnectionId);
            Context.Abort();
            return Task.CompletedTask;
        }

        if (token != _sharedSecret)
        {
            _logger.LogWarning("BuzzHub: Connection {ConnectionId} rejected — invalid or missing shared secret.", Context.ConnectionId);
            Context.Abort();
            return Task.CompletedTask;
        }

        _logger.LogInformation("BuzzHub: Connection {ConnectionId} authenticated successfully.", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// Invoked by the buzzer client to push a buzz event into the game.
    /// </summary>
public Task Buzz(string teamName, long buzzedAtUtcMs)
{
    if (string.IsNullOrWhiteSpace(teamName))
    {
        _logger.LogWarning("BuzzHub: Received Buzz with empty teamName from connection {ConnectionId}; ignoring.", Context.ConnectionId);
        return Task.CompletedTask;
    }

    var serverBuzzedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var notification = new BuzzNotification(teamName.Trim(), serverBuzzedAtUtcMs);
    _logger.LogInformation(
        "BuzzHub: Buzz received — Team={TeamName}, ClientBuzzedAtUtcMs={ClientBuzzedAtUtcMs}, ServerBuzzedAtUtcMs={ServerBuzzedAtUtcMs}",
        notification.Name,
        buzzedAtUtcMs,
        serverBuzzedAtUtcMs);
    _notifier.Publish(notification);
    return Task.CompletedTask;
}
    }
}
