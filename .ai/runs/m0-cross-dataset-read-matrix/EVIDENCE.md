# M0 Commerce cross-Dataset read matrix

Date: 2026-10-16. Scope is test-only; no Commerce runtime or schema change was made.

## Results

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`: PASS, 0 warnings and 0 errors.
- `env -u KOHAMI_M0_TEST_CONFIG dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`: 16 pure-rule assertions PASS. The harness explicitly reported `SQL product checks NOT_EXECUTED` because no dedicated fixture was available to the process.
- Dedicated SQL acceptance: BLOCKED before the matrix ran. Two fresh disposable fixture attempts were automatically torn down when SQL readiness failed. The second server logged that it was ready inside its container, but Native strict-TLS authentication returned SQL diagnostic `-2146893019`; the host loopback listener also did not become available before the provisioning timeout.

The added matrix covers a duplicate Checkout document and `CheckoutStatus`, a duplicate SKU and product catalog read, duplicate Order/RefundOperation/Receipt documents and Order summary, a duplicate Collection and reconciliation report, plus `CommerceResourceResolver`. Each pair uses the same IDs under `demo_m0` and `other_dataset`. SQL checks temporarily disable constraints only in the new disposable fixture and revalidate them after cleaning the synthetic rows in `finally`.

## Fixture lifecycle

- `14336` was already associated with a different Docker Compose project and was left untouched.
- This run's `14337` project `kohami-issue06-ac8aa888104d` and `14338` project `kohami-issue06-a1590d73fb16` were each created by the fixture provisioner and removed by its failure cleanup, including their own containers, networks, and data/TLS/backup volumes.
- Verification after teardown found no `.local/m0` under this temporary clone and no resources labeled with either project name. Ports `14337` and `14338` were closed. The existing project on `14336` remains untouched.

## Limits and next step

Do not update M0-AC01/02 acceptance status from this run. Re-run `DatasetReadIsolationAcceptance` only after a new dedicated fixture passes the project's strict-TLS readiness check; investigate why Native reports `-2146893019` for the newly generated local SQL certificate without weakening TLS.
