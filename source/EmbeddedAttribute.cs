using System;

namespace Microsoft.CodeAnalysis;

// The generator host injects the real definition into consuming compilations
// through AddEmbeddedAttributeDefinition. This shim only lets Attributes.cs be
// compiled into the generator assembly before it is embedded as source.
[AttributeUsage(AttributeTargets.All)]
internal sealed class EmbeddedAttribute : Attribute
{
}
