# Sample application

This console application demonstrates handwritten expression-bodied mappings and AlephMapper-generated projection, update, and adaptation companions over in-memory models. It targets `net10.0` and uses the SDK selected by [`global.json`](../../global.json) (10.0.400 with `latestFeature` roll-forward). The project does not set a separate C# language version; the installed SDK supplies its default. No database or external service is required.

From the repository root:

```powershell
dotnet build examples/SampleApp/SampleApp.csproj --configuration Release
dotnet run --project examples/SampleApp/SampleApp.csproj --configuration Release
```

The run prints sections for basic/reverse mappings, order calculations, helper inlining, a generated expression, an update, and explicit `[Adapt]` mapping, ending with `=== Mapping Demonstration Complete ===`. Dates, currencies, and expression formatting can vary with time and locale.

[`SampleApp.csproj`](SampleApp.csproj) references `source/AlephMapper.csproj` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`, so it runs against the generator in this checkout rather than a published NuGet package. For a smaller runnable API fixture and the generated signatures, see the [generated API reference](../../docs/generated-api-reference.md).
