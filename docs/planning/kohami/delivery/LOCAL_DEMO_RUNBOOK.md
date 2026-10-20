# 本機操作與驗收命令

更新：2026-10-21。這是操作與命令的唯一文件；執行順序見 [主計畫](IMPLEMENTATION_PLAN.md)，必要驗收見 [VALIDATION_PLAN](VALIDATION_PLAN.md)。唯一長期本機 SQL fixture 為 OrbStack `kohami-sql`，私有設定位於忽略的 `.local/kohami-sql/m0/`；舊 `.local/m0`／Issue fixture 不作為新操作目標。Issue 11 歷史 cart／quote 驗收命令與證據保留供追溯，不代表目前 fixture 或 maintenance 狀態。

## 1. 先決條件與作用範圍

位於專案根目錄；固定 candidate、工具／鎖檔及 dedicated 合成資料集。確認 .NET SDK 與 Node 符合 global.json／package.json，SQL 版本、TLS、角色及私有材料與目標相符。Node 範圍以 apps/storefront/package.json 為準，不另外建立版本權威。

私有設定與 key 只存被忽略的 .local；本機 fixture 可能已清除，不從歷史 localhost 證據推定存在。普通 start 不偷偷 migration／seed；本機 synthetic member／wallet 的建立只可透過下列明確 bootstrap 命令，禁止直接改 SQL 解 gate。任一步無法辨認目標即保持 BLOCKED。

SQL schema 僅由明確 provisioning／`migrate-native-local` 命令套用版本化向前遷移；每個版本以來源 SHA-256 固定，單一遷移與台帳紀錄在同一 SQL transaction。專用 `m0_migrator` 僅供該命令使用，具 `m0`／`m0commerce`／`ops` schema 的 DDL／DML 權限，不供 API／worker 使用，也不屬於 `db_owner`。既有 schema 若無遷移台帳會 fail closed，須先做明確基線採用；已套用來源不可編輯，變更新增版本。沒有反向遷移；故障回復要靠資料庫備份／還原流程，此流程不由本機 migration acceptance 代替。

`adopt-native-local-baseline` 是明確 opt-in 的本機舊 schema 採用命令：只接受無既有 `ops.SchemaMigrations` 且 schema、owner、database principals／role membership、物件、欄位、constraints、indexes、modules 與 database permissions 完全符合已釘選 SQL catalog fingerprint、目前 migration source hashes 的資料庫。Provisioner credential 只讀 catalog；台帳由 `m0_migrator` 在單一交易寫入，兩連線必須同指 `localhost,14334/KohamiM0`。不符或已有 ledger 即拒絕、不建 ledger。指紋只驗結構／權限及 migration source identity，不驗或重播 `CommerceSeed.sql` 的既有業務資料；採用前須另行確認 seed／業務資料完整性與一致性，並先完成可還原備份。指紋目前以 SQL Server 2025 dedicated fixture 產生；其他版本採 fail closed，須另行審查及驗收後才能更新常數。

## 1.1 本機服務基本啟動與必要檢查

```sh
python3 scripts/m0/run-local.py
```

啟動器預設讀 `.local/kohami-sql/m0/runtime-private.json` 及該目錄的 browser certificate，API 綁 `127.0.0.1:5083`，HTTPS Storefront 使用 `localhost:3003`，保留既有 5080–5082 與 3000–3002 程序。啟動前檢查私有檔、API Release DLL 與 loopback port；為 3003 產生只供本次程序使用的 0600 暫存 Origin 設定。Ctrl+C 只停止本次 API／Nuxt 子程序並移除暫存設定；不啟動 SQL、不做 migration／seed／reset，也不更動 maintenance／Owner。

本機 API／會員結帳需要 synthetic member 時，在 migration 完成且確認目前資料庫符合 marker／空白身份前置條件後，明確執行：

