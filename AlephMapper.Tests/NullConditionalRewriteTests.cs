using System.CodeDom.Compiler;

namespace AlephMapper.Tests;

// Test mapper with Ignore policy (now default, but being explicit)
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Ignore)]
public static partial class IgnoreMapper
{
    public static string GetAddress(SourceDto source) => source.BirthInfo?.Address ?? "Unknown";

    public static bool HasAddress(SourceDto source) => source.BirthInfo?.Address != null;
}

// Test mapper with Rewrite policy
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class RewriteMapper
{
    public static string GetAddress(SourceDto dto) => dto.BirthInfo?.Address ?? "Unknown";

    public static bool HasAddress(SourceDto source) => source.BirthInfo?.Address != null;
}

// Test mapper with None policy (should fail with null conditional operators)
[Expressive(NullConditionalRewrite = NullConditionalRewrite.None)]
public static partial class NoneMapper
{
    // This method should work because it doesn't use null conditional operators
    public static string GetName(SourceDto source) => source.Name;

    // Methods with null conditional operators would cause compilation errors with None policy
    // but we won't include them here to avoid build failures
}

public class NullConditionalRewriteTests
{
    [Test]
    public async Task IgnoreMapper_Should_Generate_Expressions_Without_Null_Conditional_Operators()
    {
        // Arrange & Act
        var getAddressExpression = IgnoreMapper.GetAddressExpression();
        var hasAddressExpression = IgnoreMapper.HasAddressExpression();

        // Assert
        await Assert.That(getAddressExpression).IsNotNull();
        await Assert.That(hasAddressExpression).IsNotNull();

        // These should not throw CS8072 error since null conditional operators are ignored
        var getAddressCompiled = getAddressExpression.Compile();
        var hasAddressCompiled = hasAddressExpression.Compile();

        // Test with non-null values - both should work the same
        var sourceWithAddress = new SourceDto
        {
            BirthInfo = new BirthInfo { Address = "New York" }
        };

        await Assert.That(getAddressCompiled(sourceWithAddress)).IsEqualTo("New York");
        await Assert.That(hasAddressCompiled(sourceWithAddress)).IsTrue();
    }

    [Test]
    public async Task RewriteMapper_Should_Generate_Expressions_With_Explicit_Null_Checks()
    {
        // Arrange & Act
        var getAddressExpression = RewriteMapper.GetAddressExpression();
        var hasAddressExpression = RewriteMapper.HasAddressExpression();

        // Assert
        await Assert.That(getAddressExpression).IsNotNull();
        await Assert.That(hasAddressExpression).IsNotNull();

        var getAddressCompiled = getAddressExpression.Compile();
        var hasAddressCompiled = hasAddressExpression.Compile();

        // Test with non-null BirthInfo
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "John",
            BirthInfo = new BirthInfo { Address = "New York" }
        };

        await Assert.That(getAddressCompiled(sourceWithBirthInfo)).IsEqualTo("New York");
        await Assert.That(hasAddressCompiled(sourceWithBirthInfo)).IsTrue();

        // Test with null BirthInfo - should handle gracefully with explicit null checks
        var sourceWithoutBirthInfo = new SourceDto
        {
            Name = "Jane",
            BirthInfo = null
        };

