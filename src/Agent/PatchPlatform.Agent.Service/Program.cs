using PatchPlatform.Agent.Core;
using PatchPlatform.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

var agentOptions = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();

var enrollMode = args.Contains("--enroll");
var server = args.SkipWhile(a => a != "--server").Skip(1).FirstOrDefault() ?? agentOptions.ServerBaseUrl;

builder.Services.AddSingleton<IAgentStateStore>(
    new FileAgentStateStore(agentOptions.StatePath));

builder.Services.AddHttpClient<IServerClient, ServerClient>(client =>
{
    client.BaseAddress = new Uri(server.TrimEnd('/') + "/");
});

builder.Services.AddScoped<IInventoryScanner, StubInventoryScanner>();
builder.Services.AddScoped<AgentRuntime>();

builder.Services.AddWindowsService();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

if (enrollMode)
{
    var token = args.SkipWhile(a => a != "--token").Skip(1).FirstOrDefault();
    if (string.IsNullOrEmpty(token))
    {
        Console.Error.WriteLine("Usage: PatchPlatform.Agent.Service --enroll --token <TOKEN> [--server <URL>]");
        Environment.Exit(1);
        return;
    }

    using var scope = host.Services.CreateScope();
    var runtime = scope.ServiceProvider.GetRequiredService<AgentRuntime>();
    await runtime.EnrollAsync(token, server);
    Console.WriteLine("Enrollment complete.");
    return;
}

await host.RunAsync();
