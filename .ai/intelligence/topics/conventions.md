# conventions

- 架構：模組化單體，依業務責任組織；Domain 不依賴 HTTP、SQL、UI 或外部服務，Application 定義 ports，Infrastructure 實作儲存與外部整合，API／worker 負責組裝；模組經公開契約互動，跨模組原子交易不得改為非同步事件。
- 前端：Nuxt 沿用框架路由與元件慣例；前端不裁決交易、金額、權限或庫存。
- 本機 AI 商品穿搭圖：先讀 `docs/todo/options.md` 的 OPT-18 與 `templates/creative/`；只能使用已登錄的本機 profile，不自動切換模型或改走雲端。
- TDD：新增或修改可測試產品行為先寫測試並確認因預期原因失敗；缺陷先建立回歸測試；庫存、付款、退款、授權、冪等與併發規則需真 SQL／整合測試；測試只用隔離合成資料。
- 文件：`docs/` 非圖片檔不超過 15 個；未完成工作只寫 `docs/todo/plan.md`，選配寫 `options.md`，矛盾寫 `confuse.md`；`tests/fixtures/kohami-legacy/` 不得作為現行決策依據。
- 私有設定與實際憑證只放 ignored `.local/`，不推送、不寫入文件。
- 回報規則：列出實際命令與通過／失敗／略過；未執行不得標為通過。

來源：`AGENTS.md`（2026-10-28 targeted refresh）。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。
