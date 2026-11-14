using AlephMapper;
using static System.Math;

public class Source
{
    public string Name { get; set; } = "";
    public int Number { get; set; }
    public int[] Items { get; set; } = [];
}
public class OtherSource { public string Name { get; set; } = ""; public int Number { get; set; } }
public class Destination { public string Name { get; set; } = ""; public int Number { get; set; } }
public class OtherDestination { public string Name { get; set; } = ""; public int Number { get; set; } }
public class Animal { public int Score { get; set; } }
public class Dog : Animal { }

// Keep all active diagnostic IDs exercised by a real package consumer.
public static class InvalidMapper
{
    [Projectable] public static int Map(int value) => value;
}

public static partial class ConflictingMapper
{
    [Projectable] public static int Map(int value) => value;
    public static int MapExpression() => 0;
}

public static partial class StaticImportMapper
{
    [Projectable] public static int Absolute(int value) => Abs(value);
}

public partial class FirstContainer
{
    public static partial class Mapper
    {
        [Projectable] public static int Map(int value) => value + 1;
    }
}

public partial class SecondContainer
{
    public static partial class Mapper
    {
        [Projectable] public static int Map(int value) => value + 2;
    }
}

public static partial class Mapper
{
    [Projectable, Updatable]
    public static Destination Map(Source source) => new() { Name = source.Name, Number = source.Number };

    [Adapt(typeof(OtherSource), typeof(OtherDestination), Name = "MapOther")]
    public static Destination AdaptTemplate(Source source) => new() { Name = source.Name, Number = source.Number };

    [Projectable] public static int Grade(int? value) => value switch
    { null => -1, >= 3 and <= 5 => 2, > 0 => 1, _ => 0 };
    [Projectable] public static int TypeRank(Animal? value) => value switch
    { Dog dog when dog.Score > 2 => dog.Score, Animal animal => animal.Score, _ => -1 };
    [Projectable] public static int Property(Source? value) => value switch
    { { Number: > 0, Name: not null } => value.Number, _ => -1 };
    [Projectable] public static int Slice(int[]? values) => values switch
    { [var first, .., var last] when first > 0 => first + last, [] => 0, _ => -1 };
    [Projectable] public static int Helper(Source value) => Twice(value.Number + 1) * 2;
    private static int Twice(int n) => n + n;
    [Projectable] public static int ClassicExtension(this int n) => n * 3;
    [Projectable] public static int ThroughClassic(Source value) => value.Number.ClassicExtension();
    [Projectable] public static int NonExhaustive(int value) => value switch { 1 => 1 };
}

#if EXTENSION_MEMBERS
public static partial class UnsupportedExtensionMapper
{
    extension(int value)
    {
        [Projectable] public int BlockGetter { get { return value; } }
    }
}

public static partial class Extensions
{
    extension(int value)
    {
        [Projectable] public int Squared => value * value;
        [Projectable] public int Add(int amount) => value + amount;
    }
    extension(Source source)
    {
        [Projectable, Updatable]
        [Adapt(typeof(OtherSource), typeof(OtherDestination), Name = "CopyOther",
            Generate = AdaptGeneration.Map | AdaptGeneration.Expression | AdaptGeneration.Update)]
        public Destination Copy(string prefix) => new() { Name = prefix + source.Name, Number = source.Number };
    }
    [Projectable] public static int Composed(Source value) => value.Number.Squared + value.Number.Add(3);
}
#endif

public static class Program
{
    private static void Equal<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }

    public static void Main()
    {
        foreach (var n in new[] { -1, 0, 1, 3, 5, 6 })
        {
            var source = new Source { Name = "Ada", Number = n, Items = [1, 2, 3] };
            var mapped = Mapper.MapExpression().Compile()(source);
            Equal(mapped.Name, Mapper.Map(source).Name);
            Equal(mapped.Number, n);
            var destination = new Destination();
            Mapper.Map(source, destination);
            Equal(destination.Number, n);
            Equal(destination.Name, "Ada");
            var other = Mapper.MapOtherExpression().Compile()(new OtherSource { Name = "Grace", Number = n });
            Equal(other.Name, "Grace");
            Equal(other.Number, n);
            Equal(Mapper.GradeExpression().Compile()(n), Mapper.Grade(n));
            Equal(Mapper.PropertyExpression().Compile()(source), Mapper.Property(source));
            Equal(Mapper.HelperExpression().Compile()(source), Mapper.Helper(source));
            Equal(Mapper.ThroughClassicExpression().Compile()(source), Mapper.ThroughClassic(source));
#if EXTENSION_MEMBERS
            Equal(Extensions.SquaredExpression().Compile()(n), n.Squared);
            Equal(Extensions.AddExpression(3).Compile()(n), n.Add(3));
            Equal(Extensions.ComposedExpression().Compile()(source), Extensions.Composed(source));
            var copied = new Destination();
            Equal(ReferenceEquals(Extensions.Copy(source, "!", copied), copied), true);
            Equal(copied.Name, "!Ada");
            Equal(copied.Number, n);
            Equal(Extensions.CopyExpression("!").Compile()(source).Name, "!Ada");
            var otherSource = new OtherSource { Name = "Grace", Number = n };
            var copiedOther = Extensions.CopyOther(otherSource, "?");
            Equal(copiedOther.Name, "?Grace");
            Equal(Extensions.CopyOtherExpression("?").Compile()(otherSource).Name, "?Grace");
            var otherDestination = new OtherDestination();
            Equal(ReferenceEquals(Extensions.CopyOther(otherSource, "!", otherDestination), otherDestination), true);
            Equal(otherDestination.Name, "!Grace");
            Equal(otherDestination.Number, n);
#endif
        }
        Equal(Mapper.GradeExpression().Compile()(null), Mapper.Grade(null));
        Equal(Mapper.PropertyExpression().Compile()(null), Mapper.Property(null));
        foreach (var value in new Animal?[] { null, new Animal { Score = 1 }, new Dog { Score = 4 } })
            Equal(Mapper.TypeRankExpression().Compile()(value), Mapper.TypeRank(value));
        foreach (var values in new int[]?[] { null, [], [1], [1, 3], [1, 2, 3], [-1, 3] })
            Equal(Mapper.SliceExpression().Compile()(values), Mapper.Slice(values));
        Equal(StaticImportMapper.AbsoluteExpression().Compile()(-7), StaticImportMapper.Absolute(-7));
        Equal(FirstContainer.Mapper.MapExpression().Compile()(4), 5);
        Equal(SecondContainer.Mapper.MapExpression().Compile()(4), 6);
        Console.WriteLine("Package compatibility runtime checks passed.");
    }
}
