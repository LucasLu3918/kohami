# M0-AC01 source-bound SQL/API acceptance follow-up

Date: 2026-10-16. Identity regression test candidate: `fd00076ba858965df813cbd47dd2fa6e8a5eb7ed`. The API/Nuxt acceptance ran on its parent runtime candidate `582423288df41e5d1eaf86305d8ff3c46e60b28c`; the intervening commit only added Identity test coverage and did not alter runtime code.

## Results

- Dedicated target preflight: `dotnet tools/native-operations/bin/Release/net10.0/Native.dll fixture-status "$PWD"` passed. It reported strict TLS and a present database for the repository-marked loopback Issue06 fixture on port 14336. No private config or credential values were printed.
- Native migration acceptance: `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json` stopped with `SQL_MIGRATION_BASELINE_REQUIRED` because `ops.SchemaMigrations` is absent. The guard failed before migration/test DDL or DML. No schema baseline was adopted.
- Native baseline-adoption acceptance: NOT EXECUTED. Its fixture test requires original migration history to restore; the current fixture has no ledger. The separate baseline-adoption command requires a verified restorable backup and review of existing business data, neither of which is established by this run.
- Identity SQL suite: the final command was `M0_IDENTITY_SQL_TEST_CONFIG=/Users/lucas/Coding/main/kohami/.local/m0/fixture-private.json /usr/local/share/dotnet/dotnet test tests/Identity/Identity.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false`; PASS on candidate `fd00076ba858965df813cbd47dd2fa6e8a5eb7ed`, 36/36, 0 skipped. This includes `GetSessionRejectsCookieBoundToAnotherDataset`: the stored session ID with an `other_dataset` cookie label returns `Anonymous`, null `userId`, and no permissions. Only generated `itest_*` rows and transient maintenance state are used and cleaned/restored by the suite.
- API/Nuxt Identity acceptance: `python3 scripts/m0/run-issue06-aftercare-acceptance.py --identity-only`; PASS, Playwright 1/1. The real local API returned the authenticated Owner projection from `GET /api/v1/auth/session`; after logout and replay of the prior cookie it returned `Anonymous`. Origin/antiforgery guards passed. The runner removed its synthetic SQL rows and session artifact, stopped only its own processes, and preserved the fixture.
- An earlier relative-config attempt of the Identity suite failed 11 cases before fixture access because `dotnet test` resolves the relative path from `tests/Identity/bin/Release/net10.0`; 24 pure cases passed. The absolute-path rerun passed and then passed again with the new dataset case (36/36).
- The new SQL regression initially failed at its assertion because a JSON null node was dereferenced in the test itself. The assertion was corrected to check the node directly; the complete suite then passed. No runtime code change was needed.

## Remaining acceptance limits

- `apps/api/Program.cs` exposes `/health` with status `database-connected`; there is no schema readiness endpoint or readiness contract. A HTTP 200/DB connection therefore is not evidence that an unknown schema is rejected from readiness.
- The migration/baseline checks that would establish existing-seed upgrade and unknown-schema rejection could not be run on this fixture without adopting a missing schema ledger. This run did not verify a fresh database setup, seed integrity, or non-destructive seeded upgrade.
- Existing `.ai/runs/m0-cross-dataset-read-matrix/EVIDENCE.md` records Commerce dataset-isolation checks on a separate disposable SQL fixture. The new Identity test specifically adds session-cookie dataset isolation; neither evidence alone establishes all AC01 foreign-key/read cases.
- M0-AC01 remains `PARTIAL`. No production API, SQL schema, fixture business seed, or user data was changed.
