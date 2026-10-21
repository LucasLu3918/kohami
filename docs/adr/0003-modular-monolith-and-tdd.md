# ADR-0003：模組化單體、Clean Architecture 與 TDD

日期：2026-10-14｜狀態：ACCEPTED，實作採漸進遷移

## 決策與授權

使用者批准在 AGENTS 補充 DDD／CA／SOLID、TDD、GPT-6 以上開發模型，並執行五步驟重整；後續明確將文件根目錄修正為 `docs/`。

採業務模組優先的模組化單體；複雜商務規則採 DDD，Domain 與 HTTP／SQL 分離，依 SOLID 保持精簡責任與依賴。程式行為開發預設 TDD，保留必要的真 SQL、安全与瀏覽器證據。

## 相容性與取代

延續 ADR-0001 的固定非 AI 模特／免本人照片方向与 ADR-0002 的 .NET／Nuxt／SQL Server。取代 agents/domain 中禁止多模組目錄的 single-context 布局与正式票券放 `.scratch` 的舊位置；統一術語表繼續有效。

不拆微服務，不改資料庫交易政策、native 授權或既定 Foundation 實作範圍。純規則先抽離，既有 mixed adapter 列為遷移債務，避免一次重寫造成交易与安全回歸。

## 代價與驗證

程式路徑、引用、文件與來源 hash 必須一起遷移；Domain project 邊界由測試與建置檢查。未來新功能遵循新架構，既有模組按受保護的旅程逐步遷移。

詳見 [架構](../architecture/ARCHITECTURE.md)、[責任](../architecture/MODULES.md)、[遷移](../architecture/MIGRATION.md)。
