#nullable enable

using System.Collections.Generic;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;

namespace AlephMapper.SyntaxRewriters;

/// <summary>State shared by a single expansion tree. Lexical scopes stay on each visitor.</summary>
internal sealed class InliningContext
{
    public HashSet<IMethodSymbol> CallStack { get; } = new(SymbolEqualityComparer.Default);
    public Dictionary<IMethodSymbol, MappingAnalysis> InlinedMethods { get; } = new(SymbolEqualityComparer.Default);
    public List<CircularReferenceInfo> CircularReferences { get; } = [];
    public List<UnsafeConditionalReceiverInfo> UnsafeConditionalReceivers { get; } = [];
    public List<UnsupportedNullConditionalInfo> UnsupportedNullConditionals { get; } = [];
    public List<UnsupportedSwitchPatternInfo> UnsupportedSwitchPatterns { get; } = [];
    public List<(ISymbol Member, string Reason)> UnsupportedExtensions { get; } = [];
    public INamedTypeSymbol? OutputType { get; set; }
}
