# Explicit Adapt and troubleshooting

## Reuse an existing mapping as an explicit template

`[Adapt]` belongs on a mapping method and declares a specific compatible source/destination pair. It does **not** search for compatible classes automatically.

~~~csharp
using AlephMapper;

public static partial class PersonMapper
{
    [Adapt(
        typeof(Employee),
        typeof(EmployeeDto),
        Name = "MapEmployee",
        Generate = AdaptGeneration.Map | AdaptGeneration.Expression)]
    public static PersonDto MapPerson(Person source) => new()
    {
        Id = source.Id,
        Name = source.FirstName + " " + source.LastName
    };
}

// Generated when the adapted members/types are compatible:
var one = PersonMapper.MapEmployee(employee);
var rows = await db.Employees
    .Select(PersonMapper.MapEmployeeExpression())
    .ToListAsync();
~~~

Here `Employee` and `EmployeeDto` must expose the members actually used by the template, with compatible construction and assignments. `Name` is mandatory when `Generate` includes `Expression`. Default flags are `Map | Expression`; explicitly add `Update` when wanted. Extra mapping parameters are preserved; check generated factory signatures. Adaptation of generic mapping methods is unsupported (`AM0018`). See repository `docs/Adapt-Attribute.md` for detailed validation constraints.

## Diagnostic-led troubleshooting

| Symptom | First things to check |
|---|---|
| Missing generated companion | `static partial` containing class, `static` expression-bodied method, source argument, applied method/class attribute, installed analyzer and compiler compatibility |
| Generated query fails EF translation | Inspect `MapExpression()`'s generated expression; verify provider support. This is distinct from C# compilation. |
| Missing adapted member | `AM0005`–`AM0015`: inspect explicitly requested types, accessed source-member paths, destination writable members, constructor compatibility, generated name/signature conflicts and `Name` requirements |
| Helper recursion or unsupported conditional receiver | `AM0002`, `AM0003`, `AM0013` for circular composition; `AM0016` for unsafe null-conditional receiver; `AM0017` for unsupported null-conditional policy |
| Generated member names collide | Inspect declared mapper members and other generated adaptations; do not invent fallback suffixes |

## Inspect generated files

In the consuming project's `.csproj`, temporarily enable:

~~~xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
~~~

Read the actual emitted C# to confirm inlining, argument capture, nullable behavior, adaptation, and destination updates. Avoid treating the source generator as a runtime service; generated mappings compile into the consuming application.
