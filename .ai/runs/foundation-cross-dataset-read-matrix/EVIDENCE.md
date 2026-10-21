# Foundation Commerce cross-Dataset read matrix

Date: 2026-10-16. The work was performed in the dedicated temporary clone `/private/tmp/kohami-cross-dataset-20261016`; the main checkout was not modified.

## Findings and changes

- The earlier TLS failure was a target mismatch: the fixture was bound to an alternate loopback port while Native readiness/provisioning and generated test config still targeted `localhost,14336`. Strict TLS correctly rejected the different server certificate at that port. The existing project on 14336 was preserved.
- Dedicated fixture tools now pass one explicitly selected port through readiness, provisioning, config generation and bootstrap guards. Only loopback ports 14336-14399 are accepted; SQL strict TLS, hostname and certificate pin validation remain required.
- The SQL acceptance helper now supplies complete synthetic Checkout/RefundOperation relationships and preserves all protected cookies in the request header.

## Verification

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`: PASS, 0 warnings and 0 errors.
- `dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`: PASS, 0 warnings and 0 errors.
- `dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal` and `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`: PASS, 49 synthetic policy checks; live OS confirmation was not executed.
- `python3 -m unittest tests/foundation/test_fixture_provisioning.py -v`: PASS, 1 port-range regression test.
- `python3 scripts/foundation/provision-issue06-fixture.py --port 14337`: PASS. Strict TLS readiness, dedicated bootstrap guards, and the full Commerce SQL harness passed. All eight new collision assertions passed: dataset-scoped document lookup, CheckoutStatus, catalog SKU lookup, Order/payment summary, RefundOperation summary, receipt summary, resource-scope resolution and reconciliation report stability. The remainder of the existing Commerce SQL harness also passed.

## Fixture lifecycle and cleanup

- The successful run created project `kohami-issue06-e1ceaaee990f` on loopback port 14337. No Owner or native authority was created.
- After acceptance, its containers, network, data/TLS/backup volumes and temporary `.local/foundation` material were removed. Port 14337 is closed.
- Existing project and fixture on 14336 remain present and untouched.
- Verification found no container, volume or network labeled with the successful run's project name.

## Limits

- Foundation-AC01/02 acceptance status was not modified. This run adds read-isolation evidence; it does not establish unrelated release criteria.
- The repository-wide Impact Graph remains partial. The reviewed code/build/fixture evidence is bounded to the paths recorded in `CHANGE_IMPACT.yaml`.
