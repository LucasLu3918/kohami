# Owner 驗證器與原生 user-presence 選配

狀態：`DEFERRED_OPTION`。本文件集中保存 Owner 額外驗證與原生裝置人工實證的候選優化；不屬於目前 Identity 重跑、M0/M4 結案或核心交付的必要前置。尚未選擇採用任何額外登入因子或變更 native broker 行為。

## 選配範圍

- Owner 可評估是否啟用 TOTP；目前 Owner 登入與退款／授權／帳號管理驗收不以 Owner TOTP 為必要條件。其他後台角色仍依既有密碼＋TOTP 規劃。
- Issue 06 曾以專用 fixture 的合成預先授權 Owner session 執行 HTTP/API／SQL 售後檢查；此為該次 runner 的測試方式，不代表登入驗證器已通過。登入 TOTP／MFA 未在該次檢查中驗證。這項方法與證據界線集中記於本選配，未來若要驗證或強化 Owner 登入因子，須依「若日後選擇優化」重新確認威脅模型與驗收契約。
- 真實驗證器 app、Owner 裝置上的人工確認、Secure Enclave 與 OS user-presence 實測是選配證據，不阻擋 Identity 自動化測試或 M0/M4 文件結案。
- 原生 permit issuer 仍須使用受保護的原生簽章 key、限定用途／目標／資料集與一次性 permit；不得改由 HTTP/API 取用 issuer 私鑰，也不得加入軟體 key fallback。此選配不變更目前 broker 的 fail-closed 行為。

## 若日後選擇優化

重新開啟時，先提出 Owner threat model、是否要求敏感操作 step-up、Owner／LastOwner 復原及遷移相容方案，再明確選擇是否採用；只有核准後才更新正式登入／API／驗收契約。Secure Enclave／OS user-presence 人工證據須限定本機 synthetic dataset，記錄最小去識別結果，不留 raw prompt、畫面截圖或秘密材料。

本機提示只可經受控、隱藏輸入通道取得必要合成憑證；密碼、TOTP seed/code、復原碼、private key 與 connection string 不得貼入聊天、命令列參數、log 或可提交檔案。互動提示通道不可用時應停止該項人工驗證，不改用明文輸出或降低 broker 保護。

## 啟用條件

此 option 目前不授權產品程式、API、schema 或 runtime 變更。若使用者日後明確選擇，須另行檢視受影響的 Owner login、敏感操作、session／recovery、native broker、證據保存與相容性，再批准實作範圍。
