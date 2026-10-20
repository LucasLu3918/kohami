# KOHAMI 文件入口

更新：2026-10-17。先依問題找權威文件，再讀詳規；規劃、採用與實測是不同狀態。

## 從哪裡開始

- **要做完整網站**：讀[網站開發與交付計畫](planning/kohami/delivery/IMPLEMENTATION_PLAN.md)；目前目標是本機 Storefront／API／SQL 產品，正式網域與搜尋曝光僅列[未來選配](planning/kohami/options/PUBLIC_DOMAIN_SEO_OPTION.md)，不作本機交付前置條件。Issue 06 基線、Issue 08–23 的本機核准 mock 範圍已完成，下一張未完成且依賴已解除的票是 Issue 24。Issue 23 的正式備份生命週期與會計／稅務分類仍待部署目標、營業主體及專業審查；不屬本機 mock 結案。自動對帳排程僅列[選配](planning/kohami/options/RECONCILIATION_SCHEDULING_OPTION.md)。正式 provider sandbox／實際收款不屬目前本機 mock 範圍。01–05 為已完成的政策／mock 基線輸入。
- **了解產品**：讀[產品目標](planning/kohami/product/PRODUCT_PLAN.md)、[功能規格](planning/kohami/SPEC.md)、[術語](planning/kohami/product/GLOSSARY.md)。
- **確認現在做到哪裡**：讀[技術基線實作狀態](planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)與[歷史驗證證據](architecture/VERIFICATION.md)。目前是 `PARTIAL_NOT_M0_COMPLETE`，不是重新從 Issue 01 開始。
- **查文件分類與清理結果**：讀[整理報告及完整清冊](maintenance/DOCUMENTATION_AUDIT.md)、[搬移／移除對照](maintenance/DOCUMENTATION_MIGRATION.json)。

## 權威與閱讀優先順序

| 問題 | 權威來源 | 適用範圍 |
| --- | --- | --- |
| 如何開發 | [AGENTS.md](../AGENTS.md)、[架構](architecture/ARCHITECTURE.md)、[模組](architecture/MODULES.md)、[ADR](adr/0003-modular-monolith-and-tdd.md) | 模組化單體、DDD／CA／SOLID、TDD |
| 已採商務政策 | [政策入口](proposals/kohami/README.md)、[mock 政策](proposals/kohami/04-demo-commerce-policy.md) | 現階段全流程模擬；其他正式營運條件依各文件適用範圍 |
| 已批准實作哪些功能 | [批准範圍](planning/kohami/delivery/technical-baseline/APPROVED_SCOPE.yaml) | 本機 core34／native4／12 個基線驗收；不是完整 104 或 140 操作批准 |
| API 執行契約 | [執行契約](contracts/runtime/Core.openapi.json) | 已採用的 34 個操作；runtime 仍須授權與狀態驗證 |
| 完整 API 設計 | [契約說明](planning/kohami/contracts/API_SPEC.md)、[整合候選](planning/kohami/contracts/API_CANONICAL.openapi.yaml)、[操作來源圖](planning/kohami/contracts/CANONICAL_OPERATION_MAP.yaml) | 104 核心＋36 換裝操作；不能全數視為已實作或批准 |
| 未來會員餘額與人工履約 | [後續方案](planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md) | 已選方向的規劃提案；不覆蓋目前訪客 mock 契約 |
| 驗收標準 | [唯一驗證計畫](planning/kohami/delivery/VALIDATION_PLAN.md)、[品質基準](quality/QUALITY_PROFILE.yaml) | 定義怎麼驗，不代表通過 |
| 工作與依賴 | [原 38 張票券](issues/kohami/06-commerce-technology-quality-baseline.md)、[會員餘額票](issues/member-wallet/01-policy-and-contract-decisions.md)、[正式啟用票](issues/production-launch/01-go-live-readiness.md)、[票券慣例](agents/issue-tracker.md) | 保留編號、triage、Work status、Comments／Resolution |

同一主題以較新、明確採用且適用當前階段的決議優先；無法判定時列待決，不從檔名或工具 PASS 推定批准。`PLANNING_READINESS.yaml` 是設計來源與規劃檢查快照，產品實測以 `IMPLEMENTATION_STATUS.yaml` 和驗證紀錄為準。

## 目錄用途

| 目錄 | 內容 |
| --- | --- |
| `planning/kohami/product/` | 產品目標、術語、決策來源 |
| `planning/kohami/experience/` | 畫面、視覺、三語與線框 |
| `planning/kohami/commerce/` | 商品、購物車、結帳、訂單、售後、對帳及會員餘額提案 |
| `planning/kohami/identity/`、`security/` | 帳號、授權、訪客訂單能力、key/cert、SQL 權限 |
| `planning/kohami/architecture/` | 邏輯領域／資料與技術設計提案；現行程式架構見 `docs/architecture/` |
| `planning/kohami/fitting/` | 固定模特、素材匯入、權利、PNG、收藏匯出與實證模板 |
| `planning/kohami/contracts/` | OpenAPI 設計來源、整合候選及追溯圖；`docs/contracts/runtime/` 為已採執行子集 |
| `planning/kohami/delivery/` | 開發順序、驗收、操作手冊；`technical-baseline/` 為技術基線範圍／狀態／來源綁定 |
| `planning/kohami/fixtures/`、`options/` | 合成規劃資料；未採用、延後評估的選配 |
| `architecture/`、`adr/` | 現行架構、漸進遷移、驗證紀錄與已採決策 |
| `issues/`、`proposals/`、`quality/`、`agents/`、`maintenance/` | 票券、政策、品質、作業慣例與文件維護 |

## 階段代號

當前唯一執行順序為核心本機 → 會員實付儲值 → 換裝另版 → 正式營運 → 營運後觀測，詳見執行主計畫。

M0＝本機技術與交易基線；M1＝商品與費用確認；M2＝結帳付款與訂單；M3＝履約售後；M4＝本機驗收交付。它們是歷史里程碑與驗收 ID，不是業務模組。檔案改以用途分類，保留 ID 以維持測試、票券及採用紀錄可追溯；新文件請使用功能名稱。

## 本機命令

在專案根目錄執行；啟動環境與私有設定見[本機操作手冊](planning/kohami/delivery/LOCAL_DEMO_RUNBOOK.md)及[遷移計畫](architecture/MIGRATION.md)。本次整理未啟動服務或重設資料庫。

```sh
dotnet build Kohami.slnx -c Release
python3 -m unittest discover -s tests/architecture -v
python3 scripts/m0/run-local.py
```
