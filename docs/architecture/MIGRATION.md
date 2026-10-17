# 五步驟重整與搬移計畫

本次授權：補充 AGENTS、整理文件狀態、模組責任與骨架、搬移／重構、驗證購物旅程。最終文件根目錄為 `docs/`。

## 1. 文件權威與狀態

使用 [docs 入口](../README.md)與 M0_IMPLEMENTATION_STATUS 作現行入口，保留已採用決議、歷史政策、原票編號与未執行案例。規劃中的 hash 是來源完整性證據；移動更新 path 与來源 hash 不產生新的產品通過結果。

## 2. 模組責任

見 [MODULES](MODULES.md)。Commerce 保留跨庫存／訂單／退款的同 SQL 原子邊界，其他模組經公開契約互動。

## 3. 骨架與搬移對照

逐檔對照見 [PATH_MIGRATION.json](PATH_MIGRATION.json)。Catalog、Fulfillment、CustomerCare、Fitting 只建立含責任說明的 README；Git 不追蹤空資料夾，按實作需要再建立 Domain／Application／Infrastructure／Presentation／Contracts。

## 4. 分批搬移與重構

- `apps/m0/Api` → `apps/api`，新增獨立 `apps/worker` 背景程序入口；API 保留 `--worker-only` 相容模式。
- `apps/m0/Core` → `src/BuildingBlocks/Runtime`。沿用 `Kohami.M0.Core` namespace／assembly 名稱，避免與舊測試／native 整合同步擴大變更。
- Commerce、Security → `src/Modules/Commerce`、`src/Modules/IdentityAccess`；HTTP 映射放 Presentation，既有混合 service 放 Infrastructure/Legacy，SQL 放 Infrastructure/Persistence。
- Native → `tools/native-operations`；測試 → `tests/Commerce`、`tests/Identity`、`tests/Core`、`tests/Native`。scripts 与 infra 延用原用途。
- `.scratch/kohami` 中正式規格／票券／提案 → `docs/planning/kohami/SPEC.md`、`docs/issues/kohami`、`docs/proposals/kohami`。

所有 CLI、csproj、Markdown relative link、來源 hash 与 core34 projection 同步。開發中已啟動的舊程序仍使用舊 binary；新建置驗證後才替換程序。`.local`、SQL instance、volume、信任鏈與原生金鑰不搬移、不重設。

## 5. 購物旅程驗證


驗證命令与結果見 [VERIFICATION](VERIFICATION.md)。核心 M0 完成、Phase2 啟用、正式營運分别保留原驗收條件。

## 後續遷移債務

1. CommerceService 的 HTTP、cookie、SQL 与 use case orchestration 分離：先 cart，再 quote／checkout，再 payment／refund。
3. 依實際需求定義聚合和值物件，保留 wire DTO 在 adapter；不讓 ORM row／JsonObject成為所有新 Domain 的模型。
4. Runtime 拆為技術專責元件；每次變更核對跨模組 transaction、native caller 与 consumer。
