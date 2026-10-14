# ADR-0002：採 ASP.NET Core／.NET、Vue.js 與 Microsoft SQL Server

日期：2026-10-13｜狀態：框架家族與 Nuxt SSR ACCEPTED；本地 SQL 開發環境已建置；正式支援平台／部署待驗。

## 來源與決策

使用者明確指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，取代尚未採用的 Next.js／Vendure／PostgreSQL 提案。後端採 ASP.NET Core／現代 .NET，前台採 Vue，權威交易資料採 Microsoft SQL Server。以 .NET 10 LTS 與 Vue 3 作受支援版本基準；SQL Server 版本與 edition 依實際環境與授權確認。

實作自建 .NET 商務核心，沿用既有邏輯領域所有權，先採可維護的單一核心與必要背景工作。EF Core SQL Server provider 為工程預設，真實資料庫的庫存保留／去重／事件競爭與復原須另測試。

## 理由與取捨

符合使用者指定技術與已採用交易／身份政策。相比原候選商務平台，自建會增加狀態機、可信金流、對帳、退款、安全與版本維護責任；需實證與成本證據，不將採用框架當作功能完成。

## 未決與實證邊界

使用者明確回覆「Vue 採 Nuxt SSR」，採 Vue 3＋Nuxt 伺服器端渲染，Node SSR 與 ASP.NET Core 後端分別執行。使用者亦請求本地 Docker SQL Server 測試環境：先建立隔離 SQL Server 2022 Developer instance，僅 loopback 存取、專用 volume／DB／受限登入；實際結果見[本地環境文件](../../infra/sql-server/README.md)。ARM64／Rosetta 不是官方支援平台，開發實測與後續原生 x64 平台驗證分開；不使用正式資料、外部供應商、付費主機或收款。

框架採用不批准正式部署、付費或業務供應商；具名退款人員、法定資料分類與正式服務資格仍沿用政策包限制。

## 與既有決策相容性

不改變 [ADR-0001](0001-preset-avatar-fitting.md)：不使用 AI／本人照片，固定三視圖與素材授權／審核、購買與展示邊界繼續適用。01–03 已採政策繼續適用。

詳見[票 06 技術基準](../proposals/kohami/06-commerce-technology.md)；實驗不證明產品 SQL／MFA／付款／復原通過。
