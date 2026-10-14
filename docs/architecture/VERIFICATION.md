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

維護狀態只能由已核准的原生授權／reconciliation 流程解除；不要直接更新 `m0.SecurityState`、使用 DBA／SA 繞過 gate，或把 `initialize-rings` 當成 Owner activation（它明確不解除此 gate）。

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
- M0-AC03–05 因本票購物流程情境與 SQL／HTTP 證據更新為 PASS。退款 HTTP 全流程、完整 provider event fault matrix、多 worker race/replay、M0-A 原生 OS／Owner 驗收及 M0-C 復原／交接仍未通過；因此整體 M0 與 M0-B 仍維持 PARTIAL。

## Issue 02 售後 UI 驗收追蹤

日期：2026-10-15。狀態：UI mock PASS；專用 SQL harness PASS；真實 API／SQL browser journey NOT_EXECUTED。一次 authenticator app TOTP proof 已接受，但啟用確認碼比對失敗，未完成 Owner bootstrap。

- `npm exec playwright -- test --config playwright.config.ts --grep 'shopper can follow mock delivery'`：PASS，1 test／1 passed。此案例以測試內確定性 mock API 回應驗證 UI；包括配送狀態更新、顧客退貨申請、營運受理／收件／驗收、退款前 TOTP 重新驗證 UI，以及 DEMO 憑證無稅務效果揭露。它不呼叫真實 Commerce API。
- `npm run typecheck` 與 Nuxt production build：PASS；Commerce harness Release build：PASS，0 warnings／0 errors；SQL Server 2025 loopback fixture 上 51 項 Commerce checks PASS，包含 Owner `orders.read` 授權正反向案例。bootstrap wrong-instance 與直接 DML 拒絕檢查 PASS，狀態未改變。
- 後續本機操作回報 bootstrap 失敗與 runner 錯誤拒絕 pinned localhost connection。只讀／負向 SQL bootstrap checks 再次通過，確認 bootstrap marker 仍可用、無 Owner、maintenance 未改變。runner 已修正 `Initial Catalog`／`Database` 及 `Data Source`／`Server` 別名驗證，改由 fixture-private 中已 pin 的 worker connection 設定執行；新增 4 項 Python 回歸測試 PASS。Native Release build PASS，42 項 synthetic checks PASS，並修正啟用碼 5 分鐘期限從發碼時計算。失敗當下的 InvalidOperationException 具體原因未能回溯；後續僅輸出白名單安全錯誤碼以利診斷。
- 隨後一次真實 bootstrap 操作已由 authenticator app 通過 6 位 TOTP proof；啟用確認碼比對失敗，未呼叫 Owner SQL 交易。接續的驗收 preflight 因 Owner 尚未建立而拒絕，屬預期阻擋。bootstrap 將 TOTP seed 保存在記憶體至交易成功；失敗退出後 seed 已丟棄，需以新 URI 重做 authenticator enrollment。確認碼僅在同一個本機終端輸入，不得貼到聊天。
- 已建立真實 API 售後 Playwright journey 與 Developer-only fixture bootstrap。fixture provision 可建立受限 SQL fixture；一次 authenticator app TOTP proof 已接受，但啟用確認碼比對失敗，Owner SQL transaction 未執行。完整 Owner bootstrap 與真實 API／SQL browser journey 仍 `NOT_EXECUTED`；原生 Secure Enclave OS confirmation 亦仍未執行。
- 此結果證明售後 UI mock journey 與 Commerce SQL harness 通過，不代表 Issue 02 後端旅程或 M0-AC06～12 全數通過；整體仍為 `PARTIAL_NOT_M0_COMPLETE`。
