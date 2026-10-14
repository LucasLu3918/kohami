# M0 Commerce runtime

Approved M0 subset only, fixed `demo_m0` in dedicated `KohamiM0`. Register `AddM0Commerce`, map `/api/v1` with `MapM0Commerce`. `MockWorker` is registered as a singleton; the API host must add it as a hosted service **only in worker-only mode**. Separate processes use distinct worker connection strings and persistent SQL leases. API and worker consume `TimeProvider` from the host.

## Installation

Run `CommerceSchema.sql`, `CommerceSeed.sql`, `CommerceGrants.sql` with the native provisioner/migrator, after the identity schema/dispatch function. The grants file uses `m0_api`/`m0_worker` and grants exact objects, no service receives `db_owner`, schema grants, private-proof reads or login creation. Inventory is synthetic: `sku_demo_01` has a single item; `sku_demo_02` has 3; `sku_demo_03` has 10. Restore targets a new database handled by native tooling; this module never resets existing data.

Every transaction takes `kohami.m0.security` first (Shared API / Exclusive worker), reads `m0.SecurityState` Maintenance=0, then takes `kohami.m0.commerce.demo_m0` Exclusive. M0 uses bounded dataset serialization; this is deliberately conservative, and must be revisited before throughput scaling. `m0.security_dispatch_allowed` runs in the same transaction as NotStarted→Started. Started work follows its original operation after actor revocation; new dispatch remains denied.

## Capabilities and mock trust

Cart/bootstrap cookies and read/pay cookies are Data Protection protected with server-checked expiry. Guest verification matches an exact Order and synthetic contact but never assigns it to a Member by email. Requests return only opaque requestId/requestNonce; proofs are encrypted in `PrivateDeliveries`, which neither API nor worker can SELECT. The native test-driver may decrypt a private delivery with the session provider purpose `kohami.mock.private-delivery`; it must never put proof values in logs or general API responses. Confirm binds browser, intent, order, nonce, proof and expiry, once. Action capability is consumed by the operation key; same-key replay revalidates the exact browser/intent/capability.

Provider results originate from the protected Owner + demo.control + recent purpose operation or `MockWorker.RecordTrustedResult` in a private native/test harness. No extra HTTP injection route exists. New mock attempts default to Unknown; query checks do not resubmit. Source IDs are deduplicated and conflicting hashes fail. Unknown retains source/units/shipping reservations until a terminal result. Late payments become independent Late collection compensation cases; no stock consumption or extra order confirmation.

Synthetic initial refund fixtures: `order_refund_demo`, `refund_case_demo` (merchandise), `shipping_case_demo` (shipping-only), `compensation_case_demo` (extra collection). Every version token binds dataset, resource kind, resource ID and revision through SHA-256; its quoted form is the ETag. Initial case tokens are in the immutable synthetic seed, and must never be reused across cases. Checkout creation matches the current Cart ETag and revalidates the Quote dependency. Primary units are `unit_demo_0_0`=425, `unit_demo_1_0`=552, `unit_demo_1_1`=553; primary shipping 60, total1590; extra source1590 independently. Product refund selects free units by fixed ordinal and asserts client amount. Source, unit and shipping balances are changed in one SQL transaction.

## Evidence

`dotnet run --project tests/Commerce/Commerce.Tests.csproj` covers integer allocation, stable ordering, null Quote identities and header/body version rejection. Set `KOHAMI_M0_TEST_CONFIG` to a private, strict-TLS config for additional real SQL concurrency and rollback checks. Absent config explicitly reports NOT_EXECUTED. Runtime restore, dual-process crash and current real principal permissions belong to root M0 acceptance, not these pure checks. No claim of product acceptance follows from a successful build.
