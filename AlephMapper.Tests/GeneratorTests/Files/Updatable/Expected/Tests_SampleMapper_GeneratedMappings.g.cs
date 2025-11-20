using AlephMapper;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace Tests;

[GeneratedCode("AlephMapper", "0.4.1")]
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
