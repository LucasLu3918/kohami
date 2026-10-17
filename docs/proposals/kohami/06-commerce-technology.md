# 票 06：.NET／Vue／SQL Server 技術實證基準 v0.2

日期：2026-10-13｜Framework decision: ACCEPTED｜Work status: claimed｜實證狀態分項記錄。

使用者明確指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，取代 v0.1 未批准的 Next.js／Vendure／PostgreSQL 候選。採用框架家族，本輪使用者再確認 Vue 採 Nuxt SSR，並要求協助本地 Docker SQL Server 測試環境；正式部署與官方支援平台實證仍待完成。[ADR-0002](../../adr/0002-dotnet-vue-sqlserver.md)為本輪來源與邊界。

## 適配性與技術映射

有條件適合。C# 可精確表達既有交易規則，ASP.NET Core 的身份與授權能力、EF Core 的 SQL Server provider 及 Vue 三語互動符合既定方向；自建商務核心增加庫存競爭、可信付款、對帳、退款與升級維護責任，必須以獨立案例和真實 SQL 行為驗證，不能以框架名稱保證完成。

| 層級 | 基準與證據 | 本輪範圍／待決 |
| --- | --- | --- |
| Runtime／後台 | ASP.NET Core 搭配目前受支援的 .NET 10 LTS；「.NET Core」方向以現代 .NET 實作，非使用停止支援的舊版。官方[支援政策](https://dotnet.microsoft.com/en-us/platform/support/policy)，本機 SDK 10.0.401 已偵測 | 本機 C# 規則實證；最終 patch 隨安全更新，正式部署／主機未定 |
| 資料存取 | [EF Core SQL Server provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)為合理工程預設，搭配 Microsoft SQL Server；整元金額與快照持久化契約隨旅程制定 | SQL Server 版本／edition／測試主機待環境；交易隔離、去重唯一性、庫存更新、到期重啟與重試需真實資料庫驗證 |
| 前台 | Vue 3。商品／首頁三語 URL、HTML、canonical／hreflang 必須可驗證；官方[SSR](https://vuejs.org/guide/scaling-up/ssr.html)與[效能建議](https://vuejs.org/guide/best-practices/performance.html)支持直接產生內容 HTML | 已採 Vue 3＋Nuxt SSR（Node 執行環境）；不再保留 HTML 方式為待決 |
| 商務／供應商 | 以單一商務核心與必要背景工作維護 Catalog／Inventory／Checkout／Payment 等邏輯所有權；不因領域命名建立微服務 | 自建 .NET 商務核心；金流、物流、發票仍須票 04／05 官方契約與沙盒，無已驗證現成支援結論 |

## 已觀測環境與影響

- 本機為 ARM64 Mac，已有 SDK 10.0.401、Node 與 Docker CLI；本輪 OrbStack／Docker engine 已確認可用，專用 SQL Server 2022 Developer instance 已健康啟動；host TCP、受限登入、rollback、容器重啟持久化通過。
- Microsoft 指出 SQL Server 容器只支援 Linux Intel／AMD x86-64，Rosetta／QEMU 等模擬環境未測試且不支援：[官方容器要求](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver17)。因此 ARM Mac 的模擬容器不能證明受支援環境的 SQL 行為；不以其他資料庫代替 SQL Server 的驗收。
- SQL 憑證不進入 Git／聊天／日誌。本輪已授權本地 Docker 開發建置，私有設定排除於 Git；容器只有合成 KohamiTest 資料。未授權付費主機、正式資料遷移或收款。
- SQL Server edition 與正式授權成本、雲端／Windows／Linux主機、備份、流量、郵件、開發與維護人力均未確認；不保證 NT$8,000／月，也不轉撥 AI 預算。

## 有界實證與品質矩陣

先在 [commerce-baseline](../../../experiments/commerce-baseline/README.md) 做 C# 合成規則實證；前台與資料庫的待決事項不阻止純規則編譯和測試。

| 維度 | 驗證與完成界線 |
| --- | --- |
| 效能 | 保留 N01 LCP／INP／CLS、N02 就緒切換目標；頁面與真實裝置／樣本未建立前 UNVERIFIED。小型規則測試不是負載或 SQL benchmark |
| 易用性 | 三語切換、鍵盤與錯誤提示須瀏覽器完整旅程；Vue HTML 策略確認前暫不實作依賴畫面 |
| 可靠性 | 純規則驗證重送、15 分鐘期限、取消／未知／晚到成功；SQL 鎖定、重啟持久化、復原與真供應商事件另驗；不宣稱月可用性 |
| 維護性 | 小型獨立實驗、單一公開行為接縫、鎖定 SDK、測試與限制可重現；產品模組和資料契約隨後續完整旅程建立 |
| 資源／成本 | 本輪僅本機合成實驗；開發與維護成本仍存在。依實證改造量、主機與授權取得報價後估算，不虛構費率 |
| 交付時間 | 先記錄本輪編譯／測試證據；SQL 環境、Vue 方式、外部供應商與人力未定前，不承諾完整交期 |


## 尚需解除的阻擋

1. Vue Nuxt SSR 已確認；接著須驗證實際三語 HTML 與瀏覽器旅程。
2. [本地 SQL 開發環境](../../../infra/sql-server/README.md)建置與 smoke 驗證；官方支援平台的原生 x64 環境另驗。
3. 真實 SQL／身份／三語／復原實證、部署與成本邊界，依得到的環境與候選證據逐項完成，不反覆詢問已指定的框架家族。

## 本輪純規則實證結果

已完成三輪真實 TDD：5 failed→5 passed；4 failed／5 passed→9 passed；8 failed／9 passed→17 passed。重構後與 Release 均 17 passed／0 failed／0 skipped，Release build 0 warnings／0 errors。程式與測試已在整合分支，命令、獨立算例及未驗範圍見[實驗 README](../../../experiments/commerce-baseline/README.md)。


## 2026-10-13 接續確認


## 本地 SQL 開發環境實測

本輪已完成隔離環境：SQL `16.0.4295.3`，loopback `127.0.0.1:14333`，KohamiTest／kohami_test（非 sysadmin），專用 volume。登入、合成交易 rollback、停止／重啟後 marker 保留與 host TCP 通過；秘密已 Git 忽略且 0600。這是 ARM／Rosetta 開發環境實測，不替代 .NET driver 與產品庫存／付款／權限／復原、官方支援平台測試。可重現命令見[環境文件](../../../infra/sql-server/README.md)。
