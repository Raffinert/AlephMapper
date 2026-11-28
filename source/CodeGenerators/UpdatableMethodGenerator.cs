#nullable enable

using AlephMapper.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AlephMapper.CodeGenerators;

/// <summary>
/// Lowers an (already inlined) mapping body into statements that update an existing destination in place.
/// Member types are resolved from the destination type symbols while walking the object initializers,
/// so nested objects, inherited members, value types and collections are classified by their real types.
/// </summary>
internal sealed class UpdatableMethodGenerator(
    IReadOnlyList<string> sourceParamNames,
    CollectionPropertiesPolicy collectionPolicy,
    NullablePolicy nullablePolicy)
{
    private const string Indent = "    ";

    private readonly List<string> _lines = [];
    private readonly string _primarySourceParamName = sourceParamNames.FirstOrDefault() ?? "source";

    /// <summary>
    /// Gets the reason why the body cannot be lowered into an in-place update, when that is the case.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Lowers a root body (an object creation or a conditional producing one) into update statements.
    /// Returns <c>false</c> when the shape is unsupported; <see cref="FailureReason"/> is set when the
    /// shape is recognized but cannot be updated in place.
    /// </summary>
    public bool TryProcessRoot(ExpressionSyntax body, string destPrefix, ITypeSymbol destinationType, out List<string> lines)
    {
        _lines.Clear();
        FailureReason = null;
        lines = [];

        switch (Unwrap(body))
        {
            case ObjectCreationExpressionSyntax creation:
                if (creation.Initializer is not { RawKind: (int)SyntaxKind.ObjectInitializerExpression } initializer
                    || initializer.Expressions.Count == 0)
                {
                    return false;
                }

                EmitRootValue(creation, destPrefix, destinationType, "", topLevel: true);
                break;

            case ConditionalExpressionSyntax conditional:
                EmitRootValue(conditional, destPrefix, destinationType, "", topLevel: true);
                break;

            default:
                return false;
        }

        if (FailureReason != null)
        {
            return false;
        }

        lines = _lines.ToList();
        return true;
    }

    private void EmitRootValue(ExpressionSyntax expression, string destPrefix, ITypeSymbol destinationType, string indent, bool topLevel)
    {
        var unwrapped = Unwrap(expression);

        if (IsNullValue(unwrapped))
        {
            // A mapping that yields no value leaves the existing destination untouched.
            if (!topLevel)
            {
                _lines.Add($"{indent}return {destPrefix};");
            }

            return;
        }

        switch (unwrapped)
        {
            case ThrowExpressionSyntax throwExpression:
                _lines.Add($"{indent}{ToThrowStatement(throwExpression)}");
                return;

            case ConditionalExpressionSyntax conditional:
                EmitRootConditional(conditional, destPrefix, destinationType, indent, topLevel);
                return;

            case ObjectCreationExpressionSyntax { Initializer: null, ArgumentList: null or { Arguments.Count: 0 } } emptyCreation:
                // `new T()` in a root branch: the update only has to make sure the destination exists.
                EmitInPlaceUpdate(emptyCreation, destPrefix, destinationType, indent, createWithExpression: !CreatesType(emptyCreation, destinationType));
                return;

            case ObjectCreationExpressionSyntax creation:
                if (!CanUpdateInPlace(creation, destinationType, out var reason))
                {
                    Fail(reason);
                    return;
                }

                // A derived type created for a base-typed destination must be created by the mapping's own expression.
                EmitInPlaceUpdate(creation, destPrefix, destinationType, indent, createWithExpression: !CreatesType(creation, destinationType));
                return;

            default:
                _lines.Add($"{indent}{destPrefix} = {NormalizeConditionalMemberAccess(expression)};");
                return;
        }
    }

    private void EmitRootConditional(ConditionalExpressionSyntax conditional, string destPrefix, ITypeSymbol destinationType, string indent, bool topLevel)
    {
        var whenTrue = Unwrap(conditional.WhenTrue);
        var whenFalse = Unwrap(conditional.WhenFalse);

        // `cond ? null : value` (or a throw) becomes a guard clause followed by the update of the other branch.
        if (IsTerminalBranch(whenTrue))
        {
            _lines.Add($"{indent}if ({conditional.Condition.WithoutTrivia()}) {TerminalStatement(whenTrue, destPrefix)}");
            EmitRootValue(whenFalse, destPrefix, destinationType, indent, topLevel);
            return;
        }

        if (IsTerminalBranch(whenFalse))
        {
            _lines.Add($"{indent}if ({Negate(conditional.Condition)}) {TerminalStatement(whenFalse, destPrefix)}");
            EmitRootValue(whenTrue, destPrefix, destinationType, indent, topLevel);
            return;
        }

        _lines.Add($"{indent}if ({conditional.Condition.WithoutTrivia()})");
        _lines.Add($"{indent}{{");
        EmitRootValue(whenTrue, destPrefix, destinationType, indent + Indent, topLevel: false);
        _lines.Add($"{indent}}}");
        _lines.Add($"{indent}else");
        _lines.Add($"{indent}{{");
        EmitRootValue(whenFalse, destPrefix, destinationType, indent + Indent, topLevel: false);
        _lines.Add($"{indent}}}");
    }

    /// <summary>
    /// Emits the update of an object that is created with an object initializer:
    /// ensures the target exists, then updates every initialized member.
    /// </summary>
    private void EmitInPlaceUpdate(ObjectCreationExpressionSyntax creation, string path, ITypeSymbol? type, string indent, bool createWithExpression = false)
    {
        var memberIndent = indent;
        var closeElse = false;

        // A type parameter without a `struct` constraint may still be a class, so a missing root
        // destination is created too (`== null` compiles for unconstrained type parameters).
        if (type != null
            && (type.IsReferenceType || type is ITypeParameterSymbol { IsValueType: false })
            && type.SpecialType != SpecialType.System_String)
        {
            if (!createWithExpression && CanDefaultConstruct(type))
            {
                _lines.Add($"{indent}if ({path} == null)");
                _lines.Add($"{indent}{Indent}{path} = new {GetTypeName(type)}();");
            }
            else
            {
                // Required members or a missing parameterless constructor: create the target exactly as the
                // mapping does, and only update member by member when an instance already exists.
                _lines.Add($"{indent}if ({path} == null)");
                _lines.Add($"{indent}{{");
                _lines.Add($"{indent}{Indent}{path} = {NormalizeConditionalMemberAccess(creation)};");
                _lines.Add($"{indent}}}");
                _lines.Add($"{indent}else");
                _lines.Add($"{indent}{{");
                memberIndent = indent + Indent;
                closeElse = true;
            }
        }

        if (creation.Initializer != null)
        {
            EmitInitializerMembers(creation.Initializer, path, type, memberIndent);
        }

        if (closeElse)
        {
            _lines.Add($"{indent}}}");
        }
    }

    private void EmitInitializerMembers(InitializerExpressionSyntax initializer, string path, ITypeSymbol? type, string indent)
    {
        foreach (var expression in initializer.Expressions)
        {
            if (expression is not AssignmentExpressionSyntax assignment)
            {
                Fail($"initializer element '{expression.WithoutTrivia()}' of '{path}' cannot be applied to an existing instance");
                continue;
            }

            switch (assignment.Left)
            {
                case IdentifierNameSyntax identifier:
                    var memberName = identifier.Identifier.ValueText;
                    var member = FindMember(type, memberName);
                    if (member == null && type is INamedTypeSymbol { TypeKind: not TypeKind.Error })
                    {
                        Fail($"member '{memberName}' is not declared on '{type.ToDisplayString()}'");
                        continue;
                    }

                    if (member is { IsWritable: false } && !IsNestedInitializer(assignment.Right))
                    {
                        Fail($"member '{memberName}' of '{path}' is init-only or read-only");
                        continue;
                    }

                    // Identifier.Text keeps a verbatim '@' prefix (`@event`); ValueText is only used for lookup.
                    EmitMemberValue(assignment.Right, $"{path}.{identifier.Identifier.Text}", member?.Type, indent);
                    break;

                case ImplicitElementAccessSyntax elementAccess:
                    _lines.Add($"{indent}{path}{elementAccess.ArgumentList.WithoutTrivia()} = {NormalizeConditionalMemberAccess(assignment.Right)};");
                    break;

                default:
                    Fail($"initializer element '{assignment.WithoutTrivia()}' of '{path}' is not supported");
                    break;
            }
        }
    }

    private void EmitMemberValue(ExpressionSyntax expression, string path, ITypeSymbol? memberType, string indent)
    {
        if (memberType != null
            && collectionPolicy == CollectionPropertiesPolicy.Skip
            && CollectionHelper.IsCollectionType(memberType))
        {
            _lines.Add($"{indent}// Skipping collection property: {path}");
            return;
        }

        switch (Unwrap(expression))
        {
            // Only split into if/else when a branch is updated in place; otherwise the whole conditional is
            // assigned so its natural type (e.g. `c ? 5 : default` is an int) stays exactly as in the mapping.
            case ConditionalExpressionSyntax conditional when HasInPlaceBranch(conditional, memberType):
                _lines.Add($"{indent}if ({conditional.Condition.WithoutTrivia()})");
                _lines.Add($"{indent}{{");
                EmitMemberBranch(conditional.WhenTrue, path, memberType, indent + Indent);
                _lines.Add($"{indent}}}");
                _lines.Add($"{indent}else");
                _lines.Add($"{indent}{{");
                EmitMemberBranch(conditional.WhenFalse, path, memberType, indent + Indent);
                _lines.Add($"{indent}}}");
                return;

            case ObjectCreationExpressionSyntax creation when WillUpdateInPlace(creation, memberType):
                EmitInPlaceUpdate(creation, path, memberType, indent);
                return;

            case InitializerExpressionSyntax nestedInitializer:
                EmitNestedInitializer(nestedInitializer, path, memberType, indent);
                return;

            default:
                // Scalars, value types (struct members cannot be updated through a property copy) and
                // objects that cannot be updated in place are assigned as a whole, exactly as the mapping does.
                _lines.Add($"{indent}{path} = {NormalizeConditionalMemberAccess(expression)};");
                return;
        }
    }

    private bool HasInPlaceBranch(ConditionalExpressionSyntax conditional, ITypeSymbol? memberType)
    {
        bool Check(ExpressionSyntax branch) => Unwrap(branch) switch
        {
            ConditionalExpressionSyntax nested => HasInPlaceBranch(nested, memberType),
            ObjectCreationExpressionSyntax creation => WillUpdateInPlace(creation, memberType),
            _ => false
        };

        return Check(conditional.WhenTrue) || Check(conditional.WhenFalse);
    }

    /// <summary>
    /// A nested creation is updated in place only when the member is known to be a reference type, the mapping
    /// creates exactly the member's type (not a derived one) and all of its state is settable after construction.
    /// Anything else is assigned as a whole, so the destination receives the same value the mapping produces.
    /// </summary>
    private bool WillUpdateInPlace(ObjectCreationExpressionSyntax creation, ITypeSymbol? memberType) =>
        memberType is { IsReferenceType: true }
        && CreatesType(creation, memberType)
        && CanUpdateInPlace(creation, memberType, out _);

    private bool CreatesType(ObjectCreationExpressionSyntax creation, ITypeSymbol type) =>
        string.Equals(creation.Type.WithoutTrivia().ToString(), GetTypeName(type), StringComparison.Ordinal);

    private void EmitMemberBranch(ExpressionSyntax branch, string path, ITypeSymbol? memberType, string indent)
    {
        if (Unwrap(branch) is ThrowExpressionSyntax throwExpression)
        {
            _lines.Add($"{indent}{ToThrowStatement(throwExpression)}");
            return;
        }

        EmitMemberValue(branch, path, memberType, indent);
    }

    private void EmitNestedInitializer(InitializerExpressionSyntax initializer, string path, ITypeSymbol? memberType, string indent)
    {
        // `Member = { ... }` mutates the instance the destination already holds, so the update does the same.
        if (initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            EmitInitializerMembers(initializer, path, memberType, indent);
            return;
        }

        Fail($"collection initializer of '{path}' would add items to the existing collection on every update");
    }

    /// <summary>
    /// An object creation can be applied member by member to an existing instance only when every piece of
    /// state it sets is reachable after construction: no constructor arguments and only writable members.
    /// </summary>
    private bool CanUpdateInPlace(ObjectCreationExpressionSyntax creation, ITypeSymbol? type, out string reason)
    {
        if (creation.ArgumentList is { Arguments.Count: > 0 })
        {
            reason = "constructor arguments cannot be applied to an existing instance";
            return false;
        }

        if (creation.Initializer is not { } initializer || !initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            reason = "the destination is not created with an object initializer";
            return false;
        }

        foreach (var expression in initializer.Expressions)
        {
            if (expression is not AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } assignment)
            {
                reason = $"initializer element '{expression.WithoutTrivia()}' cannot be applied to an existing instance";
                return false;
            }

            var member = FindMember(type, identifier.Identifier.ValueText);
            if (member is { IsWritable: false } && !IsNestedInitializer(assignment.Right))
            {
                reason = $"member '{identifier.Identifier.ValueText}' is init-only or read-only";
                return false;
            }

            if (Unwrap(assignment.Right) is InitializerExpressionSyntax nested && !IsReplayable(nested, member?.Type))
            {
                reason = $"the nested initializer of member '{identifier.Identifier.ValueText}' contains a collection initializer that would add items to the existing collection on every update";
                return false;
            }
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// Whether a nested initializer without <c>new</c> can be applied to the instance a destination already holds.
    /// Collection initializers add items, so they are replayable only when the collection is skipped anyway.
    /// </summary>
    private bool IsReplayable(InitializerExpressionSyntax initializer, ITypeSymbol? memberType)
    {
        if (!initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return memberType != null
                && collectionPolicy == CollectionPropertiesPolicy.Skip
                && CollectionHelper.IsCollectionType(memberType);
        }

        foreach (var expression in initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } assignment
                && Unwrap(assignment.Right) is InitializerExpressionSyntax nested
                && !IsReplayable(nested, FindMember(memberType, identifier.Identifier.ValueText)?.Type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNestedInitializer(ExpressionSyntax expression) => Unwrap(expression) is InitializerExpressionSyntax;

    private readonly struct MemberInfo(ITypeSymbol type, bool isWritable)
    {
        public ITypeSymbol Type { get; } = type;
        public bool IsWritable { get; } = isWritable;
    }

    private static MemberInfo? FindMember(ITypeSymbol? type, string name)
    {
        if (type == null)
        {
            return null;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            foreach (var constraint in typeParameter.ConstraintTypes)
            {
                if (FindMember(constraint, name) is { } constrained)
                {
                    return constrained;
                }
            }

            return null;
        }

        for (var current = type; current != null; current = current.BaseType)
        {
            if (FindDeclaredMember(current, name) is { } member)
            {
                return member;
            }
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (FindDeclaredMember(implemented, name) is { } member)
            {
                return member;
            }
        }

        return null;
    }

    private static MemberInfo? FindDeclaredMember(ITypeSymbol type, string name)
    {
        foreach (var member in type.GetMembers(name))
        {
            switch (member)
            {
                case IPropertySymbol { IsIndexer: false } property:
                    return new MemberInfo(property.Type, FindSetter(property) is { IsInitOnly: false });
                case IFieldSymbol field:
                    return new MemberInfo(field.Type, !field.IsReadOnly && !field.IsConst);
            }
        }

        return null;
    }

    /// <summary>
    /// An override may redeclare only the getter; the setter is then inherited from the overridden property.
    /// </summary>
    private static IMethodSymbol? FindSetter(IPropertySymbol property)
    {
        for (IPropertySymbol? current = property; current != null; current = current.OverriddenProperty)
        {
            if (current.SetMethod != null)
            {
                return current.SetMethod;
            }
        }

        return null;
    }

    private static bool CanDefaultConstruct(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return typeParameter.HasConstructorConstraint;
        }

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } named)
        {
            return false;
        }

        var constructor = named.InstanceConstructors.FirstOrDefault(c =>
            c.Parameters.All(p => p.IsOptional || p.IsParams)
            && c.DeclaredAccessibility is not (Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal));
        if (constructor == null)
        {
            return false;
        }

        return !HasRequiredMembers(named) || SetsRequiredMembers(constructor);
    }

    private static bool HasRequiredMembers(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (current.GetMembers().Any(member => member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SetsRequiredMembers(IMethodSymbol constructor) =>
        constructor.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");

    private void Fail(string reason) => FailureReason ??= reason;

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsTerminalBranch(ExpressionSyntax expression) =>
        IsNullValue(expression) || Unwrap(expression) is ThrowExpressionSyntax;

    private static string TerminalStatement(ExpressionSyntax expression, string destPrefix) =>
        Unwrap(expression) is ThrowExpressionSyntax throwExpression
            ? ToThrowStatement(throwExpression)
            : $"return {destPrefix};";

    private static string ToThrowStatement(ThrowExpressionSyntax throwExpression) =>
        $"throw {throwExpression.Expression.WithoutTrivia()};";

    /// <summary>
    /// Recognizes the "no value" forms a mapping uses: <c>null</c>, <c>null!</c>, <c>default</c>,
    /// <c>default(T)</c> and casts of those.
    /// </summary>
    internal static bool IsNullValue(ExpressionSyntax expression)
    {
        switch (Unwrap(expression))
        {
            case LiteralExpressionSyntax literal:
                return literal.IsKind(SyntaxKind.NullLiteralExpression) || literal.IsKind(SyntaxKind.DefaultLiteralExpression);
            case DefaultExpressionSyntax:
                return true;
            case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                return IsNullValue(postfix.Operand);
            case CastExpressionSyntax cast:
                return IsNullValue(cast.Expression);
            default:
                return false;
        }
    }

    private static string Negate(ExpressionSyntax condition)
    {
        var unwrapped = Unwrap(condition);
        switch (unwrapped)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression):
                return $"{binary.Left.WithoutTrivia()} == {binary.Right.WithoutTrivia()}";
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
                return $"{binary.Left.WithoutTrivia()} != {binary.Right.WithoutTrivia()}";
            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { Pattern: ConstantPatternSyntax constant } unary } isPattern
                when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword):
                return $"{isPattern.Expression.WithoutTrivia()} is {constant.WithoutTrivia()}";
            case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax constant } isPattern
                when constant.Expression.IsKind(SyntaxKind.NullLiteralExpression):
                return $"{isPattern.Expression.WithoutTrivia()} is not null";
            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                return prefix.Operand.WithoutTrivia().ToString();
            case IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax:
                return $"!{unwrapped.WithoutTrivia()}";
            default:
                return $"!({condition.WithoutTrivia()})";
        }
    }

    private string GetTypeName(ITypeSymbol type) =>
        TypeDisplay.ForSymbol(type, NullableAnnotation.NotAnnotated, nullablePolicy);

    private string NormalizeConditionalMemberAccess(ExpressionSyntax expression)
    {
        // Convert shapes like (conditional)?.WhenNotNull.Tail into a textual form that preserves the
        // conditional access correctly when used on the right-hand side of assignments.

        string Recurse(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case MemberAccessExpressionSyntax ma:
                    return Recurse(ma.Expression) + "." + ma.Name;

                case ConditionalAccessExpressionSyntax cae:
                    var target = cae.Expression.WithoutTrivia().ToString();
                    var tail = cae.WhenNotNull.WithoutTrivia().ToString(); // typically starts with '.' or '['
                    return target + "?" + tail;

                case MemberBindingExpressionSyntax mbs:
                    // Dot-prefixed fragment without its conditional root; attach to the source parameter
                    return _primarySourceParamName + "?" + mbs.WithoutTrivia();

                case ElementBindingExpressionSyntax ebs:
                    return _primarySourceParamName + "?" + ebs.WithoutTrivia();

                case BinaryExpressionSyntax binary:
                    return Recurse(binary.Left) + " " + binary.OperatorToken.Text + " " + Recurse(binary.Right);

                case InterpolatedStringExpressionSyntax ise:
                    return FormatInterpolated(ise, Recurse);

                default:
                    return expr.WithoutTrivia().ToString();
            }
        }

        return Recurse(expression);
    }

    /// <summary>
    /// Rebuilds an interpolated string from its original tokens, so verbatim and raw forms, escapes,
    /// alignment and format clauses are preserved; only the interpolation holes are normalized.
    /// </summary>
    private static string FormatInterpolated(InterpolatedStringExpressionSyntax ise, Func<ExpressionSyntax, string> normalize)
    {
        var sb = new StringBuilder();
        sb.Append(ise.StringStartToken.Text);
        foreach (var part in ise.Contents)
        {
            switch (part)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(text.TextToken.Text);
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append(interpolation.OpenBraceToken.Text);
                    sb.Append(normalize(interpolation.Expression));
                    if (interpolation.AlignmentClause != null)
                    {
                        sb.Append(interpolation.AlignmentClause.WithoutTrivia());
                    }

                    if (interpolation.FormatClause != null)
                    {
                        sb.Append(interpolation.FormatClause.WithoutTrivia());
                    }

                    sb.Append(interpolation.CloseBraceToken.Text);
                    break;
            }
        }

        sb.Append(ise.StringEndToken.Text);
        return sb.ToString();
    }
}
