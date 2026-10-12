# 票 06：.NET／Vue／SQL Server 技術實證基準 v0.2

日期：2026-10-13｜Framework decision: ACCEPTED｜Work status: claimed｜實證狀態分項記錄。

使用者明確指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，取代 v0.1 未批准的 Next.js／Vendure／PostgreSQL 候選。採用框架家族，前台產生 HTML 的方式、SQL Server 測試環境與正式部署仍待確認。[ADR-0002](../../../docs/adr/0002-dotnet-vue-sqlserver.md)為本輪來源與邊界。

## 適配性與技術映射

有條件適合。C# 可精確表達既有交易規則，ASP.NET Core 的身份與授權能力、EF Core 的 SQL Server provider 及 Vue 三語互動符合既定方向；自建商務核心增加庫存競爭、可信付款、對帳、退款與升級維護責任，必須以獨立案例和真實 SQL 行為驗證，不能以框架名稱保證完成。

| 層級 | 基準與證據 | 本輪範圍／待決 |
| --- | --- | --- |
| Runtime／後台 | ASP.NET Core 搭配目前受支援的 .NET 10 LTS；「.NET Core」方向以現代 .NET 實作，非使用停止支援的舊版。官方[支援政策](https://dotnet.microsoft.com/en-us/platform/support/policy)，本機 SDK 10.0.401 已偵測 | 本機 C# 規則實證；最終 patch 隨安全更新，正式部署／主機未定 |
| 身份／權限 | [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)與[多因素驗證](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0)為實作接縫，四角色細部授權與同人退款禁止仍需自建檢查 | 框架預設值不等於已採政策：會員密碼長度、階段期限、後台 MFA 強制、核准綁定與撤銷皆須測試 |
| 資料存取 | [EF Core SQL Server provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)為合理工程預設，搭配 Microsoft SQL Server；整元金額與快照持久化契約隨旅程制定 | SQL Server 版本／edition／測試主機待環境；交易隔離、去重唯一性、庫存更新、到期重啟與重試需真實資料庫驗證 |
| 前台 | Vue 3。商品／首頁三語 URL、HTML、canonical／hreflang 必須可驗證；官方[SSR](https://vuejs.org/guide/scaling-up/ssr.html)與[效能建議](https://vuejs.org/guide/best-practices/performance.html)支持直接產生內容 HTML | 建議 Vue＋Nuxt SSR（Node 執行環境）；替代 ASP.NET Core 產生 HTML、Vue 增強互動。二者均符合 Vue 指定，尚未替使用者決定 |
| 商務／供應商 | 以單一商務核心與必要背景工作維護 Catalog／Inventory／Checkout／Payment 等邏輯所有權；不因領域命名建立微服務 | 自建 .NET 商務核心；金流、物流、發票仍須票 04／05 官方契約與沙盒，無已驗證現成支援結論 |

## 已觀測環境與影響

- 本機為 ARM64 Mac，已有 SDK 10.0.401、Node 與 Docker CLI；Docker daemon 目前不可連線。
- Microsoft 指出 SQL Server 容器只支援 Linux Intel／AMD x86-64，Rosetta／QEMU 等模擬環境未測試且不支援：[官方容器要求](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver17)。因此 ARM Mac 的模擬容器不能證明受支援環境的 SQL 行為；不以其他資料庫代替 SQL Server 的驗收。
- SQL 憑證不進入 Git／聊天／日誌。真實獨立測試環境確認後才設定本機秘密與執行測試；不自動啟動 Docker、採購主機、建立付費服務、執行正式資料遷移或收款。
- SQL Server edition 與正式授權成本、雲端／Windows／Linux主機、備份、流量、郵件、開發與維護人力均未確認；不保證 NT$8,000／月，也不轉撥 AI 預算。

## 有界實證與品質矩陣

先在 [commerce-baseline](../../../experiments/commerce-baseline/README.md) 做 C# 合成規則實證；前台與資料庫的待決事項不阻止純規則編譯和測試。

| 維度 | 驗證與完成界線 |
| --- | --- |
| 效能 | 保留 N01 LCP／INP／CLS、N02 就緒切換目標；頁面與真實裝置／樣本未建立前 UNVERIFIED。小型規則測試不是負載或 SQL benchmark |
| 安全 | 四角色／MFA／資源所有權／雙人退款須正反向與繞過測試；目前沒有產品身份驗證證據，不把框架功能當 PASS |
| 易用性 | 三語切換、鍵盤與錯誤提示須瀏覽器完整旅程；Vue HTML 策略確認前暫不實作依賴畫面 |
| 可靠性 | 純規則驗證重送、15 分鐘期限、取消／未知／晚到成功；SQL 鎖定、重啟持久化、復原與真供應商事件另驗；不宣稱月可用性 |
| 維護性 | 小型獨立實驗、單一公開行為接縫、鎖定 SDK、測試與限制可重現；產品模組和資料契約隨後續完整旅程建立 |
| 資源／成本 | 本輪僅本機合成實驗；開發與維護成本仍存在。依實證改造量、主機與授權取得報價後估算，不虛構費率 |
| 交付時間 | 先記錄本輪編譯／測試證據；SQL 環境、Vue 方式、外部供應商與人力未定前，不承諾完整交期 |

付款／庫存／退款實驗以已採政策的獨立算例及狀態軌跡斷言；記憶體實驗只證明該實驗規則，不證明 SQL 原子性、持久化、支付可信性、MFA 或復原。票 06 必須取得完整必要實證與部署／成本邊界，才可 resolved；票 08 尚未解鎖。

## 尚需解除的阻擋

1. Vue 前台採 Nuxt SSR，或 ASP.NET Core HTML＋Vue 互動元件。
2. 實際可用的獨立 SQL Server 測試環境；尚無者先準備純規則與 SQL 驗證規格。
3. 真實 SQL／身份／三語／復原實證、部署與成本邊界，依得到的環境與候選證據逐項完成，不反覆詢問已指定的框架家族。
