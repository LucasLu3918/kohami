# M0 schema baseline adoption evidence

Task scope: exact catalog fingerprint and current migration source hashes must match before an existing schema can receive migration ledger rows. All changes and SQL checks ran in `/private/tmp/kohami-schema-baseline-20261016` on `codex/m0-cross-dataset-read-matrix`.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| TDD red | PASS | After adding the Native acceptance entry, Release build failed at the expected missing `SqlBaselineAdoption` type. First `--no-restore` attempt was blocked earlier by absent fresh-clone assets; restore succeeded with `-p:NuGetAudit=false` because nuget.org vulnerability metadata was unreachable. |
| Native Release build | PASS | `dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`; 0 warnings, 0 errors. |
| Native non-SQL checks | PASS | `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`; 56 checks. Live OS confirmation remains `NOT_EXECUTED`. |
| Commerce Release build | PASS | `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -nr:false -v:minimal`; 0 warnings, 0 errors. |
| Dedicated fixture provisioning | PASS | `python3 scripts/m0/provision-issue06-fixture.py --port 14337`; Compose project `kohami-issue06-5f229c301479`, SQL2025, strict TLS, Commerce SQL harness passed, no Owner created. |
| Baseline adoption SQL acceptance | PASS | `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-baseline-adoption .local/m0/fixture-private.json`; exact baseline adopted, then pre-existing ledger, altered source hash, extra schema object, and revoked grant each failed closed. Original ledger, grant, and fixture marker were verified restored. |
| Existing migration SQL acceptance | PASS | `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json`; replay, synthetic upgrade, rollback of DDL/data/history, and probe cleanup passed. |
| Whitespace check | PASS | `git diff --check`. |

Pinned SQL catalog fingerprint: `0495b8123f131fff266dc8af79d12034e5d99f9f6b19f0081eb39597c4b0b76a`. It covers the SQL2025 dedicated fixture's schema and object metadata, principals and role memberships, module definitions, constraints/indexes/columns, database permissions, and the exact current migration IDs/source hashes. Only the exact four development-fixture metadata objects plus the migration ledger are excluded.

## Limits

- `CommerceSeed.sql` is part of migration `0001`. Adoption fingerprints the migration source but does not validate or replay existing business/seed rows. Operators must independently check those data invariants and complete a restorable backup before adoption.
- The catalog fingerprint was generated and accepted on SQL Server 2025 only. Other SQL Server versions are expected to fail closed until their metadata shape is reviewed and separately accepted.
- The command path `adopt-native-local-baseline` is compile-verified; it was not run against `localhost,14334`. SQL acceptance invoked the shared adoption operation on the dedicated `localhost,14337` fixture to avoid touching any normal local database.
- No reverse migration or database restore was performed.
- Impact Graph mapping for these C# SQL nodes remains unavailable; exact native command/acceptance callers were manually reviewed and global graph coverage remains partial/unknown.
