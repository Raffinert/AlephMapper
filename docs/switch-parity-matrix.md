# Switch pattern parity

Baseline: branch `fix/switch-semantics-symbol-inlining-20261101`, HEAD `5c78b723a459f11eeffe1c7642636951f94a0286`, .NET SDK 10.0.403. Restore and Release build succeeded (two existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.6). Full Release suite: 125 passed, 0 failed, 0 skipped.

S01 introduces a structured lowering result carrying symbol-identity bindings for the current matched path. Existing declaration/parenthesized/name-collision runtime regressions in `SemanticRegressionTests` cover preserved behavior. Additional pattern support and its evidence are recorded below as implemented.
