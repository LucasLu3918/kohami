# Issue 12 專用 fixture 啟動與 development bootstrap 選配

狀態：`DEFERRED_OPTION`。本作業已移出目前執行規劃，不是 Issue 12、M0 或 Issue 13 開工的目前執行步驟，也不代表相關驗收已通過。

## 選配範圍

未來如需補足 Issue 12 的公開 API／SQL 證據，可評估建立或啟動符合 marker 與 strict TLS 條件的 dedicated synthetic fixture，並經既有受控的 development fixture bootstrap 流程完成 authenticator TOTP／人工確認，再執行 Issue 12 acceptance runner。

本選配只涵蓋開發驗收 fixture 的本機準備與 bootstrap 作業。它不改動產品登入／MFA 政策、Native 操作授權或維護閘門；不授權略過 bootstrap 驗證，不將 Owner TOTP 設為產品驗收前置，也不代表 API／SQL、庫存競爭、冪等、回滾或訪客 scope 已取得證據。

## 若日後選擇優化

1. 確認可使用的是專用 synthetic fixture，驗證唯一 marker、dataset、loopback 端點及 strict TLS；不得使用展示或一般資料庫。
2. 只在本機受控互動流程中完成 authenticator TOTP／確認；不得把 seed、code、密碼、私有設定或 connection string 貼入聊天、命令列、文件或 log。
3. 先通過既有唯讀 bootstrap verifier，再執行 Issue 12 runner；不繞過 maintenance gate，不改寫或重設非本次建立的資料。
4. 按 runner 記錄清理本次合成資料，保留實際命令、結果與限制；只有成功完成公開 API／SQL journey 才更新 Issue 12 證據。

重新啟用本選配需先確認 fixture 可用性與驗收範圍。選配本身不改變 Issue 12 `in-progress` 狀態、未驗收事實或 Issue 13 對 Issue 12 的既有相依。
