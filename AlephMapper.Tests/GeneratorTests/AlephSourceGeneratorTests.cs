namespace AlephMapper.Tests.GeneratorTests;

public class AlephSourceGeneratorTests
{
    [Test]
    public async Task Attributes_are_emitted_even_without_mappers()
    {
        const string source = """
using System;

namespace Tests;

public class Plain
{
    public string Name { get; set; } = string.Empty;
}
""";

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource()
            .RunAsync();
    }

    [Test]
    public async Task Expressive_mappers_receive_expression_companions()
    {
        const string source = """
using AlephMapper;

namespace Tests;

[Expressive]
public static partial class SampleMapper
{
    public static string ProjectName(SampleSource source) => source.Name;
}

public class SampleSource
{
    public string Name { get; set; } = string.Empty;
}
""";

        const string generated = """
using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace Tests;

[GeneratedCode("AlephMapper", "0.4.0")]
partial class SampleMapper
{
  /// <summary>
  /// This is an auto-generated expression companion for <see cref="ProjectName(SampleSource)"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
  /// </para>
  /// </remarks>
  public static Expression<Func<SampleSource, string>> ProjectNameExpression() => 
      source => source.Name;

}

""";

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource()
            .ExpectGeneratedSource("Tests_SampleMapper_GeneratedMappings.g.cs", generated)
            .RunAsync();
    }

    [Test]
    public async Task Updatable_mappers_generate_update_helpers()
    {
        const string source = """
using AlephMapper;

namespace Tests;

[Updatable]
public static partial class SampleMapper
{
    public static Destination Map(Source source) => new Destination
    {
        Name = source.Name,
        Age = source.Age
    };
}

public class Source
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class Destination
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
""";

        const string generated = """
using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace Tests;

[GeneratedCode("AlephMapper", "0.4.0")]
partial class SampleMapper
{
  /// <summary>
  /// Updates an existing instance of <see cref="Destination"/> with values from the source object.
  /// </summary>
  /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
  /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
  /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
  public static Destination Map(Source source, Destination dest)
  {
    if (source == null) return dest;
    if (dest == null)
        dest = new Destination();
    dest.Name = source.Name;
    dest.Age = source.Age;
    return dest;
  }
}

""";

        await AlephSourceGeneratorVerifier
            .CreateTest(source)
            .ExpectAttributesSource()
            .ExpectGeneratedSource("Tests_SampleMapper_GeneratedMappings.g.cs", generated)
            .RunAsync();
    }
}
