# M0-AC01 schema readiness and fresh migration acceptance

Date: 2026-10-16. Implementation started from local `main` at `039f5a5f5740b6b678015683726b122bc95f6cb4`. Acceptance used a temporary repository copy at `/private/tmp/kohami-schema-readiness-20261016`, an independent SQL Server 2025 container on loopback port 14337, and synthetic `demo_m0` data. The existing Issue06 fixture on 14336 was not used or changed.

## Contract

- `GET /health` remains a database-connectivity probe.
- `GET /ready` returns HTTP 200 with `{ "status": "ready", "mode": "mock", "datasetId": "demo_m0" }` only when `ops.SchemaMigrations` has exactly the supported migration IDs, order and source hashes. Missing ledger, incomplete history, unknown ID or a changed hash returns HTTP 503 with only `{ "status": "not-ready" }`.
- Readiness performs a SELECT only. It does not migrate, seed, adopt a baseline or repair a database.
- Migration `0003_database_grants` remains byte-for-byte unchanged. New migration `0004_schema_readiness_grant` revokes the API role's broad `ops` schema SELECT DENY and grants SELECT only on `ops.SchemaMigrations`; other `ops` objects remain unreadable to API.

## Verification

- `/usr/local/share/dotnet/dotnet build apps/api/Api.csproj -c Release --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false`: PASS, 0 warnings and 0 errors.
- `/usr/local/share/dotnet/dotnet build tests/Native/NativeTests.csproj -c Release --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false`: PASS, 0 warnings and 0 errors.
- `/usr/local/share/dotnet/dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`: PASS, 59 synthetic checks, including supported hash parity with `SqlMigrations.Load`, exact supported ledger, and rejection of missing, incomplete, unknown and changed-hash ledgers.
- `python3 scripts/m0/provision-issue06-fixture.py --port 14337` in the temporary checkout: PASS. New strict-TLS fixture applied migrations 0001–0004 and `CommerceSeed.sql`; the full Commerce SQL harness passed and no Owner or native authority was created.
- Started the API with the new fixture runtime config, confirmed `GET /ready` returned HTTP 200, then ran `M0_SCHEMA_READINESS_URL=http://127.0.0.1:5080/ready /usr/local/share/dotnet/dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json` while the API remained running. PASS: current ledger returned 200; temporarily renamed/missing ledger, wrong source hash and an unknown migration each returned 503; each restored state returned 200. With the API principal, direct SELECT on `ops.DevelopmentFixtureMarker` remained denied (SQL 229) while ledger readiness worked.
- The same SQL acceptance passed successful additive migration replay, injected-failure rollback, synthetic table/history cleanup and a before/after row snapshot of seeded Inventory, Documents, Collections and RefundUnits. Seed snapshot was unchanged.

## Fixture lifecycle and limits

- The temporary fixture used only `kohami-issue06-*` resources owned by this run. Its API process was stopped after HTTP probes; its Compose project, network, SQL container, data/TLS/backup volumes and temporary `.local/m0` private materials were removed after acceptance.
- The existing repository fixture on 14336 remained up and untouched.
- M0-AC01 remains PARTIAL: this run closes the schema-readiness and fresh seeded-upgrade evidence gaps. Existing cross-Dataset read evidence covers selected Commerce reads but not every AC01 foreign-key/read rejection case; no broader PASS is claimed from this run.
