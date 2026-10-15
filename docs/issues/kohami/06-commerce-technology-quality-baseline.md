# 06: 確認商店技術方案與品質基準

**What to build:** 以最小實證確認前台、商務核心、儲存與部署方案，訂定品質檢查、資源與成本估算，讓後續旅程可在明確技術邊界內交付。

**Blocked by:** 01：確認結帳與庫存交易規則；03：確認身份、權限與資料政策

**Status:** ready-for-agent

**Framework decision:** accepted（ASP.NET Core／.NET、Vue.js、Microsoft SQL Server）

**Work status:** claimed（有界實證；尚未完成）

- [x] 固定已採 ASP.NET Core／Nuxt SSR／SQL Server 與現有程式／鎖檔，恢復可重跑基準；不重新比較未採用的框架候選。
- [x] 核對已採框架／模組化單體、core34 批准範圍及現有 schema/API；列出當前 candidate 與缺口，不從邏輯領域推定微服務或擴大部署。
- [x] 定義瀏覽器旅程、外部供應商沙盒、公開交易並行、復原與人工素材審核的檢查方式，包含驗證命令或待完成條件。
- [x] 建立效能、可及性、可靠性、安全與復原的量測矩陣；區分規劃目標、待確認門檻與已測證據。
- [x] 提出開發、服務、維護及負載情境成本／人力估算與信心限制，不把 NT$8,000／月視為已證實總預算，不轉撥停用的 AI 預算。

前四項依現行批准範圍、canonical／schema來源、[QUALITY_PROFILE](../../quality/QUALITY_PROFILE.yaml)、[基線操作手冊](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)及本候選重跑證據完成。第五項依使用者選擇，以 S0–S4 本機核心商店展示版估算；不納入正式部署或外部供應商費用。

### 本機展示版規劃估算（低信心；2026-10-15 範圍變更前估算，待重估）

假設：依主計畫 S0–S4 單線交付、沿用現有程式與 .NET／Nuxt／SQL Server，金流／物流／憑證維持 mock；一位全職工程人力，含實作、整合、缺陷修正與驗收準備，不含正式營運、採購、真實服務商資格／沙盒及外部部署。

| 階段 | 人日規劃區間 | 主要工作 |
| --- | ---: | --- |
| S0 基線與結案 | 5–10 | 本機 API／browser 售後、剩餘 worker 故障窗口及 clean handoff；Owner TOTP、原生 OS 確認與獨立 restore／cutoff 已移出整體驗收 |
| S1 商品與購物車 | 10–18 | 08–11 商品、SKU、庫存、搜尋及費用旅程 |
| S2 訂單與付款 | 15–30 | 12–17 結帳、可信 mock 付款、逾時／補償、對帳、會員與三語 |
| S3 配送與售後 | 18–36 | 18–23 配送、退貨、受控退款、DEMO 憑證、客服及資料生命週期 |
| S4 本機交付 | 12–24 | 24–26 手機／可及性／效能、安全／復原、乾淨重建與交接 |
| 本機展示版合計（四捨五入） | 60–120（低信心） | S0–S4 規劃區間；不是日曆承諾或核定預算 |

原三點規劃（樂觀 65–75、期望 85–110、風險調整 125–155 人日）依較廣驗收範圍估算，已失效。依目前本機展示版範圍將 S0 調整為 5–10 人日，S1–S4 維持原區間；合計 60–118 人日，四捨五入為 60–120 人日。信心仍低，因 API／browser 售後與 clean handoff 尚未驗收，依賴可用性與實際工時未量測。這不是核定工期或預算；人工費率尚待提供，不填人力金額。

- **服務成本：** 本機按需啟停且無新增雲端／供應商採購，新增外部服務支出規劃為 NT$0/月；不含既有電腦、電力、網路與工具成本，因此不是總 TCO。
- **維護人力：** 無 24×7／公開 SLA。試行前三個月暫抓每月 4–8 工時供依賴更新、fixture／憑證檢查、啟停與還原演練；屬低信心預留，須以實際維護紀錄修正。
- **負載／資源：** 依品質 Profile 以 5 個 client、至少 500 次主要讀取請求，另以 20 個並行結帳競爭案例驗收；Nuxt／API／SQL 合計記錄 RSS、CPU、啟動時間與資料量，8 GiB 是待驗證上限提案。未量測前不推算主機容量或電力費。
- **人事金額：** 以實際核定的含負擔日費率乘上重新估算後的人日，再加核准的主機／電力／工具成本；日費率尚未提供，故不填 NT$ 總人力成本，也不將 NT$8,000/月當成預算。

外部正式供應商 sandbox 不在目前 mock 階段驗收範圍，保留為正式服務啟用前置，不以本估算代替其資格、費率或營運成本。