```sh
dotnet tools/native-operations/bin/Release/net10.0/Native.dll local-development-bootstrap "$PWD"
```

命令固定連線 `localhost,14349/KohamiM0/demo_m0`，以 dedicated fixture marker／instance fingerprint、schema 0013、空白 `m0.Users`／`m0.Grants` 與仍開啟的 maintenance 狀態為前置條件；任何不符即停止。它不 reset、不覆寫既有身份或業務資料、不建立 Owner／native permit，僅建立 `kohami-local-member@example.test` 合成會員、NT$50,000 測試入帳與稽核記錄，並於同一 SQL 交易解除本機 gate。密碼隨機產生，只存 `.local/kohami-sql/m0/local-development-credentials.json`（Unix mode 0600）；不要複製到聊天、shell history 或版本控制。此初始化不可用於一般資料庫或正式環境。

必要 smoke 限於：

1. `GET http://127.0.0.1:5083/health` 回 200，確認 API 可連資料庫。
2. `GET http://127.0.0.1:5083/ready` 回 200，確認 migration ledger 與目前支援 schema 相符；若回 503，停止驗收並依原生遷移／基線流程處理，不把 health 200 當 ready。
3. `https://localhost:3003/zh-TW`、`/ja`、`/en` 均回 200，且頁面商品資料確由目前 API／SQL 載入。SSR 200 單獨不代表瀏覽器購物或結帳通過。
4. 初始化後以私有憑證登入合成會員，驗證購物車→報價→結帳→內部錢包扣款→訂單狀態及帳本；必要旅程需由 dedicated SQL 驗收器確認餘額不足、重送及交易回滾。付款服務不連第三方 API；結果標記為 mock wallet payment。

可用 `KOHAMI_API_PORT`（5080–5099）及 `KOHAMI_SITE_PORT`（3000–3999，排除 3000／3001）覆寫埠號，Origin 會隨預覽埠暫時調整；`KOHAMI_M0_LOCAL_DIR` 可明確指定私有 profile 目錄。未 bootstrap 前，API health/readiness 只證明連線與 schema，不宣稱寫入已啟用。初始化成功後，API 仍沿用會員登入、scope／權限、Origin／CSRF、冪等、版本及 SQL 原子交易檢查；只有 marker-verified 本機 demo dataset 由 bootstrap 開啟 maintenance gate。正式環境仍須獨立 Owner authority 與安全審查。

## 2. 已存在的檢查與命令（根目錄執行）

以下表格包含先前 Issue 專用 fixture 的歷史命令，相關 fixture 已退役；不得在目前 `kohami-sql` 上照搬它們執行 migration／seed／reset 或 acceptance runner。它們只用於解讀過往證據。當前支援的服務啟動命令只有上節 `python3 scripts/m0/run-local.py`；本機資料庫變更操作若尚無明確指向 `kohami-sql`、preview／授權與回復方法的專用命令，保持未執行，不以舊 port 或舊 `.local/m0` 設定代用。

