# Clean checkout restore/build 與獨立 handoff 選配改善

狀態：`DEFERRED_OPTION`｜不屬 Issue 06、Foundation、M4 或本機核心完成的必備門檻。

## 範圍

本選配集中追蹤從全新 Git checkout，以 locked NuGet restore 和 Release build 產生 Native／Commerce 驗收工具，再由第二位操作者依文件建立 dedicated SQL fixture、執行 migration／seed／bootstrap-denial／Commerce harness，最後唯讀核對 Strict TLS 與資料庫存在的可重現性證據。

不採用本選配時，現有 Foundation-AC12 的 `PARTIAL` 只代表這組證據不完整；它不阻擋本機核心完成，也不代表 restore/build 或第二位操作者重跑已通過。已記錄的成功與失敗結果維持為歷史事實，不刪除、不改標 PASS。一般 API／SQL、交易、授權、worker recovery 與 fixture 安全檢查仍依各自必備驗收執行。

## 若日後選擇優化

1. 指定候選 SHA，另一位操作者在獨立的乾淨 clone 執行標準 runbook；不複製 `bin/Release`、`obj` 或 `.local` 私有材料。
2. 以 locked mode restore Native 與 Commerce，再以 Release 設定建置；如需證明套件來源獨立性，使用專用空 NuGet package cache。不得以停用 NuGet audit 或僅使用既有快取宣稱標準 restore 通過。
3. restore/build 成功後，才建立新的 loopback dedicated Issue 06 fixture，執行 migration、`CommerceSeed.sql`、bootstrap-denial checks、Commerce SQL harness 與唯讀 strict-TLS fixture status。
4. 記錄候選 SHA、SDK／NuGet source、literal commands、各階段結果、fixture port／唯一 Compose project、清理或保留決定及操作者；不得保存或交付 secrets／原始私有設定。
5. 如清除 fixture，只能依本次 `.local/foundation/issue06-compose-project` 的精確 project name 移除本次建立的 Compose resources；不 reset 展示資料、不使用全域 prune、不操作其他 fixture。

## 已有證據與限制

- 曾在乾淨 tracked snapshot 完成專用 fixture setup／seed、bootstrap-denial、Commerce SQL harness 與 Strict TLS 唯讀核對；該次沿用候選既有 Release binaries，不能證明 clean source-to-binary build。
- 多次標準 locked restore 在目前環境停滯或因 NuGet audit connectivity 失敗；關閉 NuGet audit 後使用既有 cache 的 restore/build 僅是有限補充證據。
- 尚無第二位操作者獨立重跑本流程的證據。

詳細日期、命令與限制保留於[實作狀態](../delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml)與[驗證紀錄](../../../architecture/VERIFICATION.md)；本機命令仍只由[操作手冊](../delivery/LOCAL_DEMO_RUNBOOK.md)維護。
