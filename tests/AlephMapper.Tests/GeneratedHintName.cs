using System.Text.RegularExpressions;

namespace AlephMapper.Tests;

internal static class GeneratedHintName
{
    public static bool Matches(string hintName, string mapperName) =>
        Regex.IsMatch(hintName,
            @"(?:^|_)" + Regex.Escape(mapperName) + @"_GeneratedMappings_[0-9A-F]{20}\.g\.cs$",
            RegexOptions.CultureInvariant);

    public static bool IsMapperFile(string hintName) =>
        Regex.IsMatch(hintName, @"_GeneratedMappings_[0-9A-F]{20}\.g\.cs$", RegexOptions.CultureInvariant);
}
