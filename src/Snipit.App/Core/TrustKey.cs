using System.Security.Cryptography;
using Microsoft.Win32;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// The per-user, per-machine key behind the workflow trust hash (Core WorkflowTrust). Created once,
/// kept in HKCU. If the registry cannot be read or written the key is random for this run only, so
/// every settings file reads as changed: it fails closed, never open.
/// </summary>
internal static class TrustKey
{
    private const string KeyPath = @"Software\Snipit";
    private const string ValueName = "TrustKey";
    private static byte[]? _key;

    public static byte[] Get()
    {
        if (_key is not null) return _key;
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (k.GetValue(ValueName) is byte[] existing && existing.Length == WorkflowTrust.KeyBytes) return _key = existing;
            var fresh = RandomNumberGenerator.GetBytes(WorkflowTrust.KeyBytes);
            k.SetValue(ValueName, fresh, RegistryValueKind.Binary);
            return _key = fresh;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Warn("Trust key unavailable (" + ex.Message + "); workflow settings will read as changed this run.");
            return _key = RandomNumberGenerator.GetBytes(WorkflowTrust.KeyBytes);
        }
    }
}
