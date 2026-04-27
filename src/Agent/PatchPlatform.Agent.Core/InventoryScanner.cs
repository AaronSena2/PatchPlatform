using Microsoft.Extensions.Logging;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Agent.Core;

public interface IInventoryScanner
{
    Task<List<InstalledAppDto>> ScanAsync(CancellationToken ct = default);
}

public class StubInventoryScanner : IInventoryScanner
{
    public Task<List<InstalledAppDto>> ScanAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<InstalledAppDto>
        {
            new("Stub App A", "1.0.0", "Stub Publisher", "20240101"),
            new("Stub App B", "2.1.3", "Another Publisher", "20240215")
        });
    }
}
