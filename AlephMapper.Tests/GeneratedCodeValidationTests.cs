namespace AlephMapper.Tests;

public class GeneratedCodeValidationTests
{
    [Test]
    public async Task Generated_Expressions_Should_Not_Contain_Null_Conditional_Operators()
    {
        // This test verifies that the generated expressions compile successfully
        // without CS8072 errors (null propagating operators in expression trees)

        // These expressions would have failed before our null conditional rewrite implementation
        var bornInKyivExpression = Mapper.BornInKyivExpression();
        var bornInKyivAndOlder35Expression = Mapper.BornInKyivAndOlder35Expression();

        // Assert that we can compile them (this would fail with CS8072 before our fix)
        var bornInKyivCompiled = bornInKyivExpression.Compile();
        var bornInKyivAndOlder35Compiled = bornInKyivAndOlder35Expression.Compile();

        // Output the expression trees for inspection
        Console.WriteLine("BornInKyiv Expression:");
        Console.WriteLine(bornInKyivExpression.ToString());
        Console.WriteLine("");

        Console.WriteLine("LivesInKyivAndOlder35 Expression:");
        Console.WriteLine(bornInKyivAndOlder35Expression.ToString());
        Console.WriteLine("");

        // Verify they work correctly with non-null values
        var testBirthInfo = new BirthInfo { Age = 40, Address = "Kyiv" };
        var testSourceDto = new SourceDto { BirthInfo = testBirthInfo };

        await Assert.That(bornInKyivCompiled(testBirthInfo)).IsTrue();
        await Assert.That(bornInKyivAndOlder35Compiled(testSourceDto)).IsTrue();

        // With Ignore policy (now default), null values will throw NullReferenceException
        // This is expected behavior for the Ignore policy
        await Assert.That(() => bornInKyivCompiled(null)).Throws<NullReferenceException>();

        var testSourceDtoWithNullBirthInfo = new SourceDto { BirthInfo = null };
        await Assert.That(() => bornInKyivAndOlder35Compiled(testSourceDtoWithNullBirthInfo)).Throws<NullReferenceException>();
    }

    [Test]
    public async Task Different_Null_Conditional_Policies_Should_Generate_Different_Expressions()
    {
        // Test expressions with different policies
        var ignoreMapperExpression = IgnoreMapper.GetAddressExpression();
        var rewriteMapperExpression = RewriteMapper.GetAddressExpression();

        Console.WriteLine("Ignore Policy Expression:");
        Console.WriteLine(ignoreMapperExpression.ToString());
        Console.WriteLine("");

        Console.WriteLine("Rewrite Policy Expression:");
        Console.WriteLine(rewriteMapperExpression.ToString());
        Console.WriteLine("");

        // Both should compile successfully
        var ignoreCompiled = ignoreMapperExpression.Compile();
        var rewriteCompiled = rewriteMapperExpression.Compile();

        // Test with valid data - both should work the same
        var sourceWithBirthInfo = new SourceDto
        {
            Name = "Test",
            BirthInfo = new BirthInfo { Address = "Test Address" }
        };

        await Assert.That(ignoreCompiled(sourceWithBirthInfo)).IsEqualTo("Test Address");
        await Assert.That(rewriteCompiled(sourceWithBirthInfo)).IsEqualTo("Test Address");

        // Test with null data - behavior should differ
        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "Test",
            BirthInfo = null
        };

        // Ignore policy should throw NullReferenceException
        await Assert.That(() => ignoreCompiled(sourceWithNullBirthInfo)).Throws<NullReferenceException>();

