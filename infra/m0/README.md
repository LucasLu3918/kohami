# KOHAMI M0 本機隔離環境

Scope: new Compose project `kohami-m0-2025`, loopback `14334`, database `KohamiM0`,
independent named data/TLS/backup volumes. `KohamiTest` and existing volumes are untouched.
SQL Server 2025 image is digest-pinned and runs through amd64 emulation on this ARM64 host;
this is development evidence, not Microsoft's native-platform support claim.

1. `python3 scripts/m0/provision-local.py` creates synthetic local TLS/DP material and
   per-service credentials in ignored `.local/m0`, modes 0700/0600. It refuses overwrite.
2. `docker compose -f infra/m0/compose.yaml up -d` starts the isolated SQL. Its root init
   container copies only SQL TLS files to the dedicated TLS volume, sets key 0600 and
   owner 10001; SQL runs as UID10001, HOME=/var/opt/mssql. TLS is mandatory.
3. Build `tools/native-operations/Native.csproj`, then `provision-sql <absolute-repository-root>`
   applies exact locked schema/grant sources once. The private provisioner config is
   never consumed by API/worker. No runtime receives sysadmin/db_owner.
4. `initialize-rings .local/m0/runtime-private.json` initializes **synthetic development**
   Session/TOTP independently encrypted DP rings with namespace sentinels; not an OS
   authority permit or evidence of real Owner initialization. No API empty-ring fallback.
5. Build broker with installed matching SDK and a dedicated module cache:
   `swiftc -sdk /Library/Developer/CommandLineTools/SDKs/MacOSX26.5.sdk -module-cache-path /private/tmp/kohami-swift-modulecache tools/native-operations/Broker.swift -o .local/m0/native/kohami-local-broker -framework Security -framework LocalAuthentication -framework AppKit`.
   Ad-hoc codesign ID `com.kohami.m0.native.broker`; record executable hash. This local
   signature is source identification, not enterprise distribution/trusted signer evidence.

Browser, SQL and the two wrapping certs use different private keys. The private CA is
project-local; no certificate is installed into system/browser trust. SQL driver validates
custom chain, time, SAN localhost and exact server leaf pin with Encrypt=Strict and
TrustServerCertificate=False. Browser trust remains a native human task.

## Native authority manual gate

Broker `provision` presents exact initialization scope and OS authentication, creates a
nonexportable Secure Enclave signing key with per-use user-presence access control, and
returns only public DER. It refuses unavailable Secure Enclave; no software-key fallback.
Broker `store-credential` accepts the independent native SQL secret by stdin after native
confirmation. Delete that transient native value from provisioner input after successful
import. Native/app/worker credential values must not be pasted into chat or argv.

`Operations` verifies the broker file hash, issuer public key, action/target/dataset/DB
fingerprint/epoch/preview hash, ten-minute expiry and signature. SQL consumes PermitId once,
locks security state, fences new business dispatch, and appends audit in the same transaction.
Host Intent precedes commit. Reconcile only records an existing operation; it never replays
prepare/recovery/financial work. Host failure leaves CommittedAuditPending/maintenance.

**Live Owner/native apply is BLOCKED until human OS confirmation and issuer/consumer
isolation evidence are bound.** A config evidence flag grants no authority: per-use signature
and Keychain confirmation remain mandatory. This source has no HTTP native endpoint, --yes,
web-cookie authorization, exportable signing private key, or unattended SQL credential mode.

SQL 2022 does not independently verify Secure Enclave ECDSA signatures. The trust boundary
is the source-bound verified native process plus OS-protected native credential; SQL denies
runtime ops execution and provides atomic single-use/epoch enforcement. Same-UID filesystem
ACL alone is not process isolation; pending manual broker/API-worker denial evidence must
not be reported PASS. A compromised OS operator/source-bound helper is outside this local
model and requires stronger OS service isolation before broader deployment.

RotateKeyMaterial and RestoreSecurityCheckpoint have signed scope/checkpoint guards; actual
host rotation/new-target RESTORE remains **BLOCKED** until old-private-key dependency,
encrypted bundle unlock and complete post-cutoff revocation ledger evidence exists.
Neither command overwrites source DB, resets UsedAt proofs, restores expired grants or
re-dispatches old payment/refund jobs. A backup_operator is not a restore principal.

## Verification and cleanup

`dotnet run --project tests/Native/NativeTests.csproj` tests synthetic signed permit
wrong target/epoch/hash/instance/issuer, expiry equality and restore new-target/namespace/
cutoff/missing-key rejection. Test signing keys exist only in test memory; no fake authority
is compiled into the native executable. OS confirmations/manual TOTP/browser trust are
NOT_EXECUTED until independently observed.

`docker compose -f infra/m0/compose.yaml stop` stops only this project. No global prune,
existing volume deletion, SQL password print, log environment dump or `dev-certs --clean`.

## SQL 2022 Strict compatibility finding

The initial SQL2022 Linux CU27 isolated experiment rejected TDS8 with server error17821 despite valid CN/SAN/EKU and container CA trust. No Encrypt/TrustServer fallback was introduced. Root authorized a fresh independent SQL2025 project/volumes; the prior kohami-m0 SQL2022 container is stopped, preserved, and not upgraded in place. The actual SQL2025 Strict result must be recorded by probe-sql, not inferred from image version. [Microsoft repository issue878](https://github.com/microsoft/mssql-docker/issues/878) corroborates similar reports; [Microsoft strict connection documentation](https://learn.microsoft.com/en-us/sql/relational-databases/security/networking/connect-with-strict-encryption) describes TDS8 requirements.
