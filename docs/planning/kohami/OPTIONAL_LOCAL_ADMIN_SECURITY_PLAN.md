# 可選本機管理員初始化與安全實作規劃

日期：2026-10-15。狀態：**使用者已選擇方案 B；Development-only fixture bootstrap 已實作，人工驗證待執行**。

## 目的與範圍

此為可另外選擇的本機管理安全工作項，涵蓋 Secure Enclave OS 確認、管理員 MFA 的真實驗證器實證、首次 Owner 初始化／啟用。它不屬於 [Issue 01 結帳與庫存規則](../../issues/kohami/01-checkout-inventory-rules.md)，也不是顧客瀏覽、購物車或結帳流程的產品功能。

此規劃本身不改變已批准的 M0-A/B/C、目前 M0-AC 狀態或現行安全政策。選擇不實作某方案時，相關驗收需標示 `DEFERRED`／`NOT_EXECUTED`，不可改標為 PASS；若要從 M0-A 的核准範圍或驗收條件移除，須另行明確核准並更新範圍確認與狀態來源。

## 建議分期

1. **購物網站核心驗收：** 不要求 Secure Enclave 或首次 Owner 儀式；Issue 01 只驗證交易規則及其專屬驗收。未初始化管理員的環境保持管理寫入功能關閉。
2. **可選的本機寫入展示：** 如需在專用 synthetic fixture 驗證完整購物／管理旅程，另做 Development-only bootstrap adapter／fixture，明確綁定 loopback、專用資料庫指紋與 `demo_m0` dataset，使用合成帳號，且不可對外收款。它必須透過受控工具完成初始化與維護狀態協調；不得直接更新 `m0.SecurityState`、使用 DBA／SA 繞過 gate，或將 `initialize-rings` 當成 Owner 啟用。
3. **正式管理後台：** 在正式啟用管理寫入前，選定 Owner 建立／復原程序及管理員 MFA。目前 M0 已採用 Email／密碼＋TOTP；若改用公司身分服務或 WebAuthn／Passkey，屬未來另案決策，需獨立安全審查與核准。若 M0 展示啟用管理操作，仍須符合現行 TOTP 決議與實際驗證器證據。

## 可選工作項：`OPT-M0-ADMIN-01`

**名稱：** 隔離本機展示的首位管理員初始化與受控解除維護。

**推薦候選：** 專用 fixture-only Development 工具，使用一次性、短效、單次消耗的本機啟用材料；只建立合成 Owner 並留下最小稽核，成功後關閉 bootstrap。若只測顧客購物流程，不開放管理操作，因此不需要先完成 TOTP app 實證；若要測管理操作，延用 M0 已採用的 TOTP 方案並完成真實 app 驗證。這可避免把 Secure Enclave 簽署 profile 變成顧客購物驗收前置，同時保留管理寫入的明確授權邊界。

**可選實作驗收條件：**

- [ ] 僅允許明確 Development 設定、loopback 執行、核准的專用 SQL fixture／DB fingerprint 與固定 dataset；其他環境、資料庫或 dataset 一律拒絕。
- [ ] 只可在沒有既有 Owner／bootstrap 紀錄時啟動；啟用材料有期限、單次消耗、抗重放，並綁定目標 instance 與精確操作。
- [ ] Owner 經必要的 Email／密碼驗證；是否要求真實 MFA 依此工作項選定的管理後台方案執行，不接受 client 旗標或合成碼冒充真實驗證器證據。
- [ ] 建立最小稽核；成功後永久關閉首次 bootstrap；錯誤、逾期、重播及程序中斷維持拒絕狀態，不建立部分授權。
- [ ] 維護 gate 只可由受控流程在目標與來源證據核對成功後協調；不提供公開 bootstrap endpoint，不接受直接 SQL／高權限帳號旁路。
- [ ] 對管理退款、授權與使用者操作套用所選 MFA／近期驗證與逐資源授權；只測購物流程時不開放管理寫入。

## 方案取捨

| 方案 | 用途 | 代價與限制 |
| --- | --- | --- |
| A. 不做此可選工作項（建議的預設） | 購物頁面與不依賴管理寫入的核心驗收 | 不可宣稱首次 Owner、管理 MFA、原生 OS 根或管理寫入流程通過；目前 maintenance gate 仍會阻擋寫入型購物驗收。 |
| B. 專用本機 Development fixture bootstrap（推薦候選） | 本機 mock 展示與隔離 synthetic SQL 上的寫入旅程 | 需新增並驗證受限初始化 adapter；不能直接沿用或繞過現有 maintenance gate；不能作為正式環境方案。 |
| C. 保留原本的原生安全模型 | 本機高保證管理維護、Owner 啟用／復原 | 需要 Apple Developer Team、正確簽署的 app-like broker bundle、profile、OS user-presence 與完整人工驗收；此方案目前受帳號資源存取限制。 |
| D. 正式環境採標準化管理信任服務 | 正式管理後台與多操作者 | 需規劃首位 Owner 建立、復原、稽核與部署權限；若以 IdP／WebAuthn 取代目前已採用的 TOTP，須另行決策與安全審查。有整合及營運成本，不能視為本機方案的零成本替換。 |

## 決策與相依

- **決策（2026-10-15）：** 使用者回覆「A」，對應當時列出的建議選項「Development-only fixture bootstrap」（本表方案 B）。批准範圍限於隔離 synthetic fixture，用真實 TOTP 建立本機 synthetic Owner，以完成 Issue 02 售後 API／SQL 驗收；不變更正式安全政策、production、native OS authority 或 Secure Enclave。
- **已實作待驗收：** 僅 Development、localhost、DB fingerprint 與 `demo_m0` 綁定的 bootstrap；短效單次啟用、真實 TOTP proof、synthetic Owner、固定 grants 與 audit、成功後永久關閉 bootstrap；負向 SQL 檢查與售後 API Playwright journey 已備妥。SQL harness 51 項及錯誤 instance／直接 DML 拒絕檢查通過。真實 authenticator 的 TOTP proof 已通過一次，但後續啟用確認碼不相符，Owner SQL transaction 未執行；失敗時的記憶體 TOTP seed 已丟棄，需重新註冊新 URI。Issue 02 真實 API journey 尚未完成。
- **仍未決／未完成：** 完整 Owner bootstrap 與 Issue 02 真實 API／SQL browser journey。一次真實 TOTP proof 已接受，但啟用確認碼比對失敗，未建立 Owner；失敗後需以新 URI 重新註冊 authenticator。Secure Enclave OS confirmation 仍屬原生 M0-A 人工驗收，並未納入此次方案。
- **不阻擋：** Issue 01 的業務規則政策交付；也不增加一般顧客的登入或裝置需求。
- **仍受阻：** 目前既有 M0-A 原生 Owner 驗收，以及依賴 maintenance gate 開啟的 M0 寫入型 HTTP／瀏覽器旅程。此文件不改寫現況或授予繞過權限。
- **需要另行批准：** 改動原生授權／MFA／資料庫維護信任邊界，或開始任何正式管理與復原實作。

## 來源

- [M0 範圍與逐項狀態](M0_IMPLEMENTATION_STATUS.yaml)
- [原生 broker 與 maintenance gate 限制](../../../infra/m0/README.md)
- [M0 Owner／MFA 驗收](M0_READINESS.md)
- [TOTP 生命週期與敏感操作重新驗證](TOTP_MFA_LIFECYCLE_PLAN.md)
- [Issue 01](../../issues/kohami/01-checkout-inventory-rules.md)
