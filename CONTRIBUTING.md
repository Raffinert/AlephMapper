# Contributing to AlephMapper

## Prerequisites and local build

Use the .NET SDK selected by [`global.json`](global.json) (`10.0.400` with `latestFeature` roll-forward). Python 3 is needed for the compiler/package verifier and documentation link check. Restore may download NuGet dependencies. From the repository root:

```powershell
dotnet restore AlephMapper.slnx
dotnet build AlephMapper.slnx --configuration Release --no-restore
dotnet test AlephMapper.slnx --configuration Release --no-build --no-restore
dotnet run --project examples/SampleApp/SampleApp.csproj --configuration Release
dotnet run --project examples/Documentation/Documentation.csproj --configuration Release
python scripts/verify_docs.py
```

The solution uses Microsoft.Testing.Platform with TUnit tests; `dotnet test` invokes that runner through the SDK's `global.json` configuration. For a focused test, use the test executable's `--treenode-filter` argument, for example:

```powershell
dotnet test tests/AlephMapper.Tests/AlephMapper.Tests.csproj --configuration Release --no-build --no-restore -- --treenode-filter "/*/*/SourceGeneratorTests/*"
```

## Where to work

[`source/AlephSourceGenerator.cs`](source/AlephSourceGenerator.cs) registers the incremental pipeline. [`source/Generation`](source/Generation) discovers mapper candidates, builds semantic analyses, coordinates rewriters and emitters, and returns generated source and diagnostics. [`source/SyntaxRewriters`](source/SyntaxRewriters) handles expression expansion; [`source/Adaptation`](source/Adaptation) validates and rewrites explicit type substitutions. [`source/Diagnostics`](source/Diagnostics) owns stable diagnostic descriptors. Read the [architecture overview](docs/architecture.md) before changing pipeline boundaries.

[`tests/AlephMapper.Tests`](tests/AlephMapper.Tests) contains generator snapshots and focused diagnostics/compilation tests. [`tests/AlephMapper.IntegrationTests`](tests/AlephMapper.IntegrationTests) tests runtime and SQLite behavior. [`tests/PackageCompatibility`](tests/PackageCompatibility) is run under several compiler versions by [`scripts/verify_roslyn_compatibility.py`](scripts/verify_roslyn_compatibility.py). [`examples/SampleApp`](examples/SampleApp) is a manual walkthrough; [`examples/Documentation`](examples/Documentation) is the executable source for [API snippets](docs/generated-api-reference.md).

## Add a regression

1. Reduce the defect to the smallest mapper declaration and models that reproduce it.
2. Add a generator test under `tests/AlephMapper.Tests` or a `Files/<Case>/Sources` fixture with the expected generated source. Check diagnostics and the final C# compilation, not just source text. Look at `SourceGeneratorTests.cs` and the existing fixture-based cases for the local pattern.
3. Add an integration test when runtime behavior, EF Core translation, or SQLite SQL behavior matters. A generated tree compiling does not establish provider translation.
4. If a change affects analyzer selection or the public generated API, pack to a fresh directory and run the compiler matrix:

```powershell
dotnet pack source/AlephMapper.csproj --configuration Release --output artifacts/oss-quality-docs-package
python scripts/verify_roslyn_compatibility.py --package-dir artifacts/oss-quality-docs-package
```

The verifier expects exactly one matching `.nupkg` in that directory and may download compiler packages. Use a new output directory or remove only the old artifacts you created there before rerunning. The package contains Roslyn 4.8 and 5.0 analyzer bands; the matrix checks real compiler processes, package selection, and runtime behavior.

To inspect emitted code in a consuming project, add `EmitCompilerGeneratedFiles=true` and set `CompilerGeneratedFilesOutputPath` as shown in [advanced usage](docs/advanced-usage.md#inspect-generated-code). Build, then inspect the generated `AlephMapper` files beneath the configured `obj` path.

If editing the documentation fixture, run `python scripts/verify_docs.py --write` to copy marked source regions into the API reference, then run the plain command to validate snippets and links. The fixture compiles with C# 14; classic APIs are also exercised by the multi-compiler package matrix.

## Report a bug

Use [GitHub Issues](https://github.com/Raffinert/AlephMapper/issues). Include the AlephMapper package/source version, .NET SDK and compiler version, target framework, a minimal mapper and model, all `AMxxxx` diagnostics, and the expected versus actual generated API. For EF Core issues, add the EF Core and provider versions plus the provider exception and a minimal query. Avoid posting private data or credentials in a public issue.

For release and support details, see [releasing and support](docs/releasing-and-support.md).
