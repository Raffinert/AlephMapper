using AgileObjects.ReadableExpressions;

namespace AlephMapper.Tests;

// Models for testing tech debt scenarios
public class TechDebtTestAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string FormattedAddress => $"{Street}, {City}";
}

public class TechDebtTestPerson
{
    public string Name { get; set; } = string.Empty;
    public TechDebtTestAddress? Address { get; set; }
}

public class TechDebtTestPersonDto
{
    public string Name { get; set; } = string.Empty;
    public string AddressStr { get; set; } = string.Empty;
}

// Extension method mapper that should trigger the tech debt
public static partial class TechDebtAddressMapper
{
    [Expressive]
    public static string FormatAddress(this TechDebtTestAddress address) => 
        address.FormattedAddress;
}

// Mapper with nested conditional access that triggers ParseExpression tech debt
[Expressive(NullConditionalRewrite = NullConditionalRewrite.None)]
public static partial class TechDebtPersonMapperNone
{
    [Expressive] 
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        // This should trigger the conditional access expressions stack issue (lines 79-81 in InvocationRewriter)
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class TechDebtPersonMapperRewrite
{
    [Expressive]
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        // This should trigger both tech debt scenarios:
        // 1. Extension method inlining with conditional access (lines 79-81 in InvocationRewriter) 
        // 2. Null conditional rewriter patching (line 45 in NullConditionalRewriter)
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}

[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class TechDebtPersonMapperIgnore
{
    [Expressive]
    public static TechDebtTestPersonDto ToDto(TechDebtTestPerson person) => new()
    {
        Name = person.Name,
        AddressStr = person.Address?.FormatAddress() ?? "No Address"
    };
}

/// <summary>
/// Tests specifically designed to exercise the tech debt scenarios identified in:
/// - InliningResolver.InvocationRewriter.cs lines 79-81 (ParseExpression with string.Join)
/// - InliningResolver.NullConditionalRewriter.cs line 45 (ParseExpression for patching expressions)
/// </summary>
public class TechDebtFixTests
{
    [Test]
    public async Task NonePolicy_ConditionalExtensionMethod_ExercisesParseExpressionTechDebt()
    {
        // This test exercises the ParseExpression tech debt in InvocationRewriter (lines 79-81)
        // when NullConditionalRewrite = None and we have person.Address?.FormatAddress()
        
        // Act
        var expression = TechDebtPersonMapperNone.ToDtoExpression();
        var readable = expression.ToReadableString();
        
        // Assert - Should work despite using ParseExpression internally
        await Assert.That(readable).Contains("person.Address");
        await Assert.That(readable).Contains("FormattedAddress");
        
        Console.WriteLine("None Policy Expression (uses ParseExpression tech debt):");
        Console.WriteLine(readable);
    }

    [Test]
    public async Task RewritePolicy_ConditionalExtensionMethod_ExercisesBothTechDebtScenarios()
    {
        // This test exercises both tech debt scenarios:
        // 1. InvocationRewriter ParseExpression (lines 79-81)
        // 2. NullConditionalRewriter ParseExpression patching (line 45)
        
        // Act
        var expression = TechDebtPersonMapperRewrite.ToDtoExpression();
        var readable = expression.ToReadableString();
        
        // Assert - Should contain proper null checks despite using ParseExpression internally
        await Assert.That(readable).Contains("person.Address != null");
        await Assert.That(readable).Contains("FormattedAddress");
        
        Console.WriteLine("Rewrite Policy Expression (uses ParseExpression tech debt in both locations):");
        Console.WriteLine(readable);
    }

    [Test]
    public async Task IgnorePolicy_ConditionalExtensionMethod_ExercisesParseExpressionTechDebt()
    {
        // This test exercises the ParseExpression tech debt in InvocationRewriter (lines 79-81)
        
        // Act
        var expression = TechDebtPersonMapperIgnore.ToDtoExpression();
        var readable = expression.ToReadableString();
        
        // Assert - Should work without conditional operators
        await Assert.That(readable).Contains("person.Address");
        await Assert.That(readable).Contains("FormattedAddress");
        
        Console.WriteLine("Ignore Policy Expression (uses ParseExpression tech debt):");
        Console.WriteLine(readable);
    }

    [Test]
    public async Task All_Policies_Should_Work_In_Memory()
    {
        // Arrange
        var personWithAddress = new TechDebtTestPerson
        {
            Name = "John Doe",
            Address = new TechDebtTestAddress
            {
                Street = "123 Main St",
                City = "New York"
            }
        };

        var personWithoutAddress = new TechDebtTestPerson
        {
            Name = "Jane Doe", 
            Address = null
        };

        // Act & Assert - None policy (conditional access preserved)
        var noneResult1 = TechDebtPersonMapperNone.ToDto(personWithAddress);
        var noneResult2 = TechDebtPersonMapperNone.ToDto(personWithoutAddress);
        
        await Assert.That(noneResult1.AddressStr).IsEqualTo("123 Main St, New York");
        await Assert.That(noneResult2.AddressStr).IsEqualTo("No Address");

        // Act & Assert - Rewrite policy (explicit null checks)
        var rewriteResult1 = TechDebtPersonMapperRewrite.ToDto(personWithAddress);
        var rewriteResult2 = TechDebtPersonMapperRewrite.ToDto(personWithoutAddress);
        
        await Assert.That(rewriteResult1.AddressStr).IsEqualTo("123 Main St, New York");
        await Assert.That(rewriteResult2.AddressStr).IsEqualTo("No Address");

        // Act & Assert - Ignore policy (should handle null gracefully due to coalesce)
        var ignoreResult1 = TechDebtPersonMapperIgnore.ToDto(personWithAddress);
        var ignoreResult2 = TechDebtPersonMapperIgnore.ToDto(personWithoutAddress);
        
        await Assert.That(ignoreResult1.AddressStr).IsEqualTo("123 Main St, New York");
        // With the null coalesce operator, this should return "No Address" instead of throwing
        await Assert.That(ignoreResult2.AddressStr).IsEqualTo("No Address");
    }

    [Test]
    public async Task TechDebt_Documentation_Test()
    {
        // This test documents the current tech debt for future reference
        // It demonstrates that the current implementation works but uses ParseExpression internally
        // which is not ideal for AST manipulation
        
        var noneExpression = TechDebtPersonMapperNone.ToDtoExpression();
        var rewriteExpression = TechDebtPersonMapperRewrite.ToDtoExpression();
        var ignoreExpression = TechDebtPersonMapperIgnore.ToDtoExpression();
        
        var noneReadable = noneExpression.ToReadableString();
        var rewriteReadable = rewriteExpression.ToReadableString();
        var ignoreReadable = ignoreExpression.ToReadableString();
        
        // Document that all expressions should work
        await Assert.That(noneReadable).IsNotEmpty();
        await Assert.That(rewriteReadable).IsNotEmpty(); 
        await Assert.That(ignoreReadable).IsNotEmpty();
        
        Console.WriteLine("=== TECH DEBT DOCUMENTATION ===");
        Console.WriteLine("The following expressions are generated using ParseExpression tech debt:");
        Console.WriteLine();
        Console.WriteLine($"None Policy: {noneReadable}");
        Console.WriteLine();
        Console.WriteLine($"Rewrite Policy: {rewriteReadable}");
        Console.WriteLine();
        Console.WriteLine($"Ignore Policy: {ignoreReadable}");
        Console.WriteLine();
        Console.WriteLine("FUTURE TODO: Replace ParseExpression usage with proper AST construction methods");
        Console.WriteLine("Location 1: InliningResolver.InvocationRewriter.cs lines 79-81");
        Console.WriteLine("Location 2: InliningResolver.NullConditionalRewriter.cs line 45");
    }
}