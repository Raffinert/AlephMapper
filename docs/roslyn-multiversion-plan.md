# Roslyn multiversion implementation plan

## Finding

The current package contains a single analyzer compiled against Roslyn5.0. Older
Roslyn4.x hosts reject it (CS9057). Targeting net8 while building with SDK10 only
proves target-framework compatibility, not older compiler-host compatibility.
The previous Roslyn4.14 baseline also excluded older4.x hosts; this change aims
to broaden classic support to4.8 rather than merely restoring4.14.

## Design and sequence

1. Reproduce the5.0-only package failure with a real4.x compiler toolset.
2. Isolate CSharp14 syntax/symbol APIs in a small compile-time compatibility shim;
   keep mapping, diagnostics and switch/inlining code shared, with no reflection
   or text-based recognition of extension syntax.
3. Build two netstandard2.0 analyzer assemblies from shared source: Roslyn4.8 and
   Roslyn5.0. Keep separate project/intermediate/output directories to avoid
   restore/build races and stale-asset reuse.
4. Pack exactly one assembly per version band at
   analyzers/dotnet/roslyn4.8/cs/AlephMapper.dll and
   analyzers/dotnet/roslyn5.0/cs/AlephMapper.dll. Remove the unversioned analyzer.
   Use SDK analyzer selection; do not ship compiler assemblies or custom loader.
5. Verify real package selection and execution under compiler toolsets4.8,4.14,
   5.0 and the installed SDK host. Assert exactly one AlephMapper analyzer is
   selected, the expected band, no CS9057/generator-loading failures, and runtime
   equivalence for classic projections/switches/helpers/updates/adaptations.
   Verify CSharp14 extension projections on5.0+.
6. Run the complete existing suite, Release/Debug build and pack; add the package
   matrix to CI, update compatibility documentation, commit and push to PR57.
   Keep PR57 unmerged pending owner approval.

## Boundaries

Classic mappings are supported on Roslyn4.8+. CSharp14 extension syntax inherently
requires a5.0+ compiler and uses the5.0 asset. Compiler hosts below4.8 are outside
this support floor. No target-framework/version-number bump or package publication.

## Primary references

- https://github.com/dotnet/sdk/pull/20793 (versioned analyzer selection)
- https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/ResolvePackageAssets.cs
- https://github.com/dotnet/roslyn/discussions/81256 (CS9057 and analyzer bands)

## Implemented and verified

- Reproduced the old package failure with actual compiler4.14.0:
  CS9057, analyzer references5.0.0.0 but host is4.14.0.0.
- Added RoslynCompatibility compile-time shim. Legacy CSharp14 branches return
  false/empty/null; no reflection and no string-based syntax recognition. The
  embedded-attribute definition API also has a4.8-compatible implementation.
- Shared source builds against4.8.0 and5.0.0 in separate projects/output/obj paths.
  Both use the same assembly name/version and embedded attribute resource.
- NuGet ZIP verified to contain only the two expected analyzer DLLs. No compiler
  DLLs or unversioned fallback; SDK selects exactly one compatible assembly.
- Package matrix passed for actual csc4.8.0,4.14.0,5.0.0 and SDK10.0.403/csc5.9.
  Toolset processes are explicitly invoked via tiny platform-specific wrappers;
  compiler version and an invocation marker are checked. CompilerApiVersion is
  set to the verified toolset API for SDK asset selection. Simply adding an old
  Toolset PackageReference under SDK10 did not replace its inbox compiler.
- Runtime fixture covers classic projection, helper and classic-extension inlining,
  nullable/type/property/list-slice switch patterns, Update, Adapt, AM0019 retention
  and AM0020 absence.5.0+ cases additionally execute CSharp14 property/method and
  composed factories. All target net8 to separate framework from compiler version.
- Full existing suite:191 passed,0 failed,0 skipped. Release clean build and
  Release pack --no-build --no-restore passed. Independent review caught a missing
  legacy project entry in slnx (it could build the child as Debug); added it to the
  solution and verified after cleaning Release outputs. CI covers no-build packing.
- CI installs net8 runtime/SDK plus the repository SDK10, then runs the package
  compiler matrix after packing. No merge, release, tag or publication performed.

Reproduce from repository root with SDK10 and net8 runtime installed:

```sh
dotnet restore AlephMapper.slnx
dotnet build AlephMapper.slnx -c Release --no-restore
dotnet test AlephMapper.slnx -c Release --no-build --no-restore
dotnet pack source/AlephMapper.csproj -c Release --no-build --no-restore -o artifacts/nuget
python scripts/verify_roslyn_compatibility.py --package-dir artifacts/nuget
```

The script requires exactly one AlephMapper nupkg in that directory and uses fresh
isolated package caches, so previous installs of the same package version cannot
mask a stale analyzer. Dependencies/toolsets are restored from nuget.org; package
and compiler binaries are not committed. Individual IDE versions and hosts below
4.8 were not tested or claimed supported.
