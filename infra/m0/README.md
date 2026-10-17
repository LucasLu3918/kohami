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
   Session independently encrypted DP ring with namespace sentinels; not an OS
   authority permit or evidence of real Owner initialization. No API empty-ring fallback.
5. Secure Enclave broker signing is a local Apple Developer prerequisite. Build the helper
   as the main executable inside an app-like `.app` bundle, with a registered explicit
   bundle identifier, a matching development provisioning profile embedded at
   `Contents/embedded.provisionprofile`, and a matching Apple Development signature. The
   profile and signed entitlements must authorize the app identifier and its Data Protection
   Keychain access group. Launch the executable from `Contents/MacOS` in the logged-in user
   session, then record that executable's SHA-256. A standalone ad-hoc-signed command-line
   binary is insufficient and fails with `errSecMissingEntitlement` (`-34018`). Do not add
   an unverified Secure Enclave entitlement as a workaround. Keep the profile and signing
   material in ignored `.local/`; never commit them. See Apple's [TN3137](https://developer.apple.com/documentation/technotes/tn3137-on-mac-keychains)
   and [app-like bundle signing guidance](https://developer.apple.com/documentation/xcode/signing-a-daemon-with-a-restricted-entitlement).

Browser, SQL and the two wrapping certs use different private keys. The private CA is
project-local; no certificate is installed into system/browser trust. SQL driver validates
custom chain, time, SAN localhost and exact server leaf pin with Encrypt=Strict and
TrustServerCertificate=False. Browser trust remains a native human task.

## Native authority manual gate

Broker `provision` presents exact initialization scope and OS authentication, creates a
nonexportable Secure Enclave signing key with per-use user-presence access control, and
returns only public DER. Secure Enclave key operations use macOS Data Protection Keychain;
the broker explicitly selects it for item lookup, insertion, and retrieval. Provisioning
requires the signed app-like bundle and matching profile described above; an ad-hoc CLI
signature is not sufficient. There is no software-key fallback.
Provisioning failures report only the operation, a whitelisted error-domain category, and
numeric OS/framework code on stderr; the framework error description and key material are
never printed. `ISSUER_ALREADY_INITIALIZED` means stop and inspect existing state; do not
delete the Keychain item to retry.
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

### Issue 02 disposable Development fixture

`scripts/m0/provision-issue02-fixture.py` creates a separate, loopback-only SQL Server 2025 Compose project and private `.local/m0` configuration for Issue 02. It uses a unique Compose project name, strict TLS with a pinned local certificate, a dedicated SQL provisioning credential, and `demo_m0`; it refuses to overwrite existing `.local/m0`. It runs the Commerce SQL harness before leaving the dedicated fixture available for acceptance. It does not modify the preserved `kohami-m0-2025` project or any existing volume.

The storefront login accepts the KOHAMI account email and that account's password; the API validates the stored password hash and creates a session. The SQL Server provisioning credential is service infrastructure and is never an application login identity. The Issue 02 acceptance journey requests the synthetic account password through hidden local terminal input. On successful completion the runner stops only its own local services and removes its disposable Compose project/volumes plus `.local/m0`. If interrupted, inspect the generated Compose project name in the private fixture config and remove only that project explicitly; do not use global Docker prune.

### Issue 06 disposable SQL fixture prerequisite

Run `python3 scripts/m0/provision-issue06-fixture.py` after the Native and Commerce Release binaries exist. It refuses existing `.local/m0` material and an occupied loopback port 14336, creates a unique `kohami-issue06-*` Compose project with its own namespaced SQL/TLS/backup volumes, authenticates with strict TLS, runs the synthetic native-boundary and Commerce SQL harness checks, and initializes synthetic development key rings. It leaves the fixture running for Issue 06 SQL fault/replay work. It creates no Owner or native authority and does not touch the preserved `kohami-m0-2025` project or its volumes. If provisioning fails, it removes only the uniquely named Compose project created during that invocation and the `.local/m0` directory it created. Keep `.local/m0/issue06-compose-project` to identify the project; to remove its containers and volumes later, first verify that file contains a `kohami-issue06-` project name, then use `M0_SQL_PORT=14336 docker compose -p "$(cat .local/m0/issue06-compose-project)" -f infra/m0/compose.yaml down --volumes`. Remove `.local/m0` separately only after confirming it contains the Issue 06 fixture configuration. Issue 02 and Issue 06 fixtures cannot coexist because current Native fixture configuration is rooted at `.local/m0` and port 14336.

`dotnet run --project tests/Native/NativeTests.csproj` tests synthetic signed permit
wrong target/epoch/hash/instance/issuer, expiry equality and restore new-target/namespace/
cutoff/missing-key rejection. Test signing keys exist only in test memory; no fake authority
is compiled into the native executable. OS confirmations/browser trust are
NOT_EXECUTED until independently observed.

`docker compose -f infra/m0/compose.yaml stop` stops only this project. No global prune,
existing volume deletion, SQL password print, log environment dump or `dev-certs --clean`.

## SQL 2022 Strict compatibility finding

The initial SQL2022 Linux CU27 isolated experiment rejected TDS8 with server error17821 despite valid CN/SAN/EKU and container CA trust. No Encrypt/TrustServer fallback was introduced. Root authorized a fresh independent SQL2025 project/volumes; the prior kohami-m0 SQL2022 container is stopped, preserved, and not upgraded in place. The actual SQL2025 Strict result must be recorded by probe-sql, not inferred from image version. [Microsoft repository issue878](https://github.com/microsoft/mssql-docker/issues/878) corroborates similar reports; [Microsoft strict connection documentation](https://learn.microsoft.com/en-us/sql/relational-databases/security/networking/connect-with-strict-encryption) describes TDS8 requirements.
