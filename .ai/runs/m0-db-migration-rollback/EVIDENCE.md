# Versioned SQL migration and rollback acceptance

Date: 2026-10-16
Candidate before this change: `87091e3d6931d825bd319c3a8082cb0b3d01321f`

## Result

- Release Native build: PASS, 0 warnings and 0 errors.
- Native synthetic checks: PASS, 56 checks. Live OS confirmation was not run.
- Release Commerce build: PASS, 0 warnings and 0 errors.
- `python3 scripts/m0/provision-issue06-fixture.py --port 14337`: PASS. Provisioning applied migration IDs `0001_identity_commerce_baseline`, `0002_native_operations`, and `0003_database_grants`; the fixture Commerce SQL harness completed successfully. No Owner was created.
- `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json`: PASS against the dedicated SQL 2025 fixture using strict TLS and the `m0_migrator` principal.

The SQL acceptance checked recorded source hashes, applied a synthetic successor migration, replayed it without duplicating its row, then injected a SQL failure after synthetic DDL and DML. The failed migration's table, row, and history entry rolled back; the preceding successful migration remained applied. Test-only objects and migration history rows were removed, and the fixture marker remained valid.

## Scope and limits

This proves per-migration SQL transaction rollback and forward-only version tracking on a new dedicated fixture. It does not prove rollback of a previously committed migration, backup restore, RPO/RTO, or adoption of an existing schema without a migration ledger. Such legacy databases fail closed with `SQL_MIGRATION_BASELINE_REQUIRED` and require a separately reviewed baseline-adoption procedure. No reverse migrations are provided.

## Cleanup

The task-owned Compose project `kohami-issue06-85e72e3b1876`, its data/TLS/backup volumes and network, and the task-created `.local/m0` private material were removed after acceptance. Loopback ports 14336 and 14337 were closed before and after cleanup; the existing checkout was not used for SQL acceptance.