| 用途 | 實際命令／檔案 | 前置與效果 |
| --- | --- | --- |
| 工具確認 | `dotnet --version`、`node --version`、`npm --version` | 唯讀；核對專案宣告版本 |
| 架構檢查 | `python3 -m unittest discover -s tests/architecture -v` | 不讀 SQL 私有設定，驗邊界及檔案參考 |
| NuGet 還原 | `dotnet restore Kohami.slnx --locked-mode` | 依現有 lockfiles 還原，會寫 obj／cache／可能連網；缺 assets 不能跳過後假稱測試 PASS |
| 後端建置 | `dotnet build Kohami.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false` | 已還原套件；寫 bin／obj，不改 DB |
| Core 契約測試 | `dotnet test tests/Core/Core.Tests.csproj -c Release --no-restore` | 必須看到實際測試數與結果；只回建置成功不算測試通過 |
| 商務 harness | `dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-restore` | 純規則與 SQL 能力依專用設定區分；要 SQL checks 時先驗 KOHAMI_M0_TEST_CONFIG 指向 dedicated 合成 fixture，不對展示資料重設 |
| Issue 06 worker crash/restart／failure replay matrix | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | dedicated fixture 驗證 NotStarted 已提交工作續跑、未到期 Started lease 不被搶占、兩程序競爭 expired Started payment／receipt job、付款與 receipt job 原子提交、receipt-only replay、Unknown 不作新操作、完成工作重播不 dispatch、舊 fence 拒絕及 synthetic rows 清理。只證明 mock adapter 的 SQL／lease 邊界，不模擬外部 provider 呼叫中 kill process；不執行 reset，保留 fixture |
| Issue 23 報表、安全日誌、售後資料與訪客 proof 保存 | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | 僅在 dedicated loopback 合成 fixture 執行；需包含 migration 0001–0012。完整 Commerce harness 驗證報表授權稽核、90 日登入失敗／1 年安全與報表稽核清理、Shipment／Return 去識別，以及 GuestRequests、PrivateDeliveries、GuestCapabilities 到期即清除（`ExpiresAt <= UTC now`），並保留未到期資料。測試建立的 synthetic rows 由 acceptance 清理；不得在展示資料庫重設。 |
| Issue 06 M0-AC06 付款截止／取消競態 | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | 在同一 dedicated Commerce harness 驗到期前一秒、15 分鐘等號、取消後晚到成功與取消／成功並行；固定時鐘、SQL 終態及 consume／release／補償唯一性；確認每組 synthetic rows 清理及 SKU 庫存還原。mock adapter 範圍，不代表真實 provider 呼叫中斷；不執行 reset，保留 fixture |
| Issue 06 M0-AC07 退款額度／併發／撤權追蹤 | `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll` | 同一 dedicated Commerce harness 啟動同 unit、不同 line、原 shipping 的同步競爭；驗證 1,590 原 collection 上限、Unknown 保留及查核原 operation、撤權在 Started 前釋放與 Started 後續追。只使用合成資料與 mock worker trusted-result seam；不代表完整 HTTP/browser 退款或真實 provider 結算；不執行 reset，保留 fixture |
| Native 合成檢查 | `dotnet run --project tests/Native/NativeTests.csproj -c Release --no-restore` | 合成測試驗證目前 fail-closed 行為；Identity 自動化檢查依其適用案例執行 |
| Native SQL migration/readiness acceptance | `M0_SCHEMA_READINESS_URL=http://127.0.0.1:5080/ready dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-migrations .local/m0/fixture-private.json` | 僅接受當前 repo 自有 `.local/m0/fixture-private.json`、strict TLS、loopback 14336–14399 及有效 dedicated fixture marker；在**新建 disposable fixture**驗 ledger source hash、API readiness 對 missing/hash-mismatch/unknown 的 503 與還原後 200、seeded synthetic upgrade 保持資料、失敗 rollback 與 replay。會暫時改名／修改 ledger 並在 `finally` 還原；成功／失敗 probe tables 與 ledger rows 清除。不要對一般或展示資料庫執行；既有 native fixture若缺 ledger，migration runner會在任何測試 DDL/DML前以 `SQL_MIGRATION_BASELINE_REQUIRED` 停止。 |
| Native SQL baseline adoption acceptance | `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll --sql-baseline-adoption .local/m0/fixture-private.json` | 僅接受當前 repo 自有 fixture config、strict TLS、loopback 14336–14399 及有效 dedicated marker；驗 exact schema／principal／role／module／grant fingerprint 與當前 migration hashes、已有 ledger／source/object/grant drift 拒絕；清除本案例 drift table 並還原原 ledger／grant，保留 fixture。這是結構驗收，不驗 seed/business data |
| 本機舊 schema 基線採用 | `dotnet tools/native-operations/bin/Release/net10.0/Native.dll adopt-native-local-baseline "$PWD"` | 僅連 repo `.local/m0` 的 strict TLS `localhost,14334/KohamiM0`；provisioner 只讀 catalog、`m0_migrator` 同交易寫目前版本台帳。要求 `ops.SchemaMigrations` 尚不存在、catalog 指紋與 migration source hashes 完全相同。執行前先做且驗證可還原備份，人工核對 seed／既有業務資料；本命令不搬移、補種或對帳資料。失配保持原狀並拒絕 |
| 前端還原 | `npm --prefix apps/storefront ci` | 依現有 package-lock；可能連網及寫 node_modules，不更新產品依賴版本 |
| 前端型別與建置 | `npm --prefix apps/storefront run typecheck`、`npm --prefix apps/storefront run build` | 寫產物；不等於 browser／SQL 旅程 |
| M0-AC11 Storefront SSR HTML | `node apps/storefront/tests/m0-ac11-ssr.mjs` | 需先 build Storefront；啟動本測試自有的 mock HTTP API stub（127.0.0.1:5080）及 Nuxt production server（localhost:3000），驗證三語首屏摘要／失敗狀態、mock/noindex/no-store、locale 不改 transaction ID 與 cookie forwarding；不連 SQL、不代表真實 Commerce API／actor scope，結束會停止自有程序。固定 ports 已被使用時不要執行。 |
| M0-AC11 CheckoutStatus API／SQL＋Nuxt SSR actor scope | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；在同一 API／worker／Nuxt SSR 售後旅程驗證 owner browser 可讀真實 CheckoutStatus 並看到摘要、另一個 API 簽發的 browser 得到 API 404 與 SSR generic unavailable；另驗 SQL／Playwright 售後狀態。Runner 清理本輪 synthetic rows/session artifact，保留 fixture；不驗證外部 provider 或資金結算。 |
| 文件候選 | `python3 scripts/build_kohami_canonical.py --check`、`python3 scripts/check_kohami_canonical.py` | Python 須可用 PyYAML／jsonschema／openapi-spec-validator；只驗文件，不生成 client 或改 DB |
| runner 回歸 | `python3 -m unittest discover -s tests/m0 -v` | Python runner 設定／scope 回歸；不等於真 API 產品旅程 |
| 既有 SQL 狀態 | `./infra/sql-server/manage.sh status`、`./infra/sql-server/manage.sh check` | 僅驗該 README 所述環境，不取代 dedicated 產品 fixture、TLS 或最小權限證據 |
| 本機 API＋Nuxt 啟動 | `python3 scripts/m0/run-local.py` | 需 API Release DLL、.local/m0/runtime-private.json 及 browser key/cert；啟動自己建立的程序，不 provisioning 或繞原生授權。Ctrl+C 停自己的子程序並保留 DB |
| 獨立 worker | `dotnet run --project apps/worker/Worker.csproj -c Release --no-restore` | 執行前核對實際 csproj 路徑與 M0_CONFIG 為 worker 最小權限設定；不可盲目用 API／migrator credential；runner 可能已啟 worker，避免重複；此 worker 也執行帳號生命週期排程 |
| Issue 01 真旅程 | `python3 scripts/m0/run-issue01-acceptance.py` | 只允許 pinned localhost:14336/KohamiM0 dedicated fixture 與合成設定、預建 DLL、前端依賴；核對腳本前置，會寫測試 rows／啟動本次服務，不能對一般 DB 執行 |
| Issue 02 真旅程 | `python3 scripts/m0/run-issue02-acceptance.py` | 需 scripts/m0/provision-issue02-fixture.py 定義的專用 disposable fixture、原生授權／確認與既定來源；runner 會啟停自身服務並依流程移除自有 fixture／私有材料。執行前核對目標、效果與人工前置 |
| Issue 06 專用 SQL fixture 前置 | `python3 scripts/m0/provision-issue06-fixture.py` | 需 Native／Commerce Release DLL、Docker Compose、`.local/m0` 不存在且 loopback 14336 空閒；新建唯一 `kohami-issue06-*` 專案／volumes，strict TLS 與 Commerce SQL harness 通過後保留 fixture，供 Issue 06 故障／重播證據使用；不建立 Owner／原生權限，不碰既有專案 |
| wallet03-http 專用 SQL fixture 前置 | `python3 scripts/m0/provision-wallet03-http-fixture.py --port 14350` | 建立全新 `.local/kohami-demo/wallet03-http` profile 與唯一 `kohami-wallet03-http-*` Compose project；loopback、strict TLS、migration 與 Commerce SQL harness 通過後保留。`fixture-private.json` 另標記 `Wallet03HttpDedicated=true` 與實際 SQL `port`，供專用 acceptance runner 驗證。既有 `.local/m0` 與 `.local/kohami-sql/m0` 不會被讀寫或清理；不建立 Owner／原生權限。可加 `--local-dir <path>` 指定另一個不存在且位於本 repo `.local/` 內的 profile。 |
| wallet03-http API／SQL acceptance | `python3 scripts/m0/run-wallet03-http-acceptance.py --local-dir "$PWD/.local/kohami-demo/wallet03-http" --port 5086` | 僅接受 profile marker、fixture ID、strict TLS／loopback target 與 dedicated SQL port 通過驗證的 fixture；啟動本次 API，驗證真 HTTP CSRF／會員登入 cookie、購物車、報價、餘額不足回滾、成功扣抵／訂單／帳務 readback 與登出；runner 清理本次合成 identity／交易資料並確認 maintenance／SKU 回復，保留資料庫 fixture。不得對 14349 `kohami-sql` 執行。 |
| 選配：M0-AC12 clean checkout restore/build／setup／seed／handoff | `dotnet restore tools/native-operations/Native.csproj --locked-mode`；`dotnet build tools/native-operations/Native.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`；`dotnet restore tests/Commerce/Commerce.Tests.csproj --locked-mode`；`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`；`python3 scripts/m0/provision-issue06-fixture.py --port 14337`；`dotnet tools/native-operations/bin/Release/net10.0/Native.dll fixture-status "$PWD" 14337` | 從乾淨 checkout 開始，選未使用的 loopback 14336–14399 port。Provisioner 拒絕既有 `.local/m0`，建立唯一 Compose project，套用鎖定 migration 與 `CommerceSeed.sql`，跑 bootstrap-denial 與 Commerce SQL assertions。`fixture-status` 唯讀核對 strict TLS 與 `KohamiM0` 存在；不核對業務列，也不取代 provisioning harness。驗收後如需丟棄本次 fixture，先讀 `.local/m0/issue06-compose-project` 並確認是本次產生的 `kohami-issue06-*`，再只對該 project 執行 `M0_SQL_PORT=<port> docker compose -p <verified-project> -f infra/m0/compose.yaml down --volumes`；不使用全域 prune、不操作其他 port/project。私有材料仍留在 `.local/m0`，依交付需求另行決定保留或移除。僅於採用[clean checkout／handoff 選配](../options/CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)後執行；不屬 M0／M4 必備驗收。 |
| Issue 06 HTTP/API／SQL 售後驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟停 runner 自有 API／worker，只清除唯一 synthetic rows 並還原 Maintenance／SKU，保留 fixture。覆蓋 API／SQL，不代表 browser UI 或完整 worker crash matrix |
| Issue 06 browser UI 售後驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟停本次 API／worker／Nuxt 與 Playwright，從 storefront 驗訂單／DEMO 收據提示、配送、退貨、驗收與 mock 退款。只清除本輪 synthetic rows、還原 Maintenance／SKU，保留 fixture |
| Issue 06 Identity AC08／09 安全與 SSR 驗收 | `python3 scripts/m0/run-issue06-aftercare-acceptance.py --identity-only` | 僅接受已存在且 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；啟 API／Nuxt，不啟 worker 或建立 Commerce 訂單；驗錯 Origin／CSRF、logout／舊 cookie replay、SSR 返回頁面。只清本輪 synthetic Identity rows/session artifact，保留 fixture |
| Issue 11 cart／SKU／quote browser acceptance | `python3 scripts/m0/run-issue06-aftercare-acceptance.py --cart-only` | 僅接受 marker／strict TLS 驗證通過的 Issue06 dedicated fixture；本次另以 `KOHAMI_M0_LOCAL_DIR`／`KOHAMI_M0_FIXTURE_ROOT` 指向乾淨臨時 checkout 及 port 14337。runner 暫開 acceptance window、啟動自身 API／Nuxt／Playwright，使用合成 Owner session 與 showcase SKU；驗 SKU、cart CRUD、優惠 quote、ETag／冪等及改價／缺貨錯誤。結束只刪 runner 記錄的 carts／quotes／idempotency rows、合成 session，還原 maintenance／暫改庫存並保留 fixture；不保留庫存、不執行 checkout 或真實付款 |

