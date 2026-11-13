# Generator performance baseline

Run from the repository root:

```shell
dotnet run --project benchmarks/GeneratorBenchmark/GeneratorBenchmark.csproj -c Release
```

The runner prints SDK/Roslyn versions, fixture sizes in source, elapsed generator-driver time, bytes allocated on the current thread, generated character count, and tracked source-output step reasons. It measures cold generation, unrelated edits, mapper edits, helper edits, 50 mapper classes, one mapper with 50 methods, and two nested mappers with colliding short names.

This is a local diagnostic tool, not a CI timing assertion. Run it several times after a warm-up build on the same machine and compare medians. An `Unchanged` output reason means the generated text was stable; it does not prove that all upstream analysis was skipped. The numbers include driver overhead and exclude initial compilation construction.

## Initial measurement

One Windows run on .NET runtime 10.0.14 and Roslyn 5.9.0, Release configuration, produced these illustrative values. The first cold run includes JIT and framework initialization. Edit values below are the median of iterations 2 through 5, which avoids the first transition cost.

| Scenario | Time | Current-thread allocation | Output observation |
| --- | ---: | ---: | --- |
| Cold single mapper | 583 ms | 11.8 MB | `New:2` |
| Unrelated edit | 2.1 ms | 205 KB | `Cached:2` |
| Mapping edit | 2.1 ms | 212 KB | `Modified:2` |
| Helper edit | 1.9 ms | 212 KB | `Modified:2` |
| Cold 50 mapper classes | 33 ms | 2.4 MB | `New:100` |
| Cold one class with 50 methods | 51 ms | 8.3 MB | `New:100` |
| Cold nested name collision | 132 ms | 33.3 MB | `New:4` |

The 50-method class allocated more than 50 separate classes in this sample. Collision handling includes the first use of hashing and may pay additional JIT costs. These are leads for profiling, not evidence of regressions or reasons to refactor the pipeline yet. The measurement has no pre-change comparison, includes JIT effects in cold runs, and reports current-thread allocations only. Repeat across several runs and inspect upstream tracked steps before choosing an optimization.
