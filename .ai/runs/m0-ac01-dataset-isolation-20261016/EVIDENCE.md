# M0-AC01 cross-Dataset FK and read isolation acceptance

Date: 2026-10-16  
Candidate base: `39dac4d22e4f7dc0f069edeb4606889a8a0a4d73`  
Fixture: fresh dedicated SQL Server 2025, strict TLS, loopback port 14337, temporary detached checkout. The existing 14336 Issue06 fixture was not accessed.

## Scope and results

- Commerce schema inspection found 11 tables with the fixed `Dataset='demo_m0'` check and exactly three composite foreign keys. Runtime Identity tables have no Dataset column; their existing SQL regression covers a foreign-Dataset session cookie projecting `Anonymous`, null user ID, and no permissions (36/36, candidate `fd00076ba858965df813cbd47dd2fa6e8a5eb7ed`). The Identity schema and test sources have not changed between that candidate and this run's base.
- All 11 Commerce Dataset tables rejected a synthetic `other_dataset` value with SQL Server constraint error 547.
- Each composite FK was confirmed from SQL Server metadata to include Dataset in both child and parent keys, then exercised against a foreign-Dataset-only parent. Reservations→Inventory, RefundUnits→Collections, and PrivateDeliveries→GuestRequests all rejected a `demo_m0` child reference with SQL error 547.
- Existing collision probes passed for eight Commerce read projections: checkout document lookup, CheckoutStatus, product catalog, Order/payment summary, refund summary, receipt state, resource scope resolution, and reconciliation report. Configured-dataset values were returned; foreign refund/receipt effects did not appear.
- Full Commerce SQL harness completed, including its payment/refund, worker recovery, guest aftercare, and fixture-cleanup assertions. New matrix added 17 positive rejection/metadata assertions and reran the eight read-isolation assertions.

## Commands and evidence

- `dotnet build tools/native-operations/Native.csproj -c Release --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` — PASS, 0 warnings/errors.
- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` — PASS, 0 warnings/errors.
- `python3 scripts/m0/provision-issue06-fixture.py --port 14337` — PASS; fresh fixture migrations 0001–0004 and Commerce seed applied, strict TLS authenticated, full Commerce SQL harness passed; no Owner created.
- After acceptance, the run-owned `kohami-issue06-*` containers, network, volumes, and temporary checkout `.local/m0` private materials were removed. The pre-existing 14336 fixture and volumes were preserved.
- `git diff --check` — PASS.

## M0-AC01 disposition

Together with the previously recorded fresh-schema / seeded-upgrade preservation and `/ready` unknown-schema rejection evidence, plus the existing Identity foreign-Dataset session rejection evidence, this closes the remaining fixed-Dataset, foreign-key, and read-isolation cases for M0-AC01. Set M0-AC01 to PASS. This does not imply the remaining M0 acceptance cases or overall M0 are complete.
