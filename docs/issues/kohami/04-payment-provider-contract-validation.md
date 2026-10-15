# 04: 建立模擬金流契約與情境驗證

**What to build:** 實作受控後端 mock 付款／退款契約，保留未來真實服務商選型清單。

**Blocked by:** 01：確認結帳與庫存交易規則；02：確認配送、售後與發票規則

**Status:** ready-for-agent

**Work status:** resolved

- [x] 實作模擬付款／退款、查詢與台帳契約，識別模式、資料集、訂單、操作、金額／幣別與事件版本；顧客顯示模擬結果。
- [x] 驗證成功、明確拒絕、未知、重複／亂序、金額不符及晚到成功；前台不能自行偽造已付款或已退款。
- [x] 重送去重、退款額度保留、原交易關聯與結果查核可重現；無外部收款或供應商 API 呼叫。
- [x] 輸出可供13–15／20使用的 adapter 情境與驗證證據；真實資格、費率、服務商沙盒列延期清單，不假稱完成。

## 現行 mock 階段範圍（2026-10-13）

現階段驗收改為上述mock契約與情境實證；正式服務商條件延期。僅本次政策編修不標resolved。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## 來源與範圍

- 階段：前置決策與實證。
- 驗收對應：R03–R05／AC03–AC05；V04；User Stories 20–25、75–76。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


## 正式串接延期清單（原驗收條件，非本階段阻擋）

- [ ] 用相同交易情境查核候選金流的申請資格、付款方式、費用、通知驗證、主動查詢、退款及對帳契約，記錄日期、官方來源與限制。
- [ ] 在已授權且不正式收款的沙盒中驗證重送、逾時、未知結果、通知亂序及晚到成功；不偽造內部訂單或庫存結果。
- [ ] 列出費率與能力比較、推薦與風險，取得服務商及重大交易條件確認；無實證不得標記通過。
- [ ] 輸出可供後續付款旅程使用的請求／事件、可信性、錯誤、重試／冪等及對帳契約；付費、開戶與正式收款另依實際授權。

上述資格、契約與真實沙盒仍未完成；不得以 mock 實證取代正式證據。

- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

- 2026-10-15：完成 mock 金流事件契約與情境驗證；專用 synthetic SQL Commerce harness 53 項通過。未執行真實服務商／沙盒；多 worker race/replay 留在 M0 後續驗收。

## Resolution

依目前採用的 mock 政策完成付款／退款事件 envelope 與 worker 驗證：事件持久化 mode、dataset、order、operation、amount、currency、contract version 與 outcome；不符資料被隔離且不改台帳；重送去重，成功後舊拒絕不降級，未知與晚到成功維持補償規則。付款結果只由受控後端 worker／授權情境套用，顧客仍讀取伺服器保存的模擬狀態。

證據：`dotnet build tests/Commerce/Commerce.Tests.csproj -c Release --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -nodeReuse:false` 成功（0 警告／錯誤）；`KOHAMI_M0_TEST_CONFIG=.local/m0/fixture-private.json dotnet run --project tests/Commerce/Commerce.Tests.csproj -c Release --no-build` 在 dedicated synthetic fixture 通過 53 項 SQL／規則檢查，新增覆蓋金額不符隔離、舊拒絕事件、同事件重播與台帳不重複。M0 wider multi-worker race/replay 與完整 restore/fault matrix 仍未完成；本票不宣稱真實金流、資格、費率或沙盒通過。
