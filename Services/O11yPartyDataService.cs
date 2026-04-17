using System.Text.Json;
using O11yParty.Models;

namespace O11yParty.Services;

public sealed class O11yPartyDataService
{
    private readonly IWebHostEnvironment _environment;
    private O11yPartyBoard? _cachedBoard;
    private IReadOnlyList<string>? _cachedTeams;

    public O11yPartyDataService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<O11yPartyBoard> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedBoard is not null)
        {
            return CloneBoard(_cachedBoard);
        }

        var boardPath = Path.Combine(_environment.WebRootPath, "data", "o11yparty-board.json");
        if (!File.Exists(boardPath))
        {
            _cachedBoard = BuildFallbackBoard();
            return CloneBoard(_cachedBoard);
        }

        await using var stream = File.OpenRead(boardPath);
        var board = await JsonSerializer.DeserializeAsync<O11yPartyBoard>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        _cachedBoard = board ?? BuildFallbackBoard();
        return CloneBoard(_cachedBoard);
    }

    public async Task<IReadOnlyList<string>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTeams is not null)
        {
            return _cachedTeams.ToList();
        }

        var teamsPath = Path.Combine(_environment.WebRootPath, "data", "teams.json");
        if (!File.Exists(teamsPath))
        {
            _cachedTeams = BuildFallbackTeams();
            return _cachedTeams.ToList();
        }

        await using var stream = File.OpenRead(teamsPath);
        var payload = await JsonSerializer.DeserializeAsync<TeamsPayload>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        _cachedTeams = payload?.Teams?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? BuildFallbackTeams();
        return _cachedTeams.ToList();
    }

    private static O11yPartyBoard CloneBoard(O11yPartyBoard board)
    {
        return new O11yPartyBoard
        {
            Categories = board.Categories
                .Select(category => new O11yPartyCategory
                {
                    Name = category.Name,
                    Questions = category.Questions
                        .Select(question => new O11yPartyQuestion
                        {
                            Value = question.Value,
                            Prompt = question.Prompt,
                            Answer = question.Answer,
                            IsDoubleDown = question.IsDoubleDown
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static O11yPartyBoard BuildFallbackBoard()
    {
        return new O11yPartyBoard
        {
            Categories = new List<O11yPartyCategory>
            {
                new()
                {
                    Name = "Cloud Fundamentals",
                    Questions = new List<O11yPartyQuestion>
                    {
                        new() { Value = 100, Prompt = "What is the shared responsibility model?", Answer = "A cloud model where providers secure the cloud and customers secure what they put in it." },
                        new() { Value = 200, Prompt = "Define IaaS.", Answer = "Infrastructure as a Service: virtualized compute, storage, and networking resources on demand." },
                        new() { Value = 300, Prompt = "Name a benefit of elasticity.", Answer = "The ability to scale resources up or down automatically based on demand." },
                        new() { Value = 400, Prompt = "What is a region?", Answer = "A geographic area containing one or more datacenters." },
                        new() { Value = 500, Prompt = "What is a landing zone?", Answer = "A foundational set of policies and resources for deploying workloads." }
                    }
                },
                new()
                {
                    Name = "Observability",
                    Questions = new List<O11yPartyQuestion>
                    {
                        new() { Value = 100, Prompt = "Define SLIs.", Answer = "Service Level Indicators are measurable signals of service performance." },
                        new() { Value = 200, Prompt = "What is distributed tracing?", Answer = "Tracking a request across services to understand latency and dependencies." },
                        new() { Value = 300, Prompt = "What is an alert?", Answer = "A notification triggered by a condition that needs attention." },
                        new() { Value = 400, Prompt = "What does MTTR stand for?", Answer = "Mean Time To Recovery." },
                        new() { Value = 500, Prompt = "Name the three pillars of observability.", Answer = "Logs, metrics, and traces." }
                    }
                },
                new()
                {
                    Name = "DevOps",
                    Questions = new List<O11yPartyQuestion>
                    {
                        new() { Value = 100, Prompt = "What is CI?", Answer = "Continuous Integration: merging and validating code frequently." },
                        new() { Value = 200, Prompt = "What is CD?", Answer = "Continuous Delivery/Deployment: releasing changes automatically." },
                        new() { Value = 300, Prompt = "What is infrastructure as code?", Answer = "Managing infrastructure with versioned code." },
                        new() { Value = 400, Prompt = "What is a blue/green deployment?", Answer = "A release strategy using two environments to reduce downtime." },
                        new() { Value = 500, Prompt = "What is trunk-based development?", Answer = "Working from a single shared branch with short-lived branches." }
                    }
                },
                new()
                {
                    Name = "Security",
                    Questions = new List<O11yPartyQuestion>
                    {
                        new() { Value = 100, Prompt = "Define least privilege.", Answer = "Grant only the minimum access needed to perform a task." },
                        new() { Value = 200, Prompt = "What is MFA?", Answer = "Multi-factor authentication." },
                        new() { Value = 300, Prompt = "What is a CVE?", Answer = "A Common Vulnerabilities and Exposures identifier." },
                        new() { Value = 400, Prompt = "What is zero trust?", Answer = "Never trust, always verify access to resources." },
                        new() { Value = 500, Prompt = "What is data encryption at rest?", Answer = "Protecting stored data using encryption keys." }
                    }
                },
                new()
                {
                    Name = "AI & Data",
                    Questions = new List<O11yPartyQuestion>
                    {
                        new() { Value = 100, Prompt = "What is a feature?", Answer = "An input variable used by a model." },
                        new() { Value = 200, Prompt = "What is model drift?", Answer = "When model performance degrades due to changing data patterns." },
                        new() { Value = 300, Prompt = "Define ETL.", Answer = "Extract, Transform, Load." },
                        new() { Value = 400, Prompt = "What is a vector database used for?", Answer = "Storing embeddings for similarity search." },
                        new() { Value = 500, Prompt = "What is a prompt?", Answer = "An input instruction to guide a model response." }
                    }
                }
            }
        };
    }

    private static IReadOnlyList<string> BuildFallbackTeams()
    {
        return new List<string> { "Team Azure", "Team DevRel" };
    }

    private sealed class TeamsPayload
    {
        public List<string>? Teams { get; set; }
    }
}
