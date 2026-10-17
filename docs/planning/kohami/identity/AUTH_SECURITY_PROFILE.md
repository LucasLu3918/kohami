# 身份與授權安全基線

狀態：規劃基線。登入強化的可選改善建議集中於[選配清單](../options)。實際登入行為仍以執行中產品為準。

來源：[身份與資料政策](../../../proposals/kohami/03-identity-permissions-data.md)、[現行 mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md)、[M0 契約](../contracts/API_M0.openapi.yaml)。會員與後台帳號使用個別帳號；角色、權限與資源範圍由服務端目前資料判定。

## 1. 身份、session 與授權

- Cookie 採 `HttpOnly`、`Secure`、`SameSite=Lax`、`Path=/` 且不設 `Domain`；固定本機 HTTPS 入口為 `https://localhost:3000`。所有業務 API 經同 origin 轉送；後端與 SQL 維持 loopback。
- Server 對 Host、Origin 與 forwarded headers 使用 allowlist。CSRF token 與匿名 BrowserId 不提供身份升權，也不可把 token 放入 URL、日誌或共享 cache。
- SSR 只轉送當前請求必要 cookie，不把私有資料或憑證嵌入共享 HTML cache；登出須清除 client 私有狀態並使 server session 失效。
- Member session 只授予本人資料能力。後台能力須逐請求依目前有效角色、permission、scope 與資源所有權裁決；cookie 或 UI 欄位不能作授權來源。
- Suspend、grant 變更、密碼重設及登出須依安全版本撤銷既有 session／proof。LastOwner、交易原子性與稽核不因登入方式變更而放寬。

## 2. 敏感操作重新驗證

退款、權限變更、帳號管理及資料匯出等敏感操作，仍須依其獨立政策完成近期重新驗證。proof 必須由服務端綁定操作者、當前 session、安全版本、purpose、resource 與有效期限；不得以 client flag、單一時間戳或一般登入 session 取代。具體可用的驗證方式、有效時間與流程，依各操作政策另行核准，不在此推定為特定方法。

每次操作仍檢查目前授權與精確 scope。重新驗證不會擴張權限、延長退款額度、替換核准人或復活已撤銷的 grant。敏感資料不得進日誌、trace、analytics 或公開 API 範例。

## 3. 復原、稽核與驗證紀錄

帳號復原不得由單封郵件直接替代權限核准或已撤銷的 proof。角色與授權變更須保留不可由日常操作者改寫的最小稽核資料；密碼、token、私人憑證材料與完整支付秘密不得記錄。

歷史驗證結果集中於[架構驗證紀錄](../../../architecture/VERIFICATION.md)，不作為本規劃基線的採用依據。未來其他登入方式的改善需求，應先評估[選配清單](../options)，不得當成已採用需求或實測證據。
