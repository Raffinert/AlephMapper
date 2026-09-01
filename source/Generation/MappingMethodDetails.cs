using AlephMapper.Helpers;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AlephMapper.Generation;

/// <summary>
/// Caches the display strings shared by all emitters for a mapping method.
/// </summary>
internal sealed class MappingMethodDetails
{
    public MappingMethodDetails(MappingAnalysis mapping)
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
        MethodTypeParameterList = mapping.MethodSymbol.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", mapping.MethodSymbol.TypeParameters.Select(static parameter => parameter.Name)) + ">";
        MethodTypeParameterCount = mapping.MethodSymbol.TypeParameters.Length;
        MethodConstraintClauses = BuildConstraintClauses(mapping.MethodSymbol.TypeParameters, nullableContext);
        NullableContext = nullableContext;
    }

    public MappingAnalysis Mapping { get; }
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
    public string MethodTypeParameterList { get; }
    public int MethodTypeParameterCount { get; }
    public IReadOnlyList<string> MethodConstraintClauses { get; }
    public Microsoft.CodeAnalysis.NullableContext NullableContext { get; }

    private static IReadOnlyList<string> BuildConstraintClauses(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        NullableContext nullableContext)
    {
        var clauses = new List<string>();
        foreach (var typeParameter in typeParameters)
        {
            var constraints = new List<string>();
            if (typeParameter.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }
            else if (typeParameter.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (typeParameter.HasReferenceTypeConstraint)
            {
                constraints.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
            }
            else if (typeParameter.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            constraints.AddRange(typeParameter.ConstraintTypes.Select(type =>
                TypeDisplay.ForSymbol(type, type.NullableAnnotation, nullableContext)));

            if (typeParameter.HasConstructorConstraint && !typeParameter.HasValueTypeConstraint)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                clauses.Add($"where {typeParameter.Name} : {string.Join(", ", constraints)}");
            }
        }

        return clauses;
    }
}
