namespace AlephMapper.Tests;

public class CircularPropertyDependencyTests
{
    [Test]
    public async Task Updatable_With_Circular_Property_Types_Does_Not_Overflow()
    {
        var source = new CircularPropsSource { Name = "John" };
        var dest = new TypeA();

        // Verify generated Updatable overload exists
        var method = typeof(CircularPropertyMapper).GetMethod(
            nameof(CircularPropertyMapper.UpdateTypeA), [typeof(CircularPropsSource), typeof(TypeA)]);
        await Assert.That(method).IsNotNull();

        // Call the generated Updatable method and ensure it returns the same dest
        var result = CircularPropertyMapper.UpdateTypeA(source, dest);
        await Assert.That(result).IsSameReferenceAs(dest);

        // Basic sanity to ensure mapping worked and generation succeeded
        await Assert.That(dest.Name).IsEqualTo("John");
        await Assert.That(dest.B).IsNotNull();
        await Assert.That(dest.B!.A).IsNotNull();
        await Assert.That(dest.B!.A!.Name).IsEqualTo("John");
    }
}

public class CircularPropsSource
{
    public string Name { get; set; } = string.Empty;
}

public class TypeA
{
    public string Name { get; set; } = string.Empty;
    public TypeB? B { get; set; }
}

public class TypeB
{
    public TypeA? A { get; set; }
}

[Expressive]
internal static partial class CircularPropertyMapper
{
    [Updatable]
    public static TypeA UpdateTypeA(CircularPropsSource source) => new TypeA
    {
        Name = source?.Name,
        B = new TypeB
        {
            // Circular type reference back to TypeA
            A = new TypeA
            {
                Name = source?.Name
            }
        }
    };
}

