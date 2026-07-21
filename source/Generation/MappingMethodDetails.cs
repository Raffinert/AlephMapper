using AlephMapper.Helpers;
using AlephMapper.Models;
using System.Linq;

namespace AlephMapper.Generation;

/// <summary>
/// Caches the display strings shared by all emitters for a mapping method.
/// </summary>
internal sealed class MappingMethodDetails
{
    public MappingMethodDetails(MappingModel mapping)
    {
        Mapping = mapping;
        var nullableContextPosition = mapping.MethodSymbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0;
        var nullableContext = mapping.SemanticModel.GetNullableContext(nullableContextPosition);

        ParameterTypeNames = mapping.Parameters
            .Select(parameter => TypeDisplay.ForSymbol(parameter.Type, parameter.NullableAnnotation, nullableContext))
            .ToArray();
        DestinationTypeName = TypeDisplay.ForSymbol(mapping.ReturnType, mapping.MethodSymbol.ReturnNullableAnnotation, nullableContext);
        SourceTypeName = TypeDisplay.ForSymbol(mapping.ParamType, mapping.Parameters[0].NullableAnnotation, nullableContext);
        SourceName = mapping.Parameters[0].Name;
        MethodParameterList = string.Join(", ", ParameterTypeNames);
        MethodParameterListWithNames = string.Join(", ", mapping.Parameters.Select(parameter =>
            $"{TypeDisplay.ForSymbol(parameter.Type, parameter.NullableAnnotation, nullableContext)} {parameter.Name}"));
        ExtraExpressionParameterTypeNames = ParameterTypeNames.Skip(1).ToArray();
        ExtraExpressionParameterListWithNames = string.Join(", ", mapping.Parameters.Skip(1).Select(parameter =>
            $"{TypeDisplay.ForSymbol(parameter.Type, parameter.NullableAnnotation, nullableContext)} {parameter.Name}"));
        LambdaParameters = mapping.Parameters.Count == 1
            ? mapping.Parameters[0].Name
            : "(" + string.Join(", ", mapping.Parameters.Select(parameter => parameter.Name)) + ")";
        ProjectionLambdaParameter = mapping.Parameters[0].Name;
        NullableContext = nullableContext;
    }

    public MappingModel Mapping { get; }
    public string[] ParameterTypeNames { get; }
    public string DestinationTypeName { get; }
    public string SourceTypeName { get; }
    public string SourceName { get; }
    public string MethodParameterList { get; }
    public string MethodParameterListWithNames { get; }
    public string[] ExtraExpressionParameterTypeNames { get; }
    public string ExtraExpressionParameterListWithNames { get; }
    public string LambdaParameters { get; }
    public string ProjectionLambdaParameter { get; }
    public Microsoft.CodeAnalysis.NullableContext NullableContext { get; }
}
