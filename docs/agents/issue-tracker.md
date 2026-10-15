# 議題追蹤：本機 Markdown

本專案的議題與規格存於 `docs/issues/`。

## 檔案慣例

- 每個功能使用 `docs/issues/<feature-slug>/`。
- 規格存於 `docs/planning/<feature-slug>/SPEC.md`。
- 每張實作議題各自存於 `<NN>-<slug>.md`；
  編號從 `01` 起，不合併成單一議題檔。
- 議題頂部以 `Status:` 記錄 triage 狀態；
  名稱見同目錄的 `triage-labels.md`。
- 討論依時間順序追加於檔案底部的 `## Comments`。

## 技能要求發布議題時

在 `docs/issues/<feature-slug>/` 下建立對應檔案，
必要時建立目錄。

## 技能要求讀取議題時

讀取使用者指定的檔案；只提供編號時，
先在相關功能的 `docs/issues/` 目錄定位。

## implement-spec 工作完成狀態

- `Status:` 保留 triage 角色；`Work status:` 獨立記錄工作進度（claimed／resolved）。
- 未有 `Work status:` 的票券視為尚未完成；依賴只在前置票的工作狀態 resolved 後解除。
- 工作完成時核對本票驗收清單，於 `## Resolution` 記錄完成範圍、確認來源、證據與移交後續票的限制。政策規格票完成不代表產品測試或正式營運通過。
- 同一政策已取得明確採用後，不因原草案的歷史待審語句重問；實際人員、外部契約及執行證據另行驗證。

## Wayfinder 操作

- 地圖：`docs/planning/<effort>/map.md`，
  包含 Notes、Decisions-so-far、Fog。
- 子議題：`docs/issues/<effort>/NN-<slug>.md`，
  編號從 `01` 起，內文記錄待回答問題。
- `Type:` 使用 research、prototype、grilling 或 task。
- Wayfinder 工作狀態以 `Status:` 記錄，
  包含 claimed、resolved；與 triage 角色分開解讀。
- 依賴以 `Blocked by: NN, NN` 記錄；
  所列議題皆 resolved 後才能解除阻擋。
- 下一張議題從尚未完成、無阻擋且未被認領者中，
  依編號選擇。
- 開始工作前先寫入 `Status: claimed`。
- 完成後於 `## Answer` 追加答案，
  設為 `Status: resolved`，
  並將摘要與連結追加至地圖的 Decisions-so-far。


## KOHAMI 共用執行與驗收規則

本節合併原各票重複段落。執行起點、階段、決策與完整檔名只維護於 [執行主計畫](../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)。需求查 SPEC，逐票清單查票券，驗收判準查 VALIDATION_PLAN；不再複製成另一份交接計畫。

- `ready-for-agent` 是 triage，不解除依賴；無 Work status 視為未開始，claimed 不是完成。政策票 resolved 不表示產品通過。
- 先核對明確授權及相依票的完成範圍；需求不明或影響安全、成本、資料及相容性時，提供至少兩方案與一個建議，停受影響部分並繼續獨立調查。
- 以旅程交付必要資料、契約、後端、畫面、權限與錯誤行為；功能變更採 TDD，真 SQL 驗交易／競爭／冪等／授權，瀏覽器驗完整外部行為，素材權利／品質另有人工審閱。
- 核對輸入、輸出、資料、事件與 consumer 影響；記錄 candidate、fixture、實際命令、斷言、結果與限制。缺證據保持 NOT_EXECUTED／BLOCKED，不以工具退出 0 或測試數量推定通過。
- 只有隔離合成資料能用於測試；文件與 fixture 不含秘密、真個資或憑證。資料重設、付費／素材採購、正式收款／部署及換裝啟用仍按各自明確授權執行。
- 完成後保留 Comments 與 Resolution，指出完成範圍與實測證據；本機、會員實付餘額、換裝及正式啟用各自驗收，不能跨階段推定。
- 跨功能依賴使用完整 `<feature>/<NN>` 識別；新錢包票位於 `docs/issues/member-wallet/`，上線票位於 `docs/issues/production-launch/`，不改寫既有 kohami/01–38 的編號。
