using System.Reflection;

namespace PrinterSecsGem.Eq;

public static class ApplicationInfo
{
    public static string Version => GetVersion();

    public static string DisplayVersion => $"v{Version}";

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Trim();
        }

        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }
}
