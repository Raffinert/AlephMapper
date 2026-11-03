using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AlephMapper.Generation;

/// <summary>Atomically reserves generated members against source and generated signatures.</summary>
internal sealed class GeneratedMemberRegistry
{
    private readonly HashSet<MethodSignature> existing;
    private readonly HashSet<MethodSignature> generated = new();
    private readonly HashSet<string> nonMethodNames;

    public GeneratedMemberRegistry(INamedTypeSymbol mapper)
    {
        existing = new(mapper.GetMembers().OfType<IMethodSymbol>().Select(MethodSignature.FromMethod));
        nonMethodNames = new(mapper.GetMembers().Where(m => m is not IMethodSymbol).Select(m => m.Name), StringComparer.Ordinal);
    }

    public bool TryReserve(IEnumerable<MethodSignature> requested, out string conflict)
    {
        var batch = new HashSet<MethodSignature>();
        foreach (var signature in requested)
        {
            if (nonMethodNames.Contains(signature.Name) || existing.Contains(signature) || generated.Contains(signature) || !batch.Add(signature))
            {
                conflict = nonMethodNames.Contains(signature.Name) ? signature.Name : signature.ToString();
                return false;
            }
        }
        generated.UnionWith(batch);
        conflict = string.Empty;
        return true;
    }
}