        // Rewrite policy should return default value
        await Assert.That(rewriteCompiled(sourceWithNullBirthInfo)).IsEqualTo("Unknown");
    }

    [Test]
    public async Task Expression_Trees_Should_Be_Valid_For_EF_Core()
    {
        // This test verifies that generated expressions can be used in EF Core queries
        // by checking that they represent valid expression tree structures

        var personNameExpression = EfCoreMapper.GetPersonNameExpression();
        var birthPlaceExpression = EfCoreMapper.GetBirthPlaceExpression();

        Console.WriteLine("Person Name Expression:");
        Console.WriteLine(personNameExpression.ToString());
        Console.WriteLine("");

        Console.WriteLine("Birth Place Expression:");
        Console.WriteLine(birthPlaceExpression.ToString());
        Console.WriteLine("");

        // These expressions should compile
        var nameCompiled = personNameExpression.Compile();
        var birthPlaceCompiled = birthPlaceExpression.Compile();

        // Test with mock data
        var person = new Person
        {
            Name = "John Doe",
            BirthInfo = new PersonBirthInfo { BirthPlace = "Kyiv" }
        };

        await Assert.That(nameCompiled(person)).IsEqualTo("John Doe");
        await Assert.That(birthPlaceCompiled(person)).IsEqualTo("Kyiv");

        // Test with null birth info
        var personWithNullBirthInfo = new Person
        {
            Name = "Jane Smith",
            BirthInfo = null
        };

        await Assert.That(nameCompiled(personWithNullBirthInfo)).IsEqualTo("Jane Smith");
        await Assert.That(birthPlaceCompiled(personWithNullBirthInfo)).IsEqualTo("Unknown");
    }

    [Test]
    public async Task Updatable_Methods_Should_Inline_Method_Calls()
    {
        // This test verifies that Updatable methods properly inline method calls
        // We'll check the functionality by ensuring the inlined code works properly

        var source = new SourceDto
        {
            Name = "John Doe",
            BirthInfo = new BirthInfo { Age = 30, Address = "Kyiv" },
            Email = "john@example.com"
        };

        var dest = new DestDto();

        // Call the generated update method
        var result = Mapper.MapToDestDto(source, dest);

        // Verify the method worked correctly (including inlined method call)
        await Assert.That(result).IsSameReferenceAs(dest);
        await Assert.That(dest.Name).IsEqualTo("John Doe");
        await Assert.That(dest.ContactInfo).IsEqualTo("john@example.com");
        await Assert.That(dest.BirthInfo).IsNotNull();
        await Assert.That(dest.BirthInfo!.Age).IsEqualTo(30);
        await Assert.That(dest.BirthInfo.Address).IsEqualTo("Kyiv");

        // Test with null BirthInfo to ensure null handling works in inlined code
        var sourceWithNullBirthInfo = new SourceDto
        {
            Name = "Jane Doe",
            BirthInfo = null,
            Email = "jane@example.com"
        };

        var dest2 = new DestDto();
        var result2 = Mapper.MapToDestDto(sourceWithNullBirthInfo, dest2);

        await Assert.That(result2).IsSameReferenceAs(dest2);
        await Assert.That(dest2.Name).IsEqualTo("Jane Doe");
        await Assert.That(dest2.ContactInfo).IsEqualTo("jane@example.com");
        await Assert.That(dest2.BirthInfo).IsNull();
    }

    [Test]
    public async Task Generated_Methods_Should_Have_Proper_Attributes()
    {
        // Verify that generated classes have the proper attributes
        var mapperType = typeof(Mapper);
        var rewriteMapperType = typeof(RewriteMapper);
        var ignoreMapperType = typeof(IgnoreMapper);

        // Check for GeneratedCode attributes
        var mapperGeneratedAttributes = mapperType.GetCustomAttributes(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), false);
        var rewriteGeneratedAttributes = rewriteMapperType.GetCustomAttributes(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), false);
        var ignoreGeneratedAttributes = ignoreMapperType.GetCustomAttributes(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute), false);

        await Assert.That(mapperGeneratedAttributes.Length).IsGreaterThan(0);
        await Assert.That(rewriteGeneratedAttributes.Length).IsGreaterThan(0);
        await Assert.That(ignoreGeneratedAttributes.Length).IsGreaterThan(0);

        // Verify the tool and version information
        var mapperGenAttr = (System.CodeDom.Compiler.GeneratedCodeAttribute)mapperGeneratedAttributes[0];
        await Assert.That(mapperGenAttr.Tool).IsEqualTo("AlephMapper");
        await Assert.That(mapperGenAttr.Version).IsEqualTo("0.4.4");
    }
}