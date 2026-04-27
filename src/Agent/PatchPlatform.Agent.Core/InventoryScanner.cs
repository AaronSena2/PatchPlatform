using Microsoft.Extensions.Logging;
using PatchPlatform.Shared.Contracts;

namespace PatchPlatform.Agent.Core;

public interface IInventoryScanner
{
    Task<List<InstalledAppDto>> ScanAsync(CancellationToken ct = default);
}

/// <summary>
/// Reads installed application information from the Windows registry uninstall keys.
/// Covers 64-bit (HKLM), 32-bit/WOW64 (HKLM WOW6432Node), and per-user (HKCU) entries.
/// Falls back to an empty list on non-Windows platforms or when registry access fails.
/// </summary>
public class RegistryInventoryScanner : IInventoryScanner
{
    private static readonly string[] UninstallKeyPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private readonly ILogger<RegistryInventoryScanner> _logger;

    public RegistryInventoryScanner(ILogger<RegistryInventoryScanner> logger)
    {
        _logger = logger;
    }

    public Task<List<InstalledAppDto>> ScanAsync(CancellationToken ct = default)
    {
        var apps = new List<InstalledAppDto>();

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Registry inventory scan is only supported on Windows. Returning empty inventory.");
            return Task.FromResult(apps);
        }

        try
        {
            ScanHive(Microsoft.Win32.Registry.LocalMachine, apps);
            ScanHive(Microsoft.Win32.Registry.CurrentUser, apps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registry inventory scan. Returning partial results.");
        }

        _logger.LogInformation("Registry inventory scan complete: {Count} apps found.", apps.Count);
        return Task.FromResult(apps);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void ScanHive(Microsoft.Win32.RegistryKey hive, List<InstalledAppDto> apps)
    {
        foreach (var keyPath in UninstallKeyPaths)
        {
            try
            {
                using var uninstallKey = hive.OpenSubKey(keyPath, writable: false);
                if (uninstallKey == null) continue;

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // Skip system components and updates
                        if (subKey.GetValue("SystemComponent") is int sysComp && sysComp == 1) continue;
                        if (!string.IsNullOrEmpty(subKey.GetValue("ParentKeyName") as string)) continue;

                        var version = subKey.GetValue("DisplayVersion") as string ?? string.Empty;
                        var publisher = subKey.GetValue("Publisher") as string ?? string.Empty;
                        var installDate = subKey.GetValue("InstallDate") as string ?? string.Empty;

                        apps.Add(new InstalledAppDto(
                            Name: displayName.Trim(),
                            Version: version.Trim(),
                            Publisher: publisher.Trim(),
                            InstallDate: installDate.Trim()));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skipping registry subkey '{KeyName}' in '{HivePath}\\{KeyPath}' due to access error.",
                            subKeyName, hive.Name, keyPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not open registry key '{HivePath}\\{KeyPath}'.", hive.Name, keyPath);
            }
        }
    }
}
