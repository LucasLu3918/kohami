# M4 本機驗收、復原與交付規劃

> **現行 MFA 決議（2026-10-13）：** 使用者明確「先採用B」，第一版後台採 Email／密碼＋驗證器 TOTP，Passkey 留後續。下文先前「待決／Passkey優先」是歷史狀態，已由此決議取代。方案選擇阻擋已解除；註冊／proof／復原機器契約與產品實證仍待完成，不能標為 PASS。詳見 [身份詳規](AUTH_SECURITY_PROFILE.md)。

2026-10-13｜READY_FOR_REVIEW｜全部產品案例 NOT_EXECUTED

來源：[交付計畫](IMPLEMENTATION_PLAN.md)、[品質Profile](../../quality/QUALITY_PROFILE.yaml)、[本機操作](LOCAL_DEMO_RUNBOOK.md)、[證據模板](M4_EVIDENCE_TEMPLATE.yaml)。本機完整mock核心不含真商務服務、對外部署或換裝素材啟用；票24–26依原任務圖完成，票38正式營運仍延期。此文件完善驗收規格，不授權故障注入或判定LOCAL_COMPLETE。

## 1. 候選與環境固定

一次驗收鎖定Git SHA、未提交差異摘要、完整契約版本、fixture版本／dataset、DB schema、API／Nuxt／worker build、SDK與browser版本、實際硬體及Docker平台。產品候選要求乾淨且可重建；任何產品／契約／fixture改動重新評估受影響checks，不將舊PASS直接套新SHA。

工具命令在產品建立後綁定實際路徑；現階段模板command為null，不發明可執行的API／Nuxt命令。17個純規則測試、SQL容器smoke及規劃結構檢查是分開的證據來源，不代替M4產品證據。ARM模擬SQL結果註明平台限制，不宣稱原生x64或正式效能；需要時另補隔離原生x64驗證，不新增主機費用承諾。

## 2. 驗收案例與必要斷言

| ID | 必要可觀察結果 | Required Checks／追蹤 |
| --- | --- | --- |
| M4-AC01 | 乾淨環境依文件啟動同fixture；只loopback、HTTPS與隔離通知；stop保留資料 | CHK-08；票26 |
| M4-AC02 | 會員A/B、guest與四後台角色不能跨訂單／案件；MFA與撤權不靠前台flags | CHK-03；Q-03；票25 |
| M4-AC03 | 三語SSR核心旅程完整；切語系不失SKU/cart/operation；手機錯誤與mock標示可讀 | CHK-04；Q-05；票24 |
| M4-AC04 | 鍵盤／VoiceOver／焦點／對比／縮放及限時適用性逐項有證據；自動掃描不替代人工 | CHK-05；Q-04；票24 |
| M4-AC05 | 同一候選之完整OpenAPI/DTO/enum／錯誤映射一致，非只驗M0子集 | CHK-01；Q-08；票26 |
| M4-AC06 | 最後一件20並行只有一承諾、多SKU任一不足全rollback；各原操作重送10次效果唯一 | CHK-02；Q-06；票25 |
| M4-AC07 | 退款來源／原unit／shipping併發不重退；553Unknown保留，本案只425；補償來源獨立 | CHK-02/03；M3-AC04–07；票25 |
| M4-AC08 | crash/restart跨15分鐘保留，不延長expiry；已Started未知只查原操作，兩worker不重發 | CHK-02/07；Q-07；票25 |
| M4-AC09 | 獨立DB restore後先套撤權／刪除／hold，核來源台帳、slots／outbox／稽核，不重演交易 | CHK-07；Q-07；票25 |
| M4-AC10 | 固定樣本分開成功／錯誤、冷／熱；Q-01/02/09量測可重現，不宣稱正式SLA | CHK-06；票24 |
| M4-AC11 | 合成reset先preview與授權，只作用指定dataset；不dropDB、刪volume或抹業務Audit | CHK-03/08；票25/26 |
| M4-AC12 | 所有適用checks有候選證據、未決表及限制；另一位操作者可依文件重建並完整展示 | CHK-08；Q-10；票26 |

