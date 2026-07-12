#nullable enable

using AlephMapper.Diagnostics;
using AlephMapper.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Adaptation;

/// <summary>
/// Validates an adaptation against the original semantic tree before it is inlined.
/// </summary>
internal static class AdaptationValidator
{
    public static bool Validate(SourceProductionContext context, MappingModel mapping, AdaptationModel adaptation)
    {
        var location = adaptation.Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? mapping.MethodSymbol.Locations.FirstOrDefault();
        var isValid = true;

        if (SymbolEqualityComparer.Default.Equals(adaptation.SourceType, mapping.Parameters[0].Type) &&
            SymbolEqualityComparer.Default.Equals(adaptation.DestinationType, mapping.ReturnType))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidAdaptType, location, mapping.MethodSymbol.Name));
            isValid = false;
        }

        if (adaptation.SourceType.IsUnboundGenericType || adaptation.DestinationType.IsUnboundGenericType ||
            adaptation.SourceType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter) ||
            adaptation.DestinationType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AdaptOpenGenericType, location, mapping.MethodSymbol.Name));
            isValid = false;
        }

        var destinationCreations = GetDestinationCreations(
            mapping.BodySyntax.Expression,
            mapping.SemanticModel,
            mapping.ReturnType).ToArray();
        if (destinationCreations.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AdaptUnsupportedSyntax,
                location,
                mapping.MethodSymbol.Name,
                mapping.BodySyntax.Expression.Kind()));
            isValid = false;
        }
        else if (!HasCompatibleConstructors(destinationCreations, adaptation.DestinationType, mapping.SemanticModel.Compilation, mapping.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AdaptRebindingFailed,
                location,
                mapping.MethodSymbol.Name,
                "the adapted destination does not expose a compatible constructor"));
            isValid = false;
        }

        var sourcePaths = CollectSourceMemberPaths(mapping.BodySyntax.Expression, mapping.SemanticModel, mapping.Parameters[0]);
        foreach (var path in sourcePaths)
        {
            if (!TryResolveReadablePath(adaptation.SourceType, path, out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptSourceMemberMissing,
                    location,
                    mapping.MethodSymbol.Name,
                    string.Join(".", path),
                    adaptation.SourceType.ToDisplayString()));
                isValid = false;
            }
        }

        foreach (var assignment in CollectDestinationAssignments(destinationCreations, mapping.SemanticModel))
        {
            var destinationMember = GetWritableInstanceMember(adaptation.DestinationType, assignment.MemberName);
            if (destinationMember == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptDestinationMemberMissing,
                    location,
                    mapping.MethodSymbol.Name,
                    assignment.MemberName,
                    adaptation.DestinationType.ToDisplayString()));
                isValid = false;
                continue;
            }

            if (TryGetDirectSourcePath(assignment.Expression, mapping.SemanticModel, mapping.Parameters[0], out var path) &&
                TryResolveReadablePath(adaptation.SourceType, path, out var sourceMember) &&
                !IsImplicitlyConvertible(mapping.SemanticModel.Compilation, GetMemberType(sourceMember), GetMemberType(destinationMember)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdaptIncompatibleType,
                    location,
                    mapping.MethodSymbol.Name,
                    assignment.MemberName));
                isValid = false;
            }
        }

        return isValid;
    }

    private static IEnumerable<string[]> CollectSourceMemberPaths(ExpressionSyntax body, SemanticModel semanticModel, IParameterSymbol sourceParameter)
    {
        return body.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => semanticModel.GetSymbolInfo(access).Symbol is IPropertySymbol or IFieldSymbol)
            .Select(access => TryGetDirectSourcePath(access, semanticModel, sourceParameter, out var path) ? path : null)
            .Where(path => path != null)
            .GroupBy(path => string.Join(".", path!), StringComparer.Ordinal)
            .Select(group => group.First()!);
    }

    private static bool TryGetDirectSourcePath(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IParameterSymbol sourceParameter,
        out string[] path)
    {
        var segments = new Stack<string>();
        var current = expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            segments.Push(memberAccess.Name.Identifier.Text);
            current = memberAccess.Expression;
        }

        if (current is IdentifierNameSyntax identifier &&
            SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier).Symbol, sourceParameter))
        {
            path = segments.ToArray();
            return path.Length > 0;
        }

        path = [];
        return false;
    }

    private static IEnumerable<ExpressionSyntax> GetDestinationCreations(
        ExpressionSyntax body,
        SemanticModel semanticModel,
        ITypeSymbol originalDestinationType)
    {
        return body.DescendantNodesAndSelf()
                     .OfType<ExpressionSyntax>()
                     .Where(node => node is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
                     .Where(node => SymbolEqualityComparer.Default.Equals(
                         semanticModel.GetTypeInfo(node).Type ?? semanticModel.GetTypeInfo(node).ConvertedType,
                         originalDestinationType));
    }

    private static IEnumerable<(string MemberName, ExpressionSyntax Expression)> CollectDestinationAssignments(
        IEnumerable<ExpressionSyntax> destinationCreations,
        SemanticModel semanticModel)
    {
        foreach (var creation in destinationCreations)
        {
            var initializer = creation switch
            {
                ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
                ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,
                _ => null
            };

            if (initializer == null)
            {
                continue;
            }

            foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                var member = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                if (member is IPropertySymbol or IFieldSymbol)
                {
                    yield return (member.Name, assignment.Right);
                }
            }
        }
    }

    private static bool TryResolveReadablePath(INamedTypeSymbol rootType, IEnumerable<string> path, out ISymbol member)
    {
        ITypeSymbol currentType = rootType;
        member = null!;
        foreach (var segment in path)
        {
            member = GetReadableInstanceMember(currentType, segment)!;
            if (member == null)
            {
                return false;
            }

            currentType = GetMemberType(member)!;
        }

        return true;
    }

    private static ISymbol? GetReadableInstanceMember(ITypeSymbol type, string name)
    {
        return GetMembersIncludingBaseTypes(type, name)
            .FirstOrDefault(member => member switch
            {
                IPropertySymbol property => !property.IsStatic && property.GetMethod != null,
                IFieldSymbol field => !field.IsStatic && !field.IsConst,
                _ => false
            });
    }

    private static ISymbol? GetWritableInstanceMember(ITypeSymbol type, string name)
    {
        return GetMembersIncludingBaseTypes(type, name)
            .FirstOrDefault(member => member switch
            {
                IPropertySymbol property => !property.IsStatic && property.SetMethod != null,
                IFieldSymbol field => !field.IsStatic && !field.IsReadOnly && !field.IsConst,
                _ => false
            });
    }

    private static IEnumerable<ISymbol> GetMembersIncludingBaseTypes(ITypeSymbol type, string name)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                yield return member;
            }
        }
    }

    private static ITypeSymbol? GetMemberType(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
    }

    private static bool IsImplicitlyConvertible(Compilation compilation, ITypeSymbol? source, ITypeSymbol? destination)
    {
        return source != null &&
               destination != null &&
               compilation is CSharpCompilation csharpCompilation &&
               csharpCompilation.ClassifyConversion(source, destination).IsImplicit;
    }

    private static bool HasCompatibleConstructors(
        IEnumerable<ExpressionSyntax> destinationCreations,
        INamedTypeSymbol adaptedDestinationType,
        Compilation compilation,
        SemanticModel semanticModel)
    {
        foreach (var creation in destinationCreations.OfType<ObjectCreationExpressionSyntax>())
        {
            var argumentTypes = creation.ArgumentList?.Arguments
                .Select(argument => semanticModel.GetTypeInfo(argument.Expression).Type)
                .ToArray() ?? [];
            if (!adaptedDestinationType.InstanceConstructors.Any(constructor =>
                    IsCompatibleConstructor(constructor, argumentTypes, compilation)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompatibleConstructor(
        IMethodSymbol constructor,
        IReadOnlyList<ITypeSymbol?> argumentTypes,
        Compilation compilation)
    {
        var parameters = constructor.Parameters;
        var paramsIndex = parameters.Length > 0 && parameters[parameters.Length - 1].IsParams
            ? parameters.Length - 1
            : -1;
        var requiredParameters = parameters.Count(parameter => !parameter.IsOptional && !parameter.IsParams);
        if (argumentTypes.Count < requiredParameters || (paramsIndex < 0 && argumentTypes.Count > parameters.Length))
        {
            return false;
        }

        for (var argumentIndex = 0; argumentIndex < argumentTypes.Count; argumentIndex++)
        {
            var parameter = argumentIndex < parameters.Length
                ? parameters[argumentIndex]
                : parameters[paramsIndex];
            var parameterType = parameter.IsParams && parameter.Type is IArrayTypeSymbol arrayType
                ? arrayType.ElementType
                : parameter.Type;
            if (!IsImplicitlyConvertible(compilation, argumentTypes[argumentIndex], parameterType))
            {
                return false;
            }
        }

        return true;
    }
}
