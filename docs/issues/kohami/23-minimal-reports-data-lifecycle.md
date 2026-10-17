# 23: 最小化報表與資料生命週期

**What to build:** Analyst 只取得授權且最小化的營運資訊；保存、刪除及敏感存取依政策執行並可稽核。

**Blocked by:** 20：受控退款與財務核對

**Status:** ready-for-agent

**Work status:** claimed

- [x] Analyst 從後台取得已授權的最小營運報表，不能藉查詢或匯出取得超出必要用途的個人／交易資料。
- [ ] 依確認政策執行帳號、訂單、客服與日誌保存／刪除；依法應保留的交易證據及對帳一致性不被破壞。
- [ ] 敏感操作與授權存取可稽核，日誌／報表不暴露秘密，資料不用於本人身體或健康推斷。
- [ ] 驗證授權成功、越權拒絕、保存期限邊界及處理失敗；不擴充 Phase 3 的完整銷售轉換分析。

## M3詳規追蹤（2026-10-13）

參照[M3售後退款流程與驗收](../../planning/kohami/commerce/AFTERCARE_REFUND_PLAN.md)及[對帳／報表／資料生命週期](../../planning/kohami/commerce/RECONCILIATION_DATA_PLAN.md)。本次只補齊規劃、來源額度與靜態示意；本票產品驗收NOT_EXECUTED，原依賴與工作完成狀態維持。

## 來源與範圍

- 階段：Phase 1 核心商店。
- 驗收對應：R07、R18／AC07、AC18；User Stories 50、78–82。
- 共用執行、證據與授權規則見 [票券作業規則](../../agents/issue-tracker.md)；功能需求與驗收對應保留於本票。

## Comments

- 2026-10-13：使用者回覆「整份規格」，選定完整 Phase 1／2 與營運驗證範圍。
- 2026-10-13：使用者回覆「採用這 38 張」，確認本票所屬拆分、阻擋關係與交付範圍；不是產品已完成或實作／上線批准。



## Resolution（2026-10-18；最小報表子範圍）

已完成並驗收 Analyst 最小唯讀聚合報表子範圍；Issue 23 整體維持 `claimed`，不視為結案。後台 GET `/api/v1/admin/reports` 僅允許 `reports.read` 並固定檢查 `Report:operations`；API 只回傳合成資料按 UTC 訂單建立日及收款類型彙總的訂單數、已收、成功退款、待處理、可用、淨收及待補償訂單數。日期範圍必填、順序正確且最多 31 日。回應不包含個人資料、訂單／collection ID、SKU 或列級明細；Storefront 不提供匯出。畫面與 API 均標明資料是本機合成 mock、非正式財務報表，收退款依訂單日期分桶而非入帳日期。

- SQL acceptance 使用 dedicated `KohamiM0` 的合成訂單與 collections，驗證正確權限 scope、未知 Report scope 拒絕、聚合欄位、非法／超長日期拒絕、無列級識別資料，以及請求後清理合成資料。
- Playwright 驗證摘要畫面、UTC／mock 限制說明與 403 安全錯誤；Storefront typecheck、API／Commerce build 與專用 SQL fixture 證據記錄於[架構驗證紀錄](../../architecture/VERIFICATION.md)。
- 未實作資料保存／刪除期限及其清理流程；期限維持未決。GET 報表存取事件尚未增加獨立持久化稽核；票券中的稽核與資料生命週期條件仍未完成。未變更正式 Core34 合約、SQL schema／migration 或事件。
- Impact Graph 全域 API／data／events 覆蓋仍為 partial／unknown；已人工檢視現有 Storefront proxy、Commerce 路由、Report scope resolver、角色權限規則與 SQL schema，未宣稱全域圖完整。
