using System.IO;
using Microsoft.Win32;

namespace DeskFlip.Config;

/// <summary>
/// Autostart via HKCU Run key. Self-heals: when enabled, every launch
/// rewrites the value if it no longer matches the current exe path (user moved the exe).
/// Registry access may be denied by policy/AV — <see cref="SetEnabled"/> surfaces that
/// as a returned false so the UI can snap the toggle back and warn.
/// </summary>
public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskFlip";

    private readonly Func<string?> _processPath;

    public AutostartService() : this(() => Environment.ProcessPath)
    {
    }

    /// <summary>Test constructor with an injectable process path.</summary>
    public AutostartService(Func<string?> processPath)
    {
        _processPath = processPath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Returns false when the registry write/delete was denied (policy, AV).</summary>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                var path = _processPath();
                if (string.IsNullOrEmpty(path))
                    return false;
                key.SetValue(ValueName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>When enabled, rewrite the Run value if the exe path changed (self-heal).</summary>
    public void SelfHeal()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string current)
                return;
            var path = _processPath();
            if (string.IsNullOrEmpty(path))
                return;
            var expected = $"\"{path}\"";
            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, expected);
        }
        catch (UnauthorizedAccessException)
        {
            // Healing is best-effort; a denied write must not break startup.
        }
        catch (IOException)
        {
        }
    }
}
