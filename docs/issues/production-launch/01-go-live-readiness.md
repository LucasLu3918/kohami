# 01: 正式啟用前整站驗收與上線決策

**What to build:** 以真實商家／商品、核准資金作業及指定部署候選完成正式啟用檢查，提交明確的上線／收款決策；不將本機 mock 交付直接當正式營運。

**Blocked by:** kohami/07；kohami/26；member-wallet/05；kohami/37（依本次已選的完整交付順序）

**Status:** needs-info

**Planning status:** PLANNED_NOT_IMPLEMENTED

## 完成與驗收

- [ ] 真實品牌、商家與商品來源證據由 kohami/07 完成；正式入金／退款、人工履約與憑證／資料政策完成適當審查，不採用已延期的信用卡或物流串接作為必備功能。
- [ ] 逐項採用正式入金可信來源、退款目的地、資金覆核與營運責任、通知／費率／配送規則、資料保存及事故流程；每項未決提供至少兩方案與推薦，未決不收真資金。
- [ ] 正式模式隔離 mock／合成資料與測試入口，私有憑證按角色提供；以受控測試確認付款、庫存、餘額、退款、配送、權限與通知，不以 mock UI 替代。
- [ ] 指定 candidate 的正式部署、TLS、權限、監控警示、備份與獨立 restore、帳務 cutoff 對帳、回復／停止策略有實際證據；秘密及真個資不寫入文件。
- [ ] 核對本輪換裝範圍的合法素材、人工品質、支援尺寸／SKU、收藏匯出、權利撤銷及效能證據；未合格組合不啟用。
- [ ] 取得明確的正式部署、公開啟用與收款授權；票券 resolved 或文件採用不替代實際批准，未執行不得標 PASS。
- [ ] 上線後再執行 kohami/38 的實際觀測；月可用性等尚無完整期間者標未驗證，不能假造正式 SLA 已達成。

## 來源與範圍

- [執行主計畫](../../planning/kohami/delivery/IMPLEMENTATION_PLAN.md)、[驗收定義](../../planning/kohami/delivery/VALIDATION_PLAN.md)、[會員儲值詳規](../../planning/kohami/commerce/WALLET_SETTLEMENT_SHIPPING_PLAN.md)、[作業規則](../../agents/issue-tracker.md)。
- 補齊 Issue 26（本機交付）與 Issue 38（正式營運後觀測）之間缺少的「正式啟用前驗收」；本票建立未批准部署、付費或正式收款。

## Comments

- 2026-10-15：使用者明確選擇將展示與正式查核分開；Issue 22 不再因真實商業查核阻擋本機展示。本票承接正式審查與啟用責任。
