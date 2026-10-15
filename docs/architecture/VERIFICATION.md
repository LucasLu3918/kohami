# 本次重整驗證紀錄

日期：2026-10-14。狀態：STRUCTURE_COMPLETE_ISSUE01_JOURNEY_VERIFIED_M0_PARTIAL。本文件只記錄本次實際觀察；不把架構檢查當成產品驗收。原始檢查區段保留當時的阻擋狀態，後續 Issue 01 驗收見本文末段。

## 專案結構與程式

- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 項；文件遷移映射、Domain 專案依賴邊界、專案參考路徑均通過。
- `dotnet build Kohami.slnx -c Release`：PASS，12 個專案、0 warnings、0 errors（本次抽離後執行）。
- Commerce 純購物車／價格規則：PASS，12 項；SQL product checks 因未指定 dedicated fixture 而 NOT_EXECUTED。
- Core API contract 測試：PASS，11 項。
- Identity TOTP 純規則：PASS，24 項。
- Native 合成簽章／scope／expiry／restore 防護測試：PASS，18 項；live OS confirmation NOT_EXECUTED。
- `scripts/build_kohami_canonical.py`：PASS，core 104／Phase2 36，共 140 endpoints；產品測試 NOT_EXECUTED。
- Storefront `npm run typecheck`：PASS；Nuxt production build：NOT_EXECUTED。
- `scripts/check_kohami_planning_readiness.py`：本環境缺少 `yaml`（PyYAML）套件，暫存安裝亦因套件網路不可用而未完成；未執行到快照斷言。腳本已改為掃描 `docs/issues/kohami`。不要將環境錯誤解讀成快照 PASS 或 FAIL。

## 本機服務與瀏覽器

- API `GET /health`：HTTP 200，`database-connected`、`mode=mock`、`datasetId=demo_m0`。回應中的 `activation=requires-native-reconciliation` 是目前 `apps/api/Program.cs` 寫死的值，不能證明 SQL 中即時的 maintenance／activation 狀態。
- API `GET /api/v1/auth/session`：HTTP 200，Anonymous。
- API `GET /api/v1/carts/current`：HTTP 503 `SERVICE_UNAVAILABLE`；HTTPS storefront 同路徑也回 HTTP 503。未送出 POST、加入商品、建立訂單或啟動模擬付款。
- 以 API 同一 runtime 設定／SQL principal／TLS 執行一次暫存唯讀 preflight：`BLOCKED MAINTENANCE_REQUIRED`。因此本次購物車 503 的直接阻擋原因已確認為資料庫 maintenance gate；沒有使用 DBA 權限，也沒有做任何資料變更。
- `CommerceStore.Transaction` 在每次交易前檢查 `m0.SecurityState.Maintenance`。API 把該 SQL 錯誤和所有其他 `SqlException` 都映射成相同 503，導致前端只顯示一般連線錯誤。`/health` 目前的 `activation` 字串寫死，不是 live readiness 證據。
- 瀏覽器按鈕因此保持 disabled，完整 cart／quote／checkout／payment 旅程未驗收。解除 maintenance 後，先以 API runtime principal 重跑唯讀 readiness，再驗 cart；不能以 DBA probe 代替。

維護狀態只能由已核准的原生授權／reconciliation 流程解除；不要直接更新 `m0.SecurityState` 或使用 DBA／SA 繞過 gate。

本次重整完成不自動將原 M0-AC01–12 或八個 CHK 標為全部 PASS；未完成的產品旅程維持 BLOCKED／NOT_EXECUTED。

## Issue 01 後續隔離 SQL 驗證（首次檢查）

日期：2026-10-14。這是前述重整驗證之後的補充證據，範圍僅為本次新建的 `kohami-m0-issue01-test` 專用 SQL 2025 synthetic fixture（loopback 14336）；不代表整體 M0 或本票產品驗收完成。

- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-restore`：PASS，28 項檢查。新增檢查涵蓋 SQL 冪等 key 首次使用、同請求回放原 201、不同內容回 409；未知付款到期時釋放庫存且不建立收款；逾期後可信成功只建立一次補償、不復活訂單／庫存；重複可信成功不重複收款、補償或庫存效果。測試使用專用 synthetic rows 並清理其新增資料。
- loopback API `GET /health`：PASS，回報 `database-connected`。Commerce harness 結束時將專用 fixture maintenance gate 留在關閉狀態；建立 cart 的 POST 因此回 503 `SERVICE_UNAVAILABLE`，沒有寫入 cart。未解除 gate，因此沒有進入 HTTP checkout/payment/refund，也未執行瀏覽器旅程。
- 以上 SQL 測試只提供 M0-AC05 的部分證據。HTTP 完整旅程、provider event fault matrix、退款流程與必要瀏覽器驗收仍未執行；M0-B 保持 PARTIAL，整體狀態維持 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 01 購物旅程產品驗收

日期：2026-10-14。狀態：PASS（Issue 01 R03–R05／M0-AC03–AC05 範圍）；整體 M0 仍為 `PARTIAL_NOT_M0_COMPLETE`。

- `/usr/local/share/dotnet/dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -v:minimal -m:1 -p:UseSharedCompilation=false`：PASS，0 warnings／0 errors。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" /usr/local/share/dotnet/dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-build --no-restore`：PASS，32 項；其中 SQL 20-client last-item race 僅一筆成功，persisted stock／reservation snapshot 為 stock 0、held reservation 1；多 SKU 後續 SKU 售罄確實回滾前一筆扣庫存；結帳冪等重播、付款明確失敗後保留預留並允許重試、Unknown 到期釋放、晚到可信成功補償、額外成功補償及重複成功去重均通過。
- `python3 scripts/m0/run-issue01-acceptance.py`：PASS；Playwright 1 test／1 passed，約 5.1 分鐘。HTTP／瀏覽器涵蓋匿名購物車建立及同 key 重播、商品加入、不支援區域與客戶金額拒絕、小數／溢位數量拒絕且購物車不變、商品價變拒絕、取貨運費 0、報價到期拒絕與重試算、結帳同 key 重播、付款 Unknown 狀態重新載入後仍可查且前端不顯示重付按鈕。
- `npm run typecheck`：PASS；`./node_modules/.bin/playwright test --config ./playwright.config.ts --list`：PASS，載入 1 個測試；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 項。
- runner 僅使用 localhost:14336 的 `demo_m0` 專用 synthetic fixture，結束時關閉 maintenance acceptance window、恢復商品價格並停止其自行啟動的服務。瀏覽器使用 Playwright `ignoreHTTPSErrors`，故不表示作業系統憑證信任已安裝；沒有真實金流、退款履行或配送發生。
- M0-AC03–05 因本票購物流程情境與 SQL／HTTP 證據更新為 PASS。退款 HTTP 全流程、完整 provider event fault matrix、多 worker race/replay、M0-A 原生 OS 驗收及 M0-C 復原／交接仍未通過。因此整體 M0 與 M0-B 仍維持 PARTIAL。

## Issue 02 售後 UI 驗收追蹤

日期：2026-10-15。狀態：UI mock PASS；專用 SQL harness PASS；真實 API／SQL browser journey NOT_EXECUTED。

- `npm exec playwright -- test --config playwright.config.ts --grep 'shopper can follow mock delivery'`：PASS，1 test／1 passed。此案例以測試內確定性 mock API 回應驗證 UI；包括配送狀態更新、顧客退貨申請、營運受理／收件／驗收、退款前 TOTP 重新驗證 UI，以及 DEMO 憑證無稅務效果揭露。它不呼叫真實 Commerce API。
- `npm run typecheck` 與 Nuxt production build：PASS；Commerce harness Release build：PASS，0 warnings／0 errors；SQL Server 2025 loopback fixture 上 51 項 Commerce checks PASS，包含 `orders.read` 授權正反向案例。
- 驗收 runner 的 SQL alias／TLS pin 設定回歸測試 4 項 Python tests PASS；Native Release build PASS，42 項 synthetic checks PASS。
- 真實 API／SQL browser journey 與原生 OS confirmation 未執行；本紀錄不宣稱 Issue 02 HTTP journey 或 M0-AC06～12 全數通過，整體仍為 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 06 結案標準盤點（2026-10-15）

