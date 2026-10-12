# 議題追蹤：本機 Markdown

本專案的議題與規格存於 `.scratch/`。

## 檔案慣例

- 每個功能使用 `.scratch/<feature-slug>/`。
- 規格存於 `.scratch/<feature-slug>/spec.md`。
- 每張實作議題各自存於 `issues/<NN>-<slug>.md`；
  編號從 `01` 起，不合併成單一議題檔。
- 議題頂部以 `Status:` 記錄 triage 狀態；
  名稱見同目錄的 `triage-labels.md`。
- 討論依時間順序追加於檔案底部的 `## Comments`。

## 技能要求發布議題時

在 `.scratch/<feature-slug>/` 下建立對應檔案，
必要時建立目錄。

## 技能要求讀取議題時

讀取使用者指定的檔案；只提供編號時，
先在相關功能的 `issues/` 目錄定位。

## Wayfinder 操作

- 地圖：`.scratch/<effort>/map.md`，
  包含 Notes、Decisions-so-far、Fog。
- 子議題：`.scratch/<effort>/issues/NN-<slug>.md`，
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
