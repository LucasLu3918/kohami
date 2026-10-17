# 16: 會員購物與受保護的訂單紀錄

**What to build:** KOHAMI 會員使用既有資料庫帳號密碼登入、以會員身分結帳並查閱自己的訂單；訪客仍可免註冊結帳與安全查詢交易，不能存取他人資料。

**Blocked by:** 13：完成付款並顯示可信結果

**Status:** ready-for-agent

**Work status:** resolved

- [x] 既有 KOHAMI 資料庫帳號密碼 session 可使用會員購物車結帳；會員中心僅顯示伺服器依登入 UserId 篩選的訂單摘要。
- [x] 訪客仍可免註冊結帳與使用既有訂單能力查詢；相同 Email 不會自動歸戶，訪客須明確提交與訂單／瀏覽器綁定的 claim capability。
- [x] 登出／匿名 session 不取得會員訂單；本人可讀、其他會員讀取以 404 隱藏；訪客歸戶以 SQL 原子更新 Owner、消耗能力票券並記錄 idempotency。
- [x] 覆蓋會員清單、服務錯誤與歸戶成功；不新增通知儲存或外部通知，私有 API 回應沿用 no-store。

## M2本機mock規劃補充（2026-10-13）

本票沿用[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)的訪客能力、付款期限、原操作重送及訂單快照；不因會員化改寫原聯絡資料或轉移瀏覽器能力。登入只使用既有 KOHAMI 帳號密碼，不新增註冊、通知通道或第二因素流程。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R06–R07／AC06–AC07；User Stories 18、26、80。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

## Resolution

**Work status:** resolved（2026-10-17，本機 mock 範圍）。會員 checkout 使用既有資料庫帳號密碼身份；新增私有 `GET /orders` 與訪客 proof 綁定的 `POST /orders/{id}/member-claims`。Dedicated SQL 驗收由 member session 完成購物車 quote／checkout，確認新訂單 `OwnerId` 為登入 `UserId`。訪客訂單只有在目前 member session、原瀏覽器與未過期／未消耗 `claim` capability 同時成立時才可歸戶；Owner、capability consumption 與 idempotency record 同一 SQL transaction commit。Email 相同不會批次歸戶，原訂單聯絡 Email 保留。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` 與 `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。Storefront `npm run typecheck`：PASS。
- `python3 scripts/m0/provision-issue06-fixture.py --port 14343`：建立 localhost SQL Server 2025 dedicated fixture、strict TLS 及 migrations 0001–0008。Commerce SQL acceptance PASS，包含 member session quote／checkout 的訂單 OwnerId、本人清單／單筆讀取、跨會員 404、匿名拒絕、Email 相同不自動歸戶、claim 重送、原聯絡 Email 保留、另一會員遭拒，以及兩位會員同時競爭一張 claim capability 時唯一 winner；SQL 確認 Owner／capability／idempotency 一致。完整既有 Commerce SQL regression harness 同次通過。
- `npx playwright test tests/issue16-member-orders.spec.ts --config=playwright.config.ts`：4/4 PASS，涵蓋會員清單與登出清空、明確歸戶、匿名不送出訂單查詢、503 不留下舊訂單且只顯示對應錯誤。
- 專用 Compose project `kohami-issue06-8463df8439b1` 已於驗收後移除 containers、network、volumes；臨時 `.local/m0` credentials／certificates／session key ring、TLS files、Playwright artifacts 與依賴 symlink 已刪除。主 checkout 的 `.local/m0` 與展示資料未使用或更動。
- 本輪 build 使用主 checkout 已存在的 NuGet assets／host cache；因 NuGet restore 長時間停留於 project determination，沒有證明乾淨環境 restore。Browser tests 使用 mock API；live API／Storefront 旅程、正式外部通知與真實 provider 不在本票本機 mock 證據範圍。新增路由採 Commerce module contract，不擴大已批准 core34 OpenAPI 執行契約。

