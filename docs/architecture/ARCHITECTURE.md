# 模組化單體與 Clean Architecture

日期：2026-10-14。使用者批准資料夾重整、DDD／CA／SOLID／TDD；文件根目錄指定為 `docs/`。本次不改產品範圍、SQL schema、外部供應商或正式部署。

## 適配性與責任

KOHAMI 的金額分攤、庫存保留、付款競爭、退款額度與授權生命週期適合 DDD。初期以模組化單體執行，沿用 ASP.NET Core／Nuxt SSR／SQL Server。業務模組是程式所有权，限界上下文待術語、模型與契約證據確認，不因資料夾存在宣稱已完成策略 DDD。

SOLID 用於責任與依賴設計；簡單 CRUD 不強制 rich model、Repository 或額外 service。介面放在需要該能力的內層，具體儲存與外部 adapter 由 Infrastructure 實作。

```text
apps/api                  HTTP、CSRF、wire 契約、DI
apps/worker               背景程序 DI，無 HTTP listener
tools/native-operations   原生初始化／復原／migration
apps/storefront           Nuxt SSR，同 origin 代理與功能 UI
src/Modules/*/Domain      純規則，不依賴 HTTP／SQL／Runtime
src/Modules/*/Application 使用案例與 ports
src/Modules/*/Infrastructure SQL、mock adapters、既有 Foundation 遷移接縫
src/Modules/*/Presentation HTTP 映射與 transport adapter
src/BuildingBlocks/Runtime 既有 Foundation 技術支援，非共用 Domain
```

## 依賴方向

- Domain 僅依賴 BCL；Application 指向 Domain，Infrastructure 實作 Application ports。
- Presentation 呼叫使用案例；host 可參考 Infrastructure 以完成 composition root。
- 各模組不參考其他模組的 Infrastructure。跨模組交換穩定識別、DTO 或事件，host 提供 adapter。
- Runtime 目前含 SQL、Data Protection、HttpContext 與契約驗證，不能從 Domain 引用。後續按技術責任拆分，而非把所有規則放入 Core。
- 既有 CommerceService／SecurityService 尚同時處理 HTTP 與 SQL，保留在 Infrastructure/Legacy，屬明列的 CA 遷移債務；不能把搬移當成完整 CA 合規。

## 交易与信任邊界

Commerce 先保留 Cart／Quote／Checkout／Order／Inventory／Payment／Refund 的內部子模組。建立訂單、全 SKU 保留、快照、冪等及 outbox 必須原子提交；庫存不足全部 rollback。付款到期與晚到成功、退款來源／單位／運費額度继续按同一業務交易裁決，不為解耦改成 eventual consistency。

Security 授權鎖、maintenance gate 與 commerce lock 取得順序保留。資源 resolver 由 API host 對接，不讓 Commerce 直接依賴 Identity Infrastructure。worker 使用專用 DB credential；native 原生確認與 permit 不能由 HTTP 取代。

不更動 SQL schema／commerce 表所有權、Data Protection application name／purpose、cookie 名稱、API route、dataset、mode、冪等與版本演算法。Nuxt 顯示結果，後端裁決價格、庫存與权限。

## TDD 與驗證

新行為按 red → green → refactor；重構先以已有規則與 SQL 回歸測試保护。純文件与搬移採結構／連結／雜湊／建置核對。核心交易須 dedicated SQL fixture；瀏覽器驗證另列，不從 domain PASS 推論完整交易驗收。

具體順序與限制見 [遷移](MIGRATION.md)，各模組責任見 [MODULES](MODULES.md)。
