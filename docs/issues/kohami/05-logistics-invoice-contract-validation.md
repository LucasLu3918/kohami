# 05: 建立模擬物流與交易憑證契約

**What to build:** 實作示意運費、模擬配送與無稅務效力憑證契約，不串接服務商。

**Blocked by:** 02：確認配送、售後與發票規則

**Status:** ready-for-agent

**Work status:** resolved（local mock 契約與 dedicated synthetic SQL 情境；正式服務商未驗）

- [x] 提供版本化示意費用表與有效／無效區域情境；無有效費用不得確認，歷史快照不可覆寫。
- [x] 模擬配送合法轉移、重送／亂序及異常恢復可驗證；不提供真實承運追蹤或寄送功能。
- [x] 建立 DEMO 憑證、部分／全額調整、失敗／未知及查詢重試；明示無稅務效力，退款與憑證狀態分開。
- [x] 提供18／21可使用的情境契約與證據；真實運費、資格、物流／發票服務商與沙盒延期，不當作本階段開發前置。

## 現行 mock 階段範圍（2026-10-13）

現階段驗收改為上述mock契約與情境實證；正式服務商條件延期。僅本次政策編修不標resolved。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## 來源與範圍

- 階段：前置決策與實證。
- 驗收對應：V05；User Stories 16、26、29、77。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


## 正式串接延期清單（原驗收條件，非本階段阻擋）

- [ ] 以同一配送及退款情境查核候選服務的申請條件、運費、重量／體積限制、物流狀態及發票介面，保留日期、來源與限制。
- [ ] 於授權測試環境驗證配送事件重送／亂序、失敗恢復，以及交易、取消、退款與發票處理的一致性。
- [ ] 確認服務商及政策相容性，列出事件、查詢、錯誤及恢復契約；尚未取得資格或證據的項目維持未通過。
- [ ] 正式採購、申請、寄送與發票開立依當次授權，不把查核或沙盒結果當成正式營運批准。

上述資格、契約與真實沙盒仍未完成；不得以 mock 實證取代正式證據。

- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

- 2026-10-15：完成 local mock 費用表、配送轉移及 DEMO 憑證／調整情境；測試證據與限制見下方 Resolution。正式服務商資格、實際報價與沙盒仍延期。

## Resolution

Issue 05 的 local mock 範圍完成。Commerce Domain 使用不可變 `fee_demo_v1` 費用表：`mock_home`／`demo_supported` 為 TWD 60，`mock_pickup`／`demo_supported` 明列 TWD 0；未知方法／區域沒有 fallback，Quote 回 `FEE_UNAVAILABLE`。Quote 與後續訂單沿用既有 snapshot 欄位與 SQL transaction，後續 Quote 不會改寫先前費用或版本；未變更 API wire shape、SQL schema 或 migration。

Dedicated synthetic SQL Commerce harness 新增並通過費用有效／無效、快照保存、配送重送／亂序拒絕／Exception 恢復、DEMO 憑證失敗後查詢重試、遲到失敗不降級已發出憑證，以及退款未知與憑證調整未知分離等情境。完整金額 DEMO 憑證為 TWD 560；部分退款調整為 TWD 500。失敗／未知不宣稱完成，查詢重試不重複執行退款；結束後確認專用合成 Cart、Quote、工作與冪等列清除。

驗證證據：

- `dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v minimal`：PASS，0 warnings／0 errors。
- `KOHAMI_TEST_CONFIG=.local/foundation/fixture-private.json dotnet tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll`：PASS，67 個 Commerce 純規則／SQL assertions；只使用 `IdentitySqlTestDedicated=true` 的 localhost:14336 `kohami_local` 合成 fixture，測試資料清除 assertion PASS。
- `dotnet build apps/api/Api.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`：PASS，0 warnings／0 errors。
- `npm --prefix apps/storefront run typecheck`：PASS；`python3 -m unittest discover -s tests/architecture -v`：PASS，3 tests。

限制：此證據只涵蓋 mock adapter 與本機 dedicated synthetic SQL；未呼叫承運／發票／稅務服務、未驗正式報價或供應商資格，亦未在本次候選重跑完整瀏覽器旅程。DEMO 憑證不具稅務效力。整體 Foundation／Issue 06 仍維持各自既有狀態，不因本票 resolved 而推定完成。
