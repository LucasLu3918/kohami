# KOHAMI 專案指示

## 架構與設計

- 採模組化單體，依業務責任組織程式，保留已採用 ADR、術語及交易不變條件。
- 先閱讀 `docs/README.md` 與 `docs/architecture.md`；現行狀態以文件入口指定來源為準。
- 使用 DDD 分析資料所有權、聚合、不變條件與交易邊界。資料夾是業務模組，不直接代表限界上下文；邊界調整須說明理由與影響。
- 複雜交易規則採實體、值物件與聚合；簡單 CRUD 採最小必要設計。
- 遵循 Clean Architecture：Domain 不依賴 HTTP、SQL、UI 或外部服務；Application 協調使用案例並定義所需 ports；Infrastructure 實作儲存與外部整合；API／worker 負責入口與依賴組裝。
- 遵循 SOLID，保持責任清楚、介面精簡與依賴反轉，不為形式一致增加抽象、介面或專案。
- 模組透過公開契約互動，不任意讀寫其他模組的內部資料；跨模組原子交易依架構文件協調，不能改成非同步事件而破壞庫存／訂單／退款一致性。
- 現有 Foundation adapter 尚有 HTTP／SQL 耦合，依 `docs/architecture.md` 中目前的模組邊界漸進調整；新增純業務規則不得繼續塞入共用 Runtime。
- Nuxt 沿用框架的路由與元件慣例，按功能組織；前端不裁決交易、金額、權限或庫存。
- 進行本機 AI 商品穿搭圖生成前，必須先讀 `docs/todo/options.md` 的 OPT-18 與 `templates/creative/` 的現存素材規範；依登錄的本機生成 profile 執行，現行目標為 `flux3-image-klein`。不得自動切換模型或改走雲端；新增模型須先建立 profile 並通過 runtime／權重預檢，預檢未通過就停止。

## 測試驅動開發

- 新增或修改可測試的產品行為，預設採 TDD：先寫測試並確認因預期原因失敗，再完成最小實作使其通過，最後重構並重跑相關測試。
- 修復缺陷先建立能重現問題的回歸測試；既有程式重構前補足受影響行為的保護測試。
- 測試以可觀察行為與業務不變條件為主，不綁定內部實作；不以測試數量或單一覆蓋率作為完成證據。
- 庫存、付款、退款、授權、冪等與併發規則，須包含適當的真 SQL／整合測試；mock 與單元測試不能取代交易一致性證據。
- 測試僅使用隔離合成資料；SQL fixture 必須明示 dedicated，不對使用中的展示資料執行重設。
- 純文件、檔案搬移、視覺探索與環境初始化採適當結構／建置／人工驗證，說明 TDD 不適用部分。
- 報告實際命令、通過／失敗／略過與限制；未執行不得標為通過，事後補測不得宣稱已採 TDD。

## 文件與任務

- `docs/` 最多 15 個非圖片檔案，外層只記錄現行架構、API、資料庫、操作和安全 Gate；圖解置於 `docs/images/`。
- 所有尚未完成的工作只寫 `docs/todo/plan.md`；獨立可選優化只寫 `docs/todo/options.md`；規劃矛盾只寫 `docs/todo/confuse.md`。這個目錄不得多於三份檔案。
- 原本的 ticket、planning、proposals 文件已退出現行 docs。`tests/fixtures/kohami-legacy/` 是被雜湊約束的歷史測試輸入，**不得引用它作為現行業務決策或使用者文件**。
- 專案規格若與現行程式有出入，先核對 `apps/`、`src/Modules`、`tools/native-operations/migrations`，再更新對應的現行文件與 `confuse.md`；未採用的方案只能寫 options。
- 私有設定、金鑰及實際憑證保留在 ignored `.local/`，禁止推送或存入文件。
