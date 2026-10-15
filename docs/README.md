# KOHAMI 文件入口

更新：2026-10-14。文件集中於 `docs/`；私有資料不屬於文件包。

## 現行權威與狀態

| 內容 | 現行來源 | 解讀 |
| --- | --- | --- |
| 開發規則 | [AGENTS.md](../AGENTS.md) | DDD／CA／SOLID、TDD、GPT-6 以上模型要求 |
| 重整架構 | [架構](architecture/ARCHITECTURE.md)、[模組](architecture/MODULES.md)、[ADR-0003](adr/0003-modular-monolith-and-tdd.md) | 本次使用者批准的模組化單體方向 |
| 產品與術語 | [產品](planning/kohami/PRODUCT_PLAN.md)、[術語](planning/kohami/GLOSSARY.md)、[規格](planning/kohami/SPEC.md) | 依現行 mock 更新解讀歷史正式營運內容 |
| 已採政策 | [政策入口](proposals/kohami/README.md)、[mock 政策](proposals/kohami/04-demo-commerce-policy.md) | 政策採用與產品驗證分開 |
| 已批准 M0 範圍 | [範圍](planning/kohami/M0_SCOPE_CONFIRMATION.yaml) | core34／native7／12AC；非整體 140 操作實作批准 |
| 產品執行狀態 | [M0 狀態](planning/kohami/M0_IMPLEMENTATION_STATUS.yaml) | PARTIAL_NOT_M0_COMPLETE；歷史測試結果不等於本次通過 |
| 未來選配 | [本機管理安全擴充評估](planning/kohami/OPTIONAL_LOCAL_ADMIN_SECURITY_PLAN.md) | Owner 初始化／復原與原生管理安全列為 DEFERRED_OPTION；未決定是否擴充、不屬目前 Issue／M0 驗收 |
| 後續儲值與履約規劃 | [會員儲值、訂單履約與退貨退款](planning/kohami/WALLET_SETTLEMENT_SHIPPING_PLAN.md) | 使用者已選閉環方向；規劃提案，不改變現行 M0 全 mock／驗收狀態 |
| API 執行契約 | [M0 core34](m0/Core.openapi.json) | 採用子集；來源候選仍保留 Phase2 提案 |
| 規劃候選 | [完整索引](planning/kohami/PLANNING_INDEX.md)、[候選](planning/kohami/API_CANONICAL.openapi.yaml) | 104 core／36 Phase2；整體候選與補充案非全部 runtime authority |
| 票券 | [issues](issues/kohami/01-checkout-inventory-rules.md) | 保留編號、依賴、triage、Work status、Comments、Resolution |
| 重整進度與驗證 | [遷移計畫](architecture/MIGRATION.md)、[本次驗證](architecture/VERIFICATION.md) | 搬移、重構及產品旅程證據各自標示 |

較早文件中的「未建立程式／Nuxt app」、「MFA 待決」、「canonical 未建立」等文字屬歷史。MFA B 與 M0 授權以現行範圍及產品狀態為準；歷史來源保留以利追溯，不把過去 NOT_EXECUTED 自動改為 PASS。

## 分類

- `planning/`：需求、UX、里程碑、專題詳規與規劃 fixture；`planning/<feature>/options/` 保存尚未採用、待未來評估的選配。
- `architecture/`：模組責任、依賴、交易邊界、搬移對照與驗證。
- `adr/`：已採架構決策；`agents/`：Agent 作業慣例。
- `issues/`、`proposals/`：票券與政策；`quality/`：品質基準。
- `m0/`：core34 可執行契約；完整來源候選維持 planning 路徑以保留來源關係。
- 模組／infra 的 README 說明所在程式或工具用法，產品規劃只維護於本文件包。

## 本機命令

命令須在專案根目錄執行。環境與授權前置見 [遷移計畫](architecture/MIGRATION.md)。

```sh
dotnet build Kohami.slnx -c Release
python3 -m unittest discover -s tests/architecture -v
python3 scripts/m0/run-local.py
```