狀態：未結案。依 S0 順序，Issue 04／05 的真 API 售後及供應商契約證據仍未齊；本輪只補 Issue 06 的候選基線證據與狀態盤點，不以文件檢查代替產品驗收。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，52 項 dedicated synthetic SQL／boundary checks；涵蓋庫存競爭與回滾、checkout 冪等、付款拒絕／Unknown／逾期／晚到與重複事件，以及退款額度／worker receipt 調整邊界。只支持涵蓋到的 M0-AC，不表示 provider fault matrix 或兩個 worker 的競爭矩陣完成。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors；`npm run typecheck`（`apps/storefront`）：PASS。
- `python3 scripts/m0/run-issue01-acceptance.py`：兩個 mock／UI Playwright 案例 PASS；Issue 02 真 API／SQL 售後案例因隱藏互動提示通道不可用，在任何憑證輸入前停止。runner 關閉自己的 acceptance window；此結果標 BLOCKED，未輸入或記錄密碼／TOTP。
- Identity SQL 測試專案缺 `project.assets.json`；數次 locked restore 無套件輸出後取消。此候選的 Identity SQL 測試 NOT_EXECUTED；先前 30 項通過只列歷史證據。Native 測試專案同樣缺 assets，未重跑；模擬原生結果不替代 Secure Enclave／OS user-presence。
- 對應 M0 狀態：AC03–05 維持既有 PASS；AC07 由 NOT_EXECUTED 更新為 PARTIAL（有 SQL／mock aftercare 邊界證據，無完整 HTTP refund）；AC01–02、08–11 仍 PARTIAL；AC06、AC12 仍 NOT_EXECUTED。整體保持 `PARTIAL_NOT_M0_COMPLETE`。

Issue 06 五項工作盤點：框架／core34／schema/API 候選及缺口已有來源文件；品質矩陣及執行前置已有定義；基線只部分可重跑；真供應商 sandbox、browser aftercare、worker fault matrix、獨立 restore／cutoff 對帳及原生 MFA／OS 證據待完成。開發／服務／維護與負載情境尚無足夠實測輸入以產生可信工時／成本估算；不得用舊 20–80 人日或 NT$8,000/月替代估算。故 Issue 06 work status 保持 claimed，五項清單不整體勾選。

## Issue 06 本機環境與共同驗收續跑（2026-10-15）

狀態：部分共同證據 PASS；Identity／Native 專案測試與 OS 驗收仍未執行；Issue 06 不結案。候選為目前本機 `main`（`04071e5`）；沒有產品程式碼異動。