`scripts/check_kohami_planning_readiness.py` 與 `scripts/check_kohami_security_operations_plan.py` 標為 **DEPRECATED_SNAPSHOT_ONLY（不再作現行階段的通過閘門）**：其 native7／NOT_CREATED 斷言已過時，程式保留追溯，本次未修改其判定邏輯。不得把改期待值的診斷當正式 PASS；後續若重建現行 validator，須依批准來源核對範圍並保留反例測試。對目前文件採用與產品進度，以批准範圍／逐項證據為準。

## 3. 還沒綁定的命令

本節歷史 Issue 專用命令以 retired fixture 為目標，不是目前 `kohami-sql` 的操作入口。除本文件上節明確列出的唯讀服務 smoke 外，缺少新 fixture 專用命令的項目保持未執行。

| 缺口 | 哪張票補 | 交付時必須提供 |
| --- | --- | --- |
| 選配：獨立 DB restore／cutoff 後重套及對帳 | [選配改善計畫](../options/ISOLATED_RESTORE_CUTOFF_OPTION.md) | 不列本機整體驗收；若另行採用，依專用隔離目標、備份／key 相依、dispatch 禁用、撤權／刪除／hold／帳務重建與停止方法 |
| 完整三語／手機／人工可及性／效能 | kohami/17、24、26 | 實際 browser／硬體／網路／樣本與腳本、人工覆核，不能以一個 UI mock 測試代替 |
| 會員餘額／換裝／正式模式 | 各功能票 | 契約採用與實作後綁定真命令；本文件不虛構尚未存在的入口 |
| clean setup／seed／stop／reset／交付重建 | kohami/26、member-wallet/05、production-launch/01 | 各目標效果、權限、前置／版本、資料保存、未知操作續查與安全回復方法 |

