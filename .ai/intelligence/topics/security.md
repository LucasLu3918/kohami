# security

非 Owner 後台角色依密碼＋TOTP；Owner TOTP／原生 user-presence 強化為選配，Owner 仍受有效角色、permission、scope 與逐資源檢查。Issue 06 M0-A 的 BIND-07 綁定 Identity 實作與測試；後續 M0-AC08／09 source-bound acceptance 已 PASS：Identity suite 35/35，真 API wrong-Origin／missing-CSRF probes 通過，dedicated Playwright 驗登出、舊 cookie replay 與 authenticated-to-anonymous SSR 返回頁面。證據使用合成 identity/TOTP 與 dedicated demo_m0 fixture，不代表真實 authenticator 裝置或 OS presence。其餘 M0 案例缺口仍依逐項狀態表追蹤；Phase2、真供應商及部署不在目前 M0 範圍。

來源：`docs/planning/kohami/delivery/technical-baseline/APPROVED_SCOPE.yaml`、`docs/planning/kohami/delivery/technical-baseline/IMPLEMENTATION_STATUS.yaml`、`docs/architecture/VERIFICATION.md`、`docs/planning/kohami/identity/AUTH_SECURITY_PROFILE.md`、`docs/planning/kohami/product/DECISIONS_ASSUMPTIONS.md`、`docs/planning/kohami/options/OWNER_AUTHENTICATOR_NATIVE_PRESENCE_OPTION.md`。本檔為衍生索引；以原文件為準，未知 runtime 證據不推定通過。
