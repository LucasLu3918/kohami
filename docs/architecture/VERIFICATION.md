# 本次重整驗證紀錄

日期：2026-10-14。狀態：STRUCTURE_COMPLETE_PRODUCT_JOURNEY_BLOCKED。本文件只記錄本次實際觀察；不把架構檢查當成產品驗收。

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

## Issue 01 後續隔離 SQL 驗證

日期：2026-10-14。這是前述重整驗證之後的補充證據，範圍僅為本次新建的 `kohami-m0-issue01-test` 專用 SQL 2025 synthetic fixture（loopback 14336）；不代表整體 M0 或本票產品驗收完成。

- `KOHAMI_M0_TEST_CONFIG="$PWD/.local/m0/fixture-private.json" dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-restore`：PASS，28 項檢查。新增檢查涵蓋 SQL 冪等 key 首次使用、同請求回放原 201、不同內容回 409；未知付款到期時釋放庫存且不建立收款；逾期後可信成功只建立一次補償、不復活訂單／庫存；重複可信成功不重複收款、補償或庫存效果。測試使用專用 synthetic rows 並清理其新增資料。
- loopback API `GET /health`：PASS，回報 `database-connected`。Commerce harness 結束時將專用 fixture maintenance gate 留在關閉狀態；建立 cart 的 POST 因此回 503 `SERVICE_UNAVAILABLE`，沒有寫入 cart。未解除 gate，因此沒有進入 HTTP checkout/payment/refund，也未執行瀏覽器旅程。
- 以上 SQL 測試只提供 M0-AC05 的部分證據。HTTP 完整旅程、provider event fault matrix、退款流程與必要瀏覽器驗收仍未執行；M0-B 保持 PARTIAL，整體狀態維持 `PARTIAL_NOT_M0_COMPLETE`。
