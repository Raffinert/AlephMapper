namespace AlephMapper;

internal static class VersionInfo
{
    public static string Version { get; } =
        System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(VersionInfo).Assembly)
            ?.InformationalVersion
            ?.Split('+')[0]
        is { } informationalVersion
            ? $"{informationalVersion}.{typeof(VersionInfo).Assembly.GetName().Version?.Revision ?? 0}"
            : typeof(VersionInfo).Assembly.GetName().Version?.ToString() ?? "unknown";
}