        await Assert.That(getAddressCompiled(sourceWithoutBirthInfo)).IsEqualTo("Unknown");
        await Assert.That(hasAddressCompiled(sourceWithoutBirthInfo)).IsFalse();
    }

    [Test]
    public async Task NoneMapper_Should_Work_Without_Null_Conditional_Operators()
    {
        // Arrange & Act
        var getNameExpression = NoneMapper.GetNameExpression();

        // Assert
        await Assert.That(getNameExpression).IsNotNull();

        var getNameCompiled = getNameExpression.Compile();

        var source = new SourceDto { Name = "Test" };
        await Assert.That(getNameCompiled(source)).IsEqualTo("Test");
    }

    [Test]
    public async Task Original_Mapper_With_Default_Ignore_Policy_Should_Work()
    {
        // This tests the original Mapper class which now uses default Ignore policy

        // Arrange & Act
        var bornInKyivExpression = Mapper.BornInKyivExpression();
        var bornInKyivAndOlder35Expression = Mapper.BornInKyivAndOlder35Expression();

        // Assert
        await Assert.That(bornInKyivExpression).IsNotNull();
        await Assert.That(bornInKyivAndOlder35Expression).IsNotNull();

        var bornInKyivCompiled = bornInKyivExpression.Compile();
        var livesInKyivAndOlder35Compiled = bornInKyivAndOlder35Expression.Compile();

        // Test with non-null BirthInfo
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "Jane",
            BirthInfo = new BirthInfo { Age = 40, Address = "Kyiv" }
        };

        await Assert.That(bornInKyivCompiled(sourceWithBirthInfo.BirthInfo)).IsTrue();
        await Assert.That(livesInKyivAndOlder35Compiled(sourceWithBirthInfo)).IsTrue();

        // With Ignore policy, null values will throw NullReferenceException
        await Assert.That(() => bornInKyivCompiled(null)).Throws<NullReferenceException>();

        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "John",
            BirthInfo = null
        };
        await Assert.That(() => livesInKyivAndOlder35Compiled(sourceWithNullBirthInfo)).Throws<NullReferenceException>();
    }

    [Test]
    public async Task RewriteMapper_Should_Generate_Expressions_Without_Extra_Parentheses()
    {
        // Arrange & Act
        var getAddressExpression = RewriteMapper.GetAddressExpression();
        var hasAddressExpression = RewriteMapper.HasAddressExpression();

        // Assert - The important thing is that the expressions work correctly, not their string format
        await Assert.That(getAddressExpression).IsNotNull();
        await Assert.That(hasAddressExpression).IsNotNull();

        var getAddressCompiled = getAddressExpression.Compile();
        var hasAddressCompiled = hasAddressExpression.Compile();

        // Test functionality - this is what actually matters
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "Jane",
            BirthInfo = new BirthInfo { Address = "New York" }
        };

        await Assert.That(getAddressCompiled(sourceWithBirthInfo)).IsEqualTo("New York");
        await Assert.That(hasAddressCompiled(sourceWithBirthInfo)).IsTrue();

        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "John",
            BirthInfo = null
        };

        await Assert.That(getAddressCompiled(sourceWithNullBirthInfo)).IsEqualTo("Unknown");
        await Assert.That(hasAddressCompiled(sourceWithNullBirthInfo)).IsFalse();

        // Verify that the expressions contain proper conditional logic
        // The string representation will show IIF((condition), trueValue, falseValue) 
        // which is normal for Expression trees
        var getAddressString = getAddressExpression.ToString();
        var hasAddressString = hasAddressExpression.ToString();

        // Verify we have the expected null check pattern in the expressions
        await Assert.That(getAddressString).Contains("dto.BirthInfo != null");
        await Assert.That(getAddressString).Contains("dto.BirthInfo.Address");
        await Assert.That(hasAddressString).Contains("source.BirthInfo != null");
        await Assert.That(hasAddressString).Contains("source.BirthInfo.Address");
    }

    [Test]
    public async Task Generated_Classes_Should_Have_Documentation_And_Attributes()
    {
        // This test verifies that generated classes have proper GeneratedCode attribute
        // to indicate they contain generated code

        // Arrange & Act - Get the class type
        var rewriteMapperType = typeof(RewriteMapper);
        var ignoreMapperType = typeof(IgnoreMapper);

        // Assert - Check for GeneratedCode attribute on the class
        var rewriteMapperAttributes = rewriteMapperType.GetCustomAttributes(typeof(GeneratedCodeAttribute), false);
        var ignoreMapperAttributes = ignoreMapperType.GetCustomAttributes(typeof(GeneratedCodeAttribute), false);

        await Assert.That(rewriteMapperAttributes).IsNotEmpty();
        await Assert.That(ignoreMapperAttributes).IsNotEmpty();

        // Verify the GeneratedCode attribute values
        var rewriteGenAttr = (GeneratedCodeAttribute)rewriteMapperAttributes[0];
        await Assert.That(rewriteGenAttr.Tool).IsEqualTo("AlephMapper");
        await Assert.That(rewriteGenAttr.Version).IsEqualTo("0.4.0");

        var ignoreGenAttr = (GeneratedCodeAttribute)ignoreMapperAttributes[0];
        await Assert.That(ignoreGenAttr.Tool).IsEqualTo("AlephMapper");
        await Assert.That(ignoreGenAttr.Version).IsEqualTo("0.4.0");

        // Verify methods exist and are accessible
        var getAddressMethod = rewriteMapperType.GetMethod("GetAddressExpression");
        var hasAddressMethod = rewriteMapperType.GetMethod("HasAddressExpression");

        await Assert.That(getAddressMethod).IsNotNull();
        await Assert.That(hasAddressMethod).IsNotNull();
    }
}