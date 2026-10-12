# 領域文件

本專案採 single-context，不建立多領域目錄結構。

## 探索前的閱讀順序

1. 目前已有的 `docs/planning/kohami/GLOSSARY.md`。
2. 根目錄 `GLOSSARY.md`（若存在）。
   若有 `GLOSSARY-MAP.md`，依其指向閱讀相關術語表。
3. `docs/adr/` 中與目前工作相關的 ADR。

既有 KOHAMI 術語表是目前已確認產品語言的來源。
本次設定不搬移或複製它；若未來新增根目錄術語表，
需明確記錄兩者關係，避免形成相互矛盾的來源。

上述選用文件不存在時，直接繼續，
不主動要求建立。
由 domain-modeling 技能在術語或決策確定後按需建立。

## 文件布局

- 根目錄 `GLOSSARY.md`：single-context 的通用慣例，
  目前尚未建立。
- `docs/planning/kohami/GLOSSARY.md`：既有 KOHAMI 術語表。
- `docs/adr/`：領域與架構決策。

## 使用一致術語

議題、提案、假設與測試名稱應使用術語表的正式名稱，
避免使用已被排除的同義詞。
所需概念尚未定義時，先檢查是否誤造術語；
確有缺口則交由 domain-modeling 處理。

## 揭露 ADR 衝突

提案若與既有 ADR 衝突，明確指出 ADR 編號、
衝突內容與重新審議理由，不默默覆寫既定決策。
