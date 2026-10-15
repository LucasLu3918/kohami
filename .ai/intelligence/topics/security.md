# security

非 Owner 後台角色依密碼＋TOTP；Owner TOTP／原生 user-presence 強化為選配，Owner 仍受有效角色、permission、scope 與逐資源檢查。Issue 06 M0-A 的 BIND-07 已在候選 `321d63abd49f430b897c65b3a0aab95514038092` 綁定 Identity 實作與測試，專用 SQL／純規則測試 30/30 通過；M0-AC08／09 仍為 PARTIAL，未測的 Origin／CSRF、nonce、logout 與 SSR persona/cache 不推定通過。售後 UI 使用合成預先授權 Owner session，不能當作登入 MFA 證據。Phase2、真供應商及部署不在目前 M0 範圍。

來源：`docs/planning/kohami/delivery/technical-baseline/APPROVED_SCOPE.yaml`、`docs/planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml`、`docs/architecture/VERIFICATION.md`、`docs/planning/kohami/identity/AUTH_SECURITY_PROFILE.md`、`docs/planning/kohami/product/DECISIONS_ASSUMPTIONS.md`、`docs/planning/kohami/options/OWNER_AUTHENTICATOR_NATIVE_PRESENCE_OPTION.md`。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。
