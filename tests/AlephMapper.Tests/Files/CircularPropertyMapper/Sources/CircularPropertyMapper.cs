using System;

namespace AlephMapper.Tests;

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