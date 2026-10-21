# 隔離資料庫還原與 cutoff 對帳選配改善

狀態：`DEFERRED_OPTION`｜非本機核心／Foundation／M4 整體驗收門檻

## 範圍

本計畫收納獨立資料庫還原、cutoff 後撤權／刪除／hold 重套，以及 pending 帳務與事件去重對帳的實機演練。這項改善不阻擋 Issue 06、Foundation、M4 或本機核心整體驗收；目前也沒有因此宣稱相關能力已執行或通過。一般交易、授權與 worker crash/restart 仍依各自必備案例驗收；獨立 clean-checkout restore/build 與第二位操作者 handoff 另列於[clean checkout 選配計畫](CLEAN_CHECKOUT_REBUILD_HANDOFF_OPTION.md)。

正式上線若要設定備份、復原目標或法遵承諾，須在上線範圍另行決定並取得適用證據；本機選配演練不代表 RPO／RTO 已確認。

## 重開條件

開始前先指定隔離目標、專用 synthetic dataset、可用備份及其 key／schema 相依、停止 dispatch 的方法、cutoff 後撤權／刪除／hold 來源，以及測試後清理責任。無法證明目標隔離或取得較新台帳時停止演練，不覆寫來源 DB、不重播已完成交易、不以空台帳推定沒有撤權。

驗收可觀察資料與權限狀態：來源 DB 保持不變；撤權／刪除／hold 先於重新啟用套用；已處理 operation、pending 額度、source 去重、outbox 與 audit 對帳一致；無法重建的未知項維持 dispatch 禁用並標示 BLOCKED。只記候選、版本、合成資料摘要、結果與限制，不保存秘密或原始憑證。

## 既有來源

- [跨階段驗證計畫](../delivery/VALIDATION_PLAN.md)：原 CHK-07／M4-AC09 條目已降為選配。
- [技術基線來源綁定](../delivery/technical-baseline/SOURCE_BINDING_PLAN.md)：原 BIND-10 與 Foundation-AC12 restore 證據改列選配。
- [安全作業 Profile 選配](SECURITY_OPERATIONS_PROFILE_OPTION.yaml)：保留原生操作與 restore 安全不變條件；此文件不批准啟用。
- [Owner 初始化與離線復原選配](OWNER_BOOTSTRAP_RECOVERY_OPTION.md)：保留其原生復原授權界線。
- [Owner 驗證器與原生 user-presence 選配](OWNER_AUTHENTICATOR_NATIVE_PRESENCE_OPTION.md)：Owner 驗證器與原生 user-presence 改善集中於該選配計畫。