- 工具：`dotnet --version` 回報 `10.0.401`；`node --version` 回報 `v26.11.1`；`npm --version` 回報 `11.20.0`。`./infra/sql-server/manage.sh status` 與 `check` 無法連上 OrbStack Docker API socket（permission denied）；現存 `.local/m0/issue06-compose-project` 標記及 localhost `14336` listener 可見。
- `dotnet tools/native-operations/bin/Release/net10.0/Native.dll development-fixture-bootstrap-checks .local/m0/fixture-private.json`：PASS。Native guard 以 strict TLS 驗證 dedicated `localhost:14336/KohamiM0/demo_m0`；錯誤 instance bootstrap 與直接 DML 均被拒絕，之後確認 fixture state 未改變。此為 fixture SQL 權限證據，並非 Native 測試專案或真實 OS user-presence 驗收。
- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：先在一般受限 shell 中被 loopback 權限拒絕，當時未進行 SQL 寫入；以核准的本機連線範圍重跑後 PASS，輸出 51 個 PASS assertions，涵蓋本次候選的合成 Commerce SQL／boundary checks。這不等於完整 HTTP 退款、provider fault matrix 或多 worker 競爭驗收。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false`：PASS，0 warnings／0 errors；`npm run typecheck`（`apps/storefront`）：PASS（npm 對現有 `http-proxy` 環境設定發出 warning）；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。
- Identity 與 Native 測試專案缺少 `project.assets.json`。Identity locked restore、限定本機 NuGet cache 的 Identity／Native restore 都停在「正在判斷要還原的專案」，沒有產生 assets；因此本候選 Identity SQL tests 與 Native test project 均維持 NOT_EXECUTED。Identity 先前 30 項及 Native 先前合成測試只作歷史證據。
- 本輪未啟動瀏覽器／API journey，未執行 Owner authenticator、Secure Enclave、OS user-presence、憑證安裝、restore／cutoff 對帳或供應商測試。保留 M0-AC 狀態及 `PARTIAL_NOT_M0_COMPLETE`，不以本機工具可用或 fixture SQL guard 通過推升整體驗收。

## Issue 06 Identity／Native restore 恢復與候選測試（2026-10-15）

狀態：Identity 與 Native 候選測試 PASS；Issue 06 仍未結案。測試候選為 `04071e5` 加上 Native lock 同步（現已提交為 `cdafffc`）；本次沒有產品程式碼變更。

- NuGet 來源只有官方 `https://api.nuget.org/v3/index.json`。受限執行環境的網路代理回覆 `403 blocked-by-allowlist`，是先前 restore 沒有輸出及停在專案判斷階段的環境原因。取得核准網路存取後，Identity `dotnet restore tests/Identity/Identity.Tests.csproj --locked-mode --verbosity minimal` 約 4.3 秒完成，並生成目前候選 assets。
- Native 首次 `dotnet restore tests/Native/NativeTests.csproj --locked-mode --verbosity minimal` 明確回報 `NU1004`：目前 `NativeTests.csproj` 的相依圖包含 `IdentityAccess.Domain`，舊 lock file 未記載。以 `dotnet restore tests/Native/NativeTests.csproj --force-evaluate --verbosity minimal` 依目前專案參考同步 `tests/Native/packages.lock.json`，再以 locked restore 成功完成；lock 差異只有新增該專案參考及 Native 對它的相依項目。
- `M0_IDENTITY_SQL_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet test tests/Identity/Identity.Tests.csproj -c Release --no-restore --logger 'console;verbosity=normal'`：PASS，30/30，含 dedicated synthetic SQL fixture；涵蓋 TOTP 規則、SQL 授權與狀態、併發 proof／recovery code 消費及 enrollment。
- `dotnet build tests/Native/NativeTests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。`dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`：PASS，47 項簽章／scope／expiry／target／namespace／cutoff 合成檢查。
- 第一個 `dotnet run --project tests/Native/NativeTests.csproj -c Release --no-restore` 無輸出且未完成；停止該程序後，使用已成功建置的 DLL 執行，取得上述 PASS。Native 合成測試不證明真實 Secure Enclave 或 OS user-presence；本輪也沒有執行 Owner authenticator／TOTP／瀏覽器驗收。M0-AC 狀態及整體 `PARTIAL_NOT_M0_COMPLETE` 維持不變。

## Issue 05 本機模擬物流與交易憑證契約（2026-10-15）

狀態：Issue 05 本機 mock 契約與專用 SQL 情境 PASS；不代表正式物流、稅務、供應商或整體 M0 完成。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v minimal`：PASS，0 warnings／0 errors。TDD 先新增費用政策與快照回歸案例，確認缺少 `MockShippingFeePolicy` 時建置失敗，再完成實作並重建成功。
- `KOHAMI_M0_TEST_CONFIG=.local/m0/fixture-private.json dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，專用 `demo_m0` synthetic SQL fixture 共 67 項 assertions。含宅配／取貨費用與 `fee_demo_v1` 版本、未支援區域 `FEE_UNAVAILABLE`、報價費用快照固定、出貨重複／過期事件拒絕與例外後恢復、憑證開立失敗後查詢重試、終態不可被晚到失敗降級、退款與憑證調整狀態獨立，以及測試 Cart／Quote／工作列清理。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`：PASS，0 warnings／0 errors；`npm --prefix apps/storefront run typecheck`：PASS；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests；`python3 -m unittest discover -s tests/m0 -v`：PASS，7 tests；Identity tests：24 passed／6 skipped；Native synthetic executable：47 checks PASS。
- Core tests NOT_EXECUTED：`tests/Core/obj/project.assets.json` 缺失，未執行 restore。M0 runner 與 Identity／Native 測試在受限環境遇到 socket 權限後以核准的本機執行權限重跑；Core 未列為通過。
- 範圍只使用 mock fee schedule 與 `demo_m0` synthetic SQL；未呼叫真物流、金流、電子發票、稅務或郵件供應商，未驗正式稅費／運費政策，也未執行真 API／SQL browser aftercare。Issue 06 仍未結案，整體狀態維持 `PARTIAL_NOT_M0_COMPLETE`。

