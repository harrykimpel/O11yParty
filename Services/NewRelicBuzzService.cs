using System.Text;
using System.Text.Json;

namespace O11yParty.Services;

public sealed class NewRelicBuzzService
{
    private const string GraphQlEndpoint = "https://api.newrelic.com/graphql";
    private const string GraphQlQuery = "query($accountId:Int!, $nrql:Nrql!) { actor { account(id: $accountId) { nrql(query: $nrql) { results } } } }";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NewRelicBuzzService> _logger;
    private readonly string _apiKey;
    private readonly int _accountId;
    private readonly string _eventType;
    private readonly string _nameAttribute;

    public NewRelicBuzzService(HttpClient httpClient, IConfiguration configuration, ILogger<NewRelicBuzzService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = (configuration["NewRelic:UserApiKey"] ?? string.Empty).Trim();
        _accountId = int.TryParse(configuration["NewRelic:AccountId"], out var accountId) ? accountId : 0;
        _eventType = SanitizeIdentifier(configuration["NewRelic:BuzzEventType"], "O11yPartyBuzz");
        _nameAttribute = SanitizeIdentifier(configuration["NewRelic:BuzzNameAttribute"], "teamName");
    }

    public bool IsConfigured => _accountId > 0 && !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<BuzzEvent?> GetLatestBuzzAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var nrql = $"FROM {_eventType} SELECT latest({_nameAttribute}) AS buzzedName, latest(timestamp) AS buzzedTimestamp SINCE 15 MINUTES AGO";
        var payload = new
        {
            query = GraphQlQuery,
            variables = new
            {
                accountId = _accountId,
                nrql
            }
        };

        _logger.LogDebug("Polling New Relic NerdGraph for latest buzz event.");

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("API-Key", _apiKey);

        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("New Relic poll failed with status code {StatusCode}. Response: {ResponseBody}", response.StatusCode, errorBody);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                //_logger.LogInformation("New Relic poll succeeded. Parsing results: {Results}", document.RootElement);

                if (!TryGetResults(document.RootElement, out var results) || results.GetArrayLength() == 0)
                {
                    return null;
                }

                var latestResult = results[0];
                if (!latestResult.TryGetProperty("buzzedName", out var nameElement))
                {
                    return null;
                }

                var buzzedName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(buzzedName))
                {
                    return null;
                }

                if (!latestResult.TryGetProperty("buzzedTimestamp", out var timestampElement))
                {
                    return null;
                }

                var timestampUnixMs = ParseUnixMs(timestampElement);
                if (timestampUnixMs <= 0)
                {
                    return null;
                }
                _logger.LogInformation("Latest buzz event: Name={BuzzedName}, Timestamp={Timestamp}", buzzedName, timestampUnixMs);
                return new BuzzEvent(buzzedName.Trim(), timestampUnixMs);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException exception) when (attempt < maxAttempts)
            {
                _logger.LogWarning(exception, "Transient New Relic poll failure on attempt {Attempt}. Retrying once.", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "New Relic poll failed.");
                return null;
            }
        }

        return null;
    }

    private static bool TryGetResults(JsonElement root, out JsonElement results)
    {
        results = default;

        if (!root.TryGetProperty("data", out var data))
        {
            return false;
        }

        if (!data.TryGetProperty("actor", out var actor))
        {
            return false;
        }

        if (!actor.TryGetProperty("account", out var account))
        {
            return false;
        }

        if (!account.TryGetProperty("nrql", out var nrql))
        {
            return false;
        }

        if (!nrql.TryGetProperty("results", out results))
        {
            return false;
        }

        return results.ValueKind == JsonValueKind.Array;
    }

    private static long ParseUnixMs(JsonElement timestampElement)
    {
        return timestampElement.ValueKind switch
        {
            JsonValueKind.Number when timestampElement.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when timestampElement.TryGetDouble(out var doubleValue) => (long)doubleValue,
            JsonValueKind.String when long.TryParse(timestampElement.GetString(), out var parsedString) => parsedString,
            _ => 0
        };
    }

    private static string SanitizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var sanitized = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    public sealed record BuzzEvent(string Name, long TimestampUnixMs);
}
