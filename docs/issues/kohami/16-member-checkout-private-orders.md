# 16: 會員購物與受保護的訂單紀錄

**What to build:** KOHAMI 會員使用既有資料庫帳號密碼登入、以會員身分建立訂單並查閱自己的訂單；訪客不可建立新訂單或將歷史訪客訂單歸戶至會員。歷史訂單讀取及售後能力依訪客存取契約處理。

**Blocked by:** 13：完成付款並顯示可信結果

**Status:** ready-for-agent

**Work status:** resolved

**目前範圍更新（2026-10-22）：** 訪客訂單歸戶功能已依使用者要求退役，會員中心表單、`POST /orders/{id}/member-claims` API 與 `claim` 驗證意圖均已移除。下列勾選項與驗收結果記錄的是退役前的歷史實作，不代表目前仍提供此功能。新訂單僅能由登入會員建立。

- [x] 登入會員可使用本人購物車結帳，會員中心僅顯示伺服器依登入 UserId 篩選的訂單摘要。
- [x] 訪客不可建立新訂單或讀取會員訂單；歷史訪客訂單的單筆讀取及售後能力依訪客存取契約處理。
- [x] 不因 Email 相同轉移歷史訂單；會員清單及私有 API 回應沿用 no-store。

## M2本機mock規劃補充（2026-10-13）

本票沿用[M2結帳／訂單詳規](../../planning/kohami/commerce/CHECKOUT_ORDER_PLAN.md)與[訪客存取契約](../../planning/kohami/identity/GUEST_ORDER_ACCESS.md)的歷史訂單讀取、售後能力、付款期限、原操作重送及訂單快照；不因會員化改寫原聯絡資料。登入只使用既有 KOHAMI 帳號密碼，不新增註冊、通知通道或第二因素流程。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R06–R07／AC06–AC07；User Stories 18、26、80。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。

## Resolution

**退役前實作紀錄（截至 2026-10-17；不代表現行功能）：** 曾完成會員 checkout／本人訂單清單及訪客訂單歸戶的本機 mock 實作與驗收。2026-10-22 起訪客訂單歸戶及其 API／驗證意圖已退役；目前只保留登入會員建立新單，以及歷史訪客訂單單筆讀取、取消與退貨流程。原 checkout 建單會將 `OwnerId` 設為登入 `UserId`，Email 相同不會批次轉移歷史訂單，原訂單聯絡 Email 保留。

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` 與 `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`：PASS，0 warnings／0 errors。Storefront `npm run typecheck`：PASS。
- `python3 scripts/foundation/provision-issue06-fixture.py --port 14343`：建立 localhost SQL Server 2025 dedicated fixture、strict TLS 及 migrations 0001–0008。退役前 Commerce SQL acceptance PASS，涵蓋會員 checkout／本人訂單讀取、匿名拒絕及舊歸戶流程。完整既有 Commerce SQL regression harness 同次通過。
- `npx playwright test tests/issue16-member-orders.spec.ts --config=playwright.config.ts`：退役前 4/4 PASS，涵蓋會員清單與登出清空、舊歸戶流程、匿名不送出訂單查詢、503 不留下舊訂單且只顯示對應錯誤。
- 專用 Compose project `kohami-issue06-8463df8439b1` 已於驗收後移除 containers、network、volumes；臨時 `.local/foundation` credentials／certificates／session key ring、TLS files、Playwright artifacts 與依賴 symlink 已刪除。主 checkout 的 `.local/foundation` 與展示資料未使用或更動。
- 本輪 build 使用主 checkout 已存在的 NuGet assets／host cache；因 NuGet restore 長時間停留於 project determination，沒有證明乾淨環境 restore。Browser tests 使用 mock API；live API／Storefront 旅程、正式外部通知與真實 provider 不在本票本機 mock 證據範圍。新增路由採 Commerce module contract，不擴大已批准 core34 OpenAPI 執行契約。

