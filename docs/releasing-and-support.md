# Releasing and support

## Verified scope

The source tree declares version `0.8.3` in [`Analyzer.Shared.props`](../source/Analyzer.Shared.props). This is a source/package-build version claim, not a claim that 0.8.3 has been published to NuGet. The [installation guide](installation-and-compatibility.md) shows a `0.8.3` reference for a build of this source; check the [NuGet package page](https://www.nuget.org/packages/AlephMapper) for published versions before installing from that feed.

The package carries separate Roslyn 4.8 and 5.0 analyzer assets. The repository's compiler verifier exercises Roslyn 4.8.0, 4.14.0, 5.0.0, and the installed SDK compiler against a built package. C# 14 extension members require a C# 14 compiler and the Roslyn 5.0 asset. SQLite behavior has integration tests for selected projections; other EF Core providers and every expression shape are not certified by the suite. See [compatibility](installation-and-compatibility.md) and the [extension matrix](extension-members-compatibility-matrix.md).

## Release checklist

1. Choose the release version and update `source/Analyzer.Shared.props`, current install examples, skill guidance, and generated snapshots as needed. Keep historical release notes versioned to their original releases.
2. Write release notes describing user-facing API and diagnostic changes. Review generated member signatures and diagnostic IDs for compatibility.
3. Run the contributor guide's restore, Release build, full test suite, sample, documentation fixture, and link check.
4. Pack into a fresh isolated directory and run `python scripts/verify_roslyn_compatibility.py --package-dir <directory>`. Verify the package contains both analyzer bands and a README, and inspect its `.nuspec` repository/version metadata.
5. Review symbol/PDB and Source Link evidence for the exact artifact. Do not infer source provenance from a project property alone.
6. Review the CI artifact and package-consumption smoke result. Obtain maintainer approval for publication using the repository's actual release process; publication is a separate action.

The current [Build NuGet package workflow](../.github/workflows/build-nuget.yml) restores, builds, tests, packs, runs compiler compatibility and package-consumption checks, and uploads artifacts. It does **not** publish to NuGet. No separate public release/publishing workflow is recorded here.

### Artifact inspection for this source build

An isolated local `0.8.3` pack produced one `.nupkg` with the README and both `roslyn4.8`/`roslyn5.0` analyzer DLLs. Its `.nuspec` contains the MIT license, improved description/tags, repository URL, and checkout commit. Both Release builds produced portable PDBs containing Source Link data for GitHub source paths, but this pack produced no `.snupkg` and included no PDB in the `.nupkg`. Symbol distribution and exact artifact reproducibility have not been verified. The local package was built with uncommitted documentation/metadata edits, so its repository commit does not identify every byte of this working-tree build; inspect the committed CI artifact before any release claim.

## Support and governance

Report defects and feature requests through [GitHub Issues](https://github.com/Raffinert/AlephMapper/issues); follow the [minimal reproduction guidance](../CONTRIBUTING.md#report-a-bug). This repository has no documented private security-reporting contact or response commitment. Maintainers need to choose and publish a supported confidential route before a `SECURITY.md` can make such a promise. A code of conduct and enforcement contact likewise require a maintainer decision; none is asserted by this page.

The repository has no adopted public deprecation or breaking-change policy. Proposed policy for maintainer review: document generated API and diagnostic-ID changes in release notes, prefer additive changes within a minor version, and give advance notice before removing a supported API. This proposal is not a compatibility guarantee.
