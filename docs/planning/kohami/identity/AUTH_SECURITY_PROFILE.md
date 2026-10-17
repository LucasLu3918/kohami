# 身份與授權安全基線

狀態：規劃基線。登入強化的可選改善建議集中於[選配清單](../options)。實際登入行為仍以執行中產品為準。

來源：[身份與資料政策](../../../proposals/kohami/03-identity-permissions-data.md)、[現行 mock 政策](../../../proposals/kohami/04-demo-commerce-policy.md)、[M0 契約](../contracts/API_M0.openapi.yaml)。會員與後台帳號使用個別帳號；角色、權限與資源範圍由服務端目前資料判定。

## 1. 身份、session 與授權

- Cookie 採 `HttpOnly`、`Secure`、`SameSite=Lax`、`Path=/` 且不設 `Domain`；固定本機 HTTPS 入口為 `https://localhost:3000`。所有業務 API 經同 origin 轉送；後端與 SQL 維持 loopback。
- Server 對 Host、Origin 與 forwarded headers 使用 allowlist。CSRF token 與匿名 BrowserId 不提供身份升權，也不可把 token 放入 URL、日誌或共享 cache。
- SSR 只轉送當前請求必要 cookie，不把私有資料或憑證嵌入共享 HTML cache；登出須清除 client 私有狀態並使 server session 失效。
- Member session 只授予本人資料能力。後台能力須逐請求依目前有效角色、permission、scope 與資源所有權裁決；cookie 或 UI 欄位不能作授權來源。
- Suspend、grant 變更、密碼重設及登出須依安全版本撤銷既有 session／proof。LastOwner、交易原子性與稽核不因登入方式變更而放寬。

## 2. 帳號與稽核

密碼使用 ASP.NET Identity 雜湊與服務端失敗節流；密碼不得進日誌、trace、analytics 或公開 API 範例。密碼變更、帳號停用、grant 變更與登出須依安全版本撤銷既有 session。帳號復原不得直接授予角色或復活已撤銷的 grant。

角色與授權變更須保留不可由日常操作者改寫的最小稽核資料。未來登入強化需求只記錄於[選配清單](../options)，不構成目前政策或實測證據。
