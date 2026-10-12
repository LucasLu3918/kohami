# ADR-0002：採 ASP.NET Core／.NET、Vue.js 與 Microsoft SQL Server

日期：2026-10-13｜狀態：框架家族 ACCEPTED；前台 HTML 方式、資料庫實證環境及部署條件待決。

## 來源與決策

使用者明確指定「ASP.NET Core, .NET Core, Vue.js, Microsoft SQL Server」，取代尚未採用的 Next.js／Vendure／PostgreSQL 提案。後端採 ASP.NET Core／現代 .NET，前台採 Vue，權威交易資料採 Microsoft SQL Server。以 .NET 10 LTS 與 Vue 3 作受支援版本基準；SQL Server 版本與 edition 依實際環境與授權確認。

實作自建 .NET 商務核心，沿用既有邏輯領域所有權，先採可維護的單一核心與必要背景工作。EF Core SQL Server provider 為工程預設，真實資料庫的庫存保留／去重／事件競爭與復原須另測試。

## 理由與取捨

符合使用者指定技術與已採用交易／身份政策。相比原候選商務平台，自建會增加狀態機、可信金流、對帳、退款、安全與版本維護責任；需實證與成本證據，不將採用框架當作功能完成。

## 未決與實證邊界

Vue 可採 Nuxt SSR 或 ASP.NET Core 產生 HTML＋Vue 互動，會影響部署執行環境，尚待確認。ARM64 Mac 的 SQL Server 模擬容器不是官方支援環境；真實驗證須獨立受支援 SQL Server。當前先做合成 C# 規則實驗，無正式資料、外部供應商呼叫、採購、主機部署或收款。

框架採用不批准正式部署、付費或業務供應商；具名退款人員、法定資料分類與正式服務資格仍沿用政策包限制。

## 與既有決策相容性

不改變 [ADR-0001](0001-preset-avatar-fitting.md)：不使用 AI／本人照片，固定三視圖與素材授權／審核、購買與展示邊界繼續適用。01–03 已採政策繼續適用。

詳見[票 06 技術基準](../../.scratch/kohami/proposals/06-commerce-technology.md)；實驗不證明產品 SQL／MFA／付款／復原通過。