## 現行 mock 階段範圍（2026-10-13）

使用mock外部adapter驗證，但產品SQL交易／併發／持久化／重啟、權限與Nuxt SSR仍須實證；既有開發環境不等於本票完成。

詳細以 [全流程mock政策](../../proposals/kohami/04-demo-commerce-policy.md) 的 DEMO-01–12 為準；本節優先於以下完整產品／歷史來源中的真實服務或雙人退款要求。既有阻擋編號保留，按本階段契約是否完成判斷。所有新增產品案例仍 NOT_EXECUTED。

## 來源與範圍

- 階段：前置決策與實證。
- 驗收對應：V06、V11；N01、N03–N04；User Stories 6–7、82–85、87。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。


- 2026-10-13：01–03 政策已由使用者「01／02／03 採用」確認；前置政策工作 resolved。本票已完成[官方文件比較與本機實證提案](../../proposals/kohami/06-commerce-technology.md)，候選、實測與最終架構未批准，Work status 尚未 resolved，不解除票 08。

- 2026-10-13：使用者指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，以 [ADR-0002](../../adr/0002-dotnet-vue-sqlserver.md) 記錄採用，取代先前未採用候選。本機 .NET 規則實證進行中；Vue HTML 方式與獨立 SQL Server 環境待確認，真實 SQL／身份／三語／復原未通過，不解除票 08。

- 2026-10-13：隔離實驗已整合：純 C# 規則 17/17 Release tests，Release build 0 warnings／0 errors；[證據與限制](../../../experiments/commerce-baseline/README.md)。僅單一合成 SKU／依序記憶體行為，SQL／身份／Vue／供應商／復原未驗證，Work status 仍 claimed，不解鎖票 08。

- 2026-10-13：使用者確認「Vue 採 Nuxt SSR」，並請求本地 Docker 或獨立 SQL Server 測試建置。已採 Nuxt SSR，執行[隔離本地 SQL 環境](../../../infra/sql-server/README.md)建置；ARM／Rosetta 與官方支援平台證據分開，環境 smoke 不關閉本票。

- 2026-10-13：[本地 SQL 開發環境](../../../infra/sql-server/README.md)實測 healthy，版本16.0.4295.3，受限登入／rollback／重啟持久化／host TCP 通過；只限 ARM／Rosetta 開發環境。產品並行／.NET driver／權限／三語／災難復原仍未驗，本票持續 claimed。

- 政策更新紀錄（2026-10-13）：使用者確認金流、退款、物流、發票先mock，運費／人員先規劃、管理者退款；更新目前範圍但不改產品完成狀態。

## 本機完整展示工程規劃（2026-10-13）

使用者確認先本機完整展示；新增[工程審閱包](../../planning/kohami/PLANNING_INDEX.md)，含畫面、資料／API、身份／程序、七面向品質與M0–M4交付提案。框架維持已採用，細節／門檻為可審閱提案；票06維持claimed，真SQL／MFA／三語／復原與成本實證未因文件完成而通過。

- 2026-10-15：本票為目前續做起點；先恢復共同環境／基線，再完成 04／05 所需情境證據，回本票依自身清單結案。此次只更新規劃與過時敘述，未改 claimed。