DEMO-01–12逐一引用現行mock政策；M0/M1/M2/M3-AC需完整追蹤，不能只以上表12項取代原驗收。Q-01–10與CHK-01–08全部需覆蓋或明確範圍理由；不以任意測試總數代替斷言。

## 3. 故障與復原演練

所有故障注入先確認專用process／DB／dataset與可用備份，無法辨識即停止；不停止其他Docker專案、不刪volume。普通重新啟動、合成reset、版本回復、DB還原分別留證，不互相替代。

演練截點：交易commit前（整筆rollback）；commit後但dispatch前（持久Outbox續處理原Operation）；mock已Started但結果尚未入帳（Unknown保留且查原結果）；結果入帳後通知前（只重投原通知）；refund成功但receipt未完成（只續調整）。每截點記前後Order、Reservation、Movement、CollectionBalance、unit/shipping、Operation、Outbox與Audit狀態，不在日誌記secret或整包PII。

restore在新命名隔離目標DB執行，先停止該目標worker及任何自動dispatch；保留來源DB不覆寫。記備份cutoff與Schema／build相容性，離線核對後先重套cutoff之後已知的撤權、刪除與hold，再核事件／來源去重、已處理Operation與未決。無法取得cutoff後資訊即標BLOCKED，不把舊備份當最新真相；備份後觀測到的已Started／成功結果另對帳後才能解除dispatch禁用。通知不因restore重新發送已投遞歷史訊息。

回復完成以核心讀取、帳務與庫存一致、敏感權限拒絕及原未知追蹤為準，不以DB online為準。量測復原起迄、可用資料cutoff與實際損失；正式N04 RPO/RTO仍延期，本機演練不宣稱正式目標達成。

## 4. 判定與交付

每check記NOT_EXECUTED/PASS/FAIL/BLOCKED/UNVERIFIED與scope、命令、斷言、候選、限制。缺工具／MFA待決用BLOCKED，未跑用NOT_EXECUTED，證據不完整用UNVERIFIED；N/A須適用性理由與覆核，不為失敗排除check。FAIL修復後只重跑受影響範圍與必要整合，保留舊結果關聯。

LOCAL_COMPLETE只在適用checks、主要成功／拒絕旅程與獨立重建審閱均具完整證據，核心授權／交易／復原未決為零時評估；使用者尚未採用之品質提案須先確認有效判準。無資料損失／越權／重退／超賣可接受例外；其餘已知限制列影響與後續，不自行降門檻。MFA未決與完整M1–M3機器契約未展開仍為實作準備事項。

交付包含候選、setup/start/stop/reset/restore真命令、私有設定取得方法、合成情境、checks摘要、版本／平台限制、故障處理與延期清單。沒有真密碼／session／Email或provider憑證；驗收媒體去除token／身份資料。報告與測試產物預設先暫存，本機交付若需長期保存再確認位置／期限；本輪只保存規格模板，不保存原始輸出或效能trace。

## 規劃檢查（非產品證據）

本版已核對模板8個CHK／12個M4-AC引用、DEMO-01–12與Q-01–10集合、全部未執行狀態、隔離restore與dispatch界線、文件連結及票24–26的原工作狀態／依賴／驗收清單未變；實際10檔變更與核定範圍相符。這只證明文件結構一致，沒有執行setup、reset、故障注入、restore或產品驗收。

## M1–M3契約深化後的準備狀態

M1–M3機器契約已由[整合提案](API_M1_M3.openapi.yaml)展開，與M0差異見[一致性登錄](API_CROSS_STAGE_CONSISTENCY.md)；前述「待展開」狀態由本節更新為READY_FOR_REVIEW，尚非採用。MFA、完整身份維護契約與真產品命令／驗收仍未完成，全部M4產品案例NOT_EXECUTED。
