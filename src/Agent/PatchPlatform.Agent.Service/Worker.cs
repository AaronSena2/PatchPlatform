using Microsoft.Extensions.Options;
using PatchPlatform.Agent.Core;

namespace PatchPlatform.Agent.Service;

public class AgentOptions
{
    public string ServerBaseUrl { get; set; } = "http://localhost:5000";
    public string StatePath { get; set; } = "state.json";
    public int HeartbeatIntervalMinutes { get; set; } = 60;
}

public class Worker : BackgroundService
{
    private readonly AgentRuntime _runtime;
    private readonly AgentOptions _options;
    private readonly ILogger<Worker> _logger;

    public Worker(AgentRuntime runtime, IOptions<AgentOptions> options, ILogger<Worker> logger)
    {
        _runtime = runtime;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PatchPlatform Agent Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _runtime.RunCheckInAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during agent check-in");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.HeartbeatIntervalMinutes), stoppingToken);
        }
    }
}