- 2026-10-15：依結案條件盤點，框架／core34／schema/API 缺口與品質量測方法已有可追溯文件；Commerce dedicated SQL baseline 52 checks、API Release build、Nuxt typecheck 通過。Identity／Native 目前候選測試未重跑，Issue 02 真 API aftercare 在憑證提示前被 runner 阻擋；供應商／多 worker fault matrix 與獨立 restore／cutoff 對帳仍未執行。人力／服務／維護成本缺少可用的候選實測與正式費率輸入，暫不填猜測值。詳見[逐項狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)及[驗證紀錄](../../architecture/VERIFICATION.md)。本票 work status 維持 claimed，未結案。
- 2026-10-15：續做本機環境與共同驗收；目前工具為 .NET 10.0.401／Node v26.11.1／npm 11.20.0。Issue 06 dedicated SQL fixture 的 Native strict-TLS／最小權限 guard 通過，Commerce harness 本機重跑 51 個 PASS assertions，API Release build、Nuxt typecheck、3 項 architecture tests 通過。Identity／Native 測試專案 restore 停在 project resolution，仍 NOT_EXECUTED；Docker API 受限 shell 權限不足。沒有執行 Owner／OS presence、browser aftercare、provider fault matrix、獨立 restore／cutoff 對帳或成本估算；本票仍 claimed。詳見[逐項狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)及[驗證紀錄](../../architecture/VERIFICATION.md)。
- 2026-10-15：在當前候選 `588dc7917389dfd9e5c28658d11d8c6cff8a3f2a` 重跑本機基線。Commerce／API Release build（各 0 警告／錯誤）、Nuxt typecheck、dedicated Issue06 SQL Commerce harness（67 項）、Identity dedicated SQL（30/30）、Native 合成檢查（47 項）、architecture（3/3）與 `tests/m0` runner regression（7/7）通過。第一輪 runner regression 受 sandbox Unix socket 權限阻擋，核准本機重跑通過。這些不覆蓋完整 browser/API 售後、雙 worker fault matrix、獨立 restore／cutoff 對帳、OS trust/user-presence；費用／人力估算待範圍與費率輸入。故 Issue 06 維持 claimed、尚未結案；逐項狀態見[IMPLEMENTATION_STATUS](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)，完整命令及邊界見[驗證紀錄](../../architecture/VERIFICATION.md)。
- 2026-10-15：使用者選擇先估本機展示版並將人工費率留待提供；新增 S0–S4 自下而上 65–125 人日區間與低信心三點估算、按需本機服務支出、維護人力及待驗證負載量測假設。此為規劃輸入，不是工期／預算承諾。產品驗收缺口仍在，Issue 06 work status 維持 claimed。
- 2026-10-15：排除 Identity／Native restore 阻塞並重跑目前候選。受限代理對官方 NuGet 回覆 403 allowlist；核准網路 restore 後 Identity PASS 30/30。Native 另修正過期 `packages.lock.json`（補目前專案參考），Release build PASS 且 47 項合成檢查 PASS。這些不涵蓋 Secure Enclave／OS user-presence 或 Owner 認證實測，也不解除其他 Issue 06 結案條件；本票仍 claimed。詳見[逐項狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)及[驗證紀錄](../../architecture/VERIFICATION.md)。
- 2026-10-15：依使用者選擇完成本機展示版 S0–S4 規劃估算（65–125 人日，低信心；人工費率待定），並以 dedicated synthetic SQL fixture 驗證兩個獨立 worker 程序同時送達相同付款事件只產生一筆已套用 inbox、一筆 primary collection、一份 DEMO receipt，訂單確認且 reservation 消耗一次。Commerce Release build 0 warnings／0 errors；SQL harness 69 assertions PASS。TDD 先確認缺少 race helper 時建置失敗，再補 harness 並通過。仍缺 API／browser 售後退款閉環、worker crash/restart／失敗 replay matrix、獨立 restore／cutoff 對帳及 live OS trust/user-presence；正式 provider sandbox 與外部部署在目前批准範圍外。故本票維持 claimed，尚未結案。詳見[估算與逐項狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)、[驗證紀錄](../../architecture/VERIFICATION.md)。
- 2026-10-15：範圍更新（使用者決定）：Owner TOTP／驗證器與原生 OS user-presence、瀏覽器信任實證，以及獨立資料庫 restore／cutoff 對帳由 Issue 06／M0／M4 整體必驗清單移出，改列選配改善。這些證據仍未執行，不得標 PASS；必備缺口仍包含 API／browser 售後、worker crash/restart／失敗 replay matrix 與 clean handoff。先前 65–125 人日估算依較廣驗收範圍產生，已標記待重估；人工費率仍待定。逐項分類見[實作狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)、[選配改善計畫](../../planning/kohami/options/ISOLATED_RESTORE_CUTOFF_OPTION.md)及[驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)。Issue 06 work status 仍 claimed。
- 2026-10-15：更新 S0–S4 本機展示版低信心估算：S0 5–10、合計 60–120 人日（未四捨五入合計 60–118）；Owner TOTP、原生 OS 確認、獨立 restore／cutoff 不在整體驗收估算。人工費率待定，未形成金額或日期承諾。
- 2026-10-15：新增 dedicated SQL worker recovery 證據；兩個獨立程序競爭已過期 Started payment job 時只有一個成功重取，fence 從 7 遞增至 8，Inbox、Primary collection、DEMO receipt 各一筆，舊 fence 無法完成工作。API/browser 售後與 clean handoff 仍未通過，故本票維持 claimed。命令及限制見[驗證紀錄](../../../architecture/VERIFICATION.md)、[操作手冊](../../planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)與[逐項狀態](../../planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)。
- 2026-10-15：API/browser 售後仍未執行。現有 Issue02 runner 僅接受 Issue02 disposable fixture，並要求 authentic TOTP；目前 `.local/m0` 是 Issue06 fixture、沒有 Issue02 project marker，且該 runner 會移除自己的 fixture。依使用者範圍決定未開 Owner/TOTP 驗收窗口，也未替換或重建現有 fixture；需先決定採用不驗登入 MFA 的合成預先授權 session API/SQL 驗收，或維持此旅程未執行。
