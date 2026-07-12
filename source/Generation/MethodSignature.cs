using System;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Generation;

internal static class MethodSignature
{
    public static string Build(string name, IEnumerable<string> parameterTypeNames)
    {
        return name + "(" + string.Join(",", parameterTypeNames.Select(RemoveNullableMarker)) + ")";
    }

    private static string RemoveNullableMarker(string typeName)
    {
        return typeName.EndsWith("?", StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - 1)
            : typeName;
    }
}
