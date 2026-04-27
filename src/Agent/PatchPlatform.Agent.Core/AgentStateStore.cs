using System.Text.Json;

namespace PatchPlatform.Agent.Core;

public interface IAgentStateStore
{
    Task<AgentState?> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AgentState state, CancellationToken ct = default);
}

public class FileAgentStateStore : IAgentStateStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public FileAgentStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<AgentState?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return null;
        var json = await File.ReadAllTextAsync(_filePath, ct);
        return JsonSerializer.Deserialize<AgentState>(json);
    }

    public async Task SaveAsync(AgentState state, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(state, _opts);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