## 4. 啟停、故障與展示

Issue24 要與已在使用的預設 port 並行驗收時，API 可設 `KOHAMI_API_PORT=5081`（只接受 5080–5099 且只綁 `127.0.0.1`）；dedicated Issue12 runner 可設 `KOHAMI_API_PORT` 與 `KOHAMI_SITE_PORT`，後者會更新 runner 私有 runtime config 的預期 Origin。Nuxt 可用 `NUXT_API_ORIGIN=http://127.0.0.1:5081`、`NUXT_SITE_ORIGIN=https://localhost:3001`、`NUXT_BUILD_DIR=/private/tmp/kohami-storefront-3001` 及 `NUXT_IGNORE_LOCK=1` 隔離 API target、Origin 與 build output，再以 `npm --prefix apps/storefront run dev -- --port 3001` 指定 dev port。Playwright 可設 `KOHAMI_STOREFRONT_ORIGIN=https://localhost:3001` 覆寫測試 baseURL（預設仍為 3000）。使用前確認 port 未被占用、M0 runtime config 的 Origin 與 Storefront origin 相符；憑證只讀取 `.local/m0/browser/`。停止時只停止本輪啟動的程序。已知 readiness 限制見[Issue24 驗證紀錄](../../../architecture/VERIFICATION.md#issue24-隔離連接埠合成旅程與-live-api-驗收結果2026-10-19)。

`GET /health` 只表示程序可連到資料庫。`GET /ready` 以 API principal 唯讀比對 `ops.SchemaMigrations` 中的遷移 ID 與 source hash；完全符合目前支援清單回 200，缺表、缺漏、未知遷移或 hash 不符回 503，body 只提供 `ready`／`not-ready`。它不會執行 migration、seed 或修復；改動 migration SQL 時新增向前相容 migration（不可改寫已套用 migration 的來源），同步更新 `M0SchemaContract.Supported` 與其來源比對測試。`0004_schema_readiness_grant` 撤銷 API role 對 `ops` schema 的廣域 SELECT DENY，再只授予 migration ledger 的 SELECT；其他 `ops` 物件仍沒有讀權。

`wallet03-http` fixture 失敗時，provisioner 僅在本次建立的 Compose project marker 完整且安全、且 `docker compose down --volumes` 成功後移除 `.local/kohami-demo/wallet03-http` leaf profile；父目錄 `.local/kohami-demo` 保留。若 project marker 遺失／不符或 Compose teardown 失敗，保留 leaf profile 以供人工依 marker 清理，不要使用全域 `docker compose down` 或 prune。成功驗收後若需丟棄 fixture，先讀並核對 `.local/kohami-demo/wallet03-http/wallet03-http-compose-project` 為本次唯一的 `kohami-wallet03-http-<12 hex>` 專案，再只對該 project 執行 `M0_SQL_PORT=14350 docker compose -p <verified-project> -f infra/m0/compose.yaml down --volumes`；確認完成後只刪除該 leaf profile，保留 `.local/kohami-demo` 與其他 profile。

帳號未活動排程由獨立 worker 每小時檢查一次。24 個月門檻只採 UTC `LastActivityAt`，`NULL` 不處理；通知檔寫入 `PrivateInboxPath` 後才記錄送出時間，30 日後若活動時間仍相同則停用登入並撤銷既有 session。使用者資料不刪除；最後一位有效 Owner 受保護。此為本機 mock 郵件證據，不會寄到真實信箱；一年後清除通知證據與 lifecycle audit。檢查 `.local/m0` 私有設定中的 inbox 目錄已存在且 Unix 權限為 `0700`，不要把其中通知檔加入版本控制或公開日誌。套用 `0011_issue23_account_inactivity` 時只使用既有 native migration 命令與專用 migrator 設定，worker 僅需 `m0_worker` 權限。

目前兩個 probe 都不代表 live activation；仍須核對同一 runtime principal 的 maintenance／權限與真正受保護操作。若另行啟動瀏覽器 trust 或 native confirmation 選配改善，兩者分別留證，不靠 ignoreHTTPSErrors 升格；原生維護操作本身仍須符合既有授權與 fail-closed 規則。

關閉只停止本次啟動的 API／Nuxt／worker，保留資料與未決 Operation；Unknown 不改 Failed、保留期限不重置。連線失敗先核目標／TLS／schema，不輸出秘密 connection string；不停止其他容器或全域 prune。已 Started 未決只查原來源，不另建退款。

展示順序與成功／拒絕／Unknown 及退款斷言統一見驗收文件 FINAL-CORE／WALLET／FITTING；使用獨立案例，不為下一幕任意改上一幕金融終態。Reset 需明確目標與效果，不覆寫原 DB、不直接改權限或 maintenance 解 gate。獨立 restore 演練屬選配，見專用改善計畫。
