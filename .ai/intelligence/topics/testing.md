# testing

區分結構、純規則、dedicated SQL、mock UI 與真 API browser journey；歷史 PASS 不代表本次執行。

來源：`docs/architecture/VERIFICATION.md`。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。

2026-10-15 Issue 06 current-candidate follow-up: Identity locked restore and dedicated SQL/pure-rule suite passed 30/30. Native lock metadata was synchronized to its declared IdentityAccess.Domain project reference; locked restore passed, Release build had 0 warnings/0 errors, and 47 synthetic checks passed. The restricted network proxy had returned 403 blocked-by-allowlist for nuget.org; approved restore access resolved the network block. Native checks do not prove Secure Enclave or OS user-presence. Provider/multi-worker fault matrix, independent database restore/cutoff reconciliation, live Owner authenticator and browser API aftercare remain incomplete. See the verification source for exact commands and limits.