## Issue 06 本機基線續跑（2026-10-15）

候選：`588dc7917389dfd9e5c28658d11d8c6cff8a3f2a`。本次沿用既有 `kohami-issue06-*` 專用 SQL 2025 synthetic fixture（loopback `127.0.0.1:14336`）；沒有重建、刪除或改用其他資料庫。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，67 個 dedicated synthetic SQL／boundary assertions；涵蓋付款、退款、庫存競爭／回滾、物流與 DEMO 憑證、scope／maintenance、事件重送及測試資料清理。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nr:false -v:minimal`：PASS，0 warnings／0 errors。
- `npm run typecheck`（`apps/storefront`）：PASS；npm 對現有 `http-proxy` 設定顯示環境 warning。
- `M0_IDENTITY_SQL_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet test tests/Identity/Identity.Tests.csproj -c Release --no-build --no-restore --logger 'console;verbosity=normal'`：PASS，30/30；含 TOTP step／recovery code 併發消耗與 SQL 權限測試。
- `dotnet tests/Native/bin/Release/net10.0/NativeTests.dll`：PASS，47 項 synthetic scope／簽章／expiry／target／namespace／cutoff checks；live OS confirmation 未執行。
- `DOTNET_ENVIRONMENT=Development dotnet tools/native-operations/bin/Release/net10.0/Native.dll development-fixture-bootstrap-checks .local/m0/fixture-private.json`：PASS；wrong-instance bootstrap 與 direct DML 均遭拒，fixture state 保持不變。
- `python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。
- `python3 -m unittest discover -s tests/m0 -v`：sandbox 內因 Unix socket `PermissionError` 中止；以核准的本機執行權限重跑後 PASS，7/7 runner regression tests。

此結果支持目前 candidate 的共同建置與局部 SQL／Identity／Native 基線，不代表 12 個 M0 acceptance cases 全數通過。Issue 06／M0 尚缺完整 API／browser 售後與退款、worker crash/restart 與失敗 replay fault matrix、獨立資料庫 restore＋cutoff ledger 對帳、OS 憑證信任與 user-presence 證據；provider sandbox 與正式部署不在目前批准範圍。Issue 06 work status 保持 claimed。

## Issue 06 雙程序付款事件競爭驗收（2026-10-15）

狀態：同事件併發去重情境 PASS；完整 worker fault matrix 與 Issue 06 結案條件仍未完成。

- TDD：先將測試掛入 Commerce runner，Release 建置因缺少 `TwoWorkerPaymentRace` helper 失敗（2 個預期 CS0103）；新增 helper 後 Release 建置 PASS，0 warnings／0 errors。
- `env KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：在既有 dedicated synthetic SQL fixture PASS，69 個 `PASS` assertions。新增兩個獨立 `dotnet` worker 程序透過同一 gate 同時提交相同 mock payment event，確認一筆已套用 Inbox、一筆 Primary collection、付款／訂單成功、reservation consumed 與一份 DEMO receipt。
- race 使用唯一 synthetic IDs；完成或失敗皆清理新增 Inbox／帳務／工作／文件資料並還原 SKU 原庫存。沙箱首次 loopback 連線被 OS 拒絕，核准本機 fixture 執行後通過。
- 本案例只證明同事件競爭去重，不覆蓋 worker crash/restart、lease 過期、部分故障後重試、HTTP/browser 售後退款、獨立 restore／cutoff 對帳或 OS 信任／user-presence。Issue 06 維持 claimed。

## 目前驗收範圍補充（2026-10-15）

以上具日期的 NOT_EXECUTED 紀錄仍是當時真實執行狀態；依使用者後續範圍決定，Owner TOTP／驗證器 app、原生 OS trust/user-presence 及獨立 restore／cutoff 對帳改列選配改善，不再阻擋 Issue 06、M0、M4 或本機核心整體驗收。此分類不把未執行改寫成 PASS。Issue 06／M0 仍為 `claimed`／`PARTIAL_NOT_M0_COMPLETE`，其餘 API／browser、worker crash/restart fault matrix、clean handoff 等必備缺口照常追蹤；restore／cutoff 設計集中於 [選配改善計畫](../planning/kohami/options/ISOLATED_RESTORE_CUTOFF_OPTION.md)。
