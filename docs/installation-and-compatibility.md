# Installation and compatibility

The root [README](../README.md) contains the quick start. This page covers package-management and compiler-host details. Version `0.8.3` below is the version declared by this source tree; check the [NuGet package page](https://www.nuget.org/packages/AlephMapper) before installing from the public feed.

## Install AlephMapper

```shell
dotnet add package AlephMapper
```

AlephMapper is a compile-time source generator. If you reference it from a library, keep its analyzer assets local instead of exposing the package transitively to library consumers.

With a regular `PackageReference`:

```xml
<PackageReference Include="AlephMapper" Version="0.8.3">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

With Central Package Management:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="AlephMapper" Version="0.8.3" />

<!-- Your project file -->
<PackageReference Include="AlephMapper">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

When using the generator directly from source:

```xml
<ProjectReference Include="..\path\to\AlephMapper.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

`PrivateAssets="all"` prevents the dependency from flowing transitively; `IncludeAssets` makes the analyzer/source-generator assets available during compilation.

## Compiler compatibility

| Compiler host | Support |
| --- | --- |
| Roslyn 4.8 through 4.x | Classic mapping methods, generated projections, updates, adaptations, and supported switch lowering |
| Roslyn 5.0+ | All classic features plus supported C# 14 extension members |

The NuGet package contains two versioned analyzer assets; the compatible one is selected automatically by the compiler host, independently of the target framework. The analyzers target `netstandard2.0`. C# 14 extension-member source still requires a compiler and language version that supports C# 14.

Hosts older than Roslyn 4.8 are not supported. For coverage and package verification, see the [multiversion implementation evidence](roslyn-multiversion-plan.md) and [extension compatibility matrix](extension-members-compatibility-matrix.md).

## Migrating from older attributes

`[Expressive]` was renamed to `[Projectable]`. Update attribute usages directly; no compatibility alias is generated.

```csharp
// Before
[Expressive]
public static PersonDto Map(Person person) => new();

// After
[Projectable]
public static PersonDto Map(Person person) => new();
```

See the [README](../README.md) for the quick start, the [generated API reference](generated-api-reference.md) for companion signatures, and [advanced usage](advanced-usage.md) for null handling.
