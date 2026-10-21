# Foundation identity implementation

The root host calls `AddSecurity` and `MapSecurity` under `/api/v1`. This module maps 16 approved identity/grant endpoints; the root host owns CSRF, exact Origin/Host checks, no-store middleware, safe Problem rendering and `/auth/csrf`.

`Principal.Stage` uses `MemberAuthenticated` and `AdminAuthenticated`. SQL SecurityVersion, hold/status, session expiry, Dataset maintenance and effective role/scope grants remain authoritative.

SQL migrations: migrator applies `SQLSchema.sql`, then named EXECUTE allowlists in `SQLGrants.sql`. Native procedures receive no runtime EXECUTE grant; `ops.Apply` dispatches through a fixed ownership chain in the native module. All mutations require a transaction holding `sp_getapplock` on `kohami.identity_access.security`, including native and worker Started decisions. `security_dispatch_allowed` returns one bit and must run in the same transaction as Started. Calling `ISecurity.CanDispatchAsync` separately is an inspection, not an atomic dispatch authorization.

The host registers `IResourceScopeResolver` for typed commerce resource existence and parent scope aliases. The default rejects business resource kinds as SOURCE_UNBOUND. A foreign Dataset never matches. `IEmailEvidenceSink` defaults to blocked; production local host registers `PrivateEmailEvidenceSink` against an existing private non-symlink directory (0700), outside SSR/static roots. It emits CreateNew 0600 out-of-band evidence, never an HTTP token, and no secret replay endpoint exists.

Session cookies use a dedicated ASP.NET Data Protection provider. Account passwords use ASP.NET Identity hashing and server-side failure throttling; password changes, account suspension, grant changes and logout revoke affected sessions through the security version.

No native permit, default password, certificate private key, usable code or owner account is committed. Public RFC vectors in tests are not operational credentials.

## Verification

`dotnet test tests/Identity/Identity.Tests.csproj` runs unit and opt-in SQL integration tests. SQL tests explicitly report SKIP/NOT_EXECUTED unless `Foundation_IDENTITY_SQL_TEST_CONFIG` points to a private config for a **dedicated disposable SQL instance**, containing the ordinary RuntimeSettings fields plus `IdentitySqlTestDedicated: true` and `MigratorConnectionString`. The marker is test-fixture scope and never a native operations permit. Config values must not be printed/committed. The test instance must use DB Kohami and real validated TLS; the fixture may modify only its generated `itest_*` users and transient maintenance bit. Apply the module schema and API roles before running; migrations are not silently installed by API startup. SQL evidence covers session stage, challenge consumption, exact runtime SQL deny probes and server expiry. Absent configuration is never reported as product PASS.
