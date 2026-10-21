# 本機候選驗證 Gate

KOHAMI 將必要的 AIPS Integration Gate 程式固定在 `scripts/security/integration_gate.py`，避免 Gate 依賴安裝位置推斷 repository root。執行時以目前 Git worktree 的頂層目錄作為 repository root，並把相對檔案檢查命令固定在該目錄執行。

## 安裝一次性驗證環境

```sh
python3 -m venv .venv-validation
.venv-validation/bin/python -m pip install -r docs/quality/requirements-validation.txt
```

驗證環境只需要固定的 `PyYAML==6.0.3`；其餘 Gate helper 使用 Python 標準函式庫及專案內 vendored code。不得以全域 AIPS 安裝路徑替代本專案 Gate。

## 驗證精確候選

從 worktree 內任意子目錄皆可執行；`base` 與 `head` 必須是明確 Git commit，且 `head` 必須等於目前 checkout 的 `HEAD`。

```sh
.venv-validation/bin/python scripts/security/integration_gate.py \
  --profile docs/quality/VALIDATION_PROFILE.yaml \
  --base <base-sha> \
  --head <head-sha> \
  --format yaml \
  --omit-output-tail
```

必要 profile 會先以 strict publication mode 掃描完整候選樹、`base..head` 中的每個 commit blob 與 commit message。Gate 程式固定最低 coverage contract：`src/**` 與 `apps/api/**` 必須通過 Release build 及 Foundation 測試；`apps/storefront/**` 必須先安裝鎖定依賴，再通過 Nuxt typecheck 與 production build。這些路徑和 check IDs 由 Gate 常數驗證，profile 缺少、設為非必要、縮窄路徑或使匹配 check SKIPPED 時會 BLOCKED。Release build 使用標準 repo `bin/Release`／`obj` 路徑，讓 Foundation runner 能直接驗收建置產物。為保護既有展示環境，此 profile 以獨立、乾淨的 recovery worktree 執行，不會觸碰主要 checkout 的 ignored `bin/obj` 或 demo 備份；NuGet vulnerability audit 保持啟用，因此初次還原需要可連到 NuGet advisory service；storefront check 使用 Node.js 26+ 與 npm lockfile 安裝依賴。現有 Playwright P3 flows 需要已啟動的 HTTPS Nuxt、mock API fixture 與本機憑證；這版不把它們偽裝成離線 smoke，瀏覽器 smoke 保留在 P5。Gate 將 PASS 綁定精確候選、檔案清單、profile、scanner 與 policy fingerprint；任何變動後都必須重跑。

本機 Gate 不會授權 merge、release 或其他發布操作。它不實作 AIPS 的獨立審查簽章驗證與執行遙測選配；若 matrix 要求獨立審查，Gate 會回報未驗證並失敗關閉；`--observe-run-id` 不支援。必要候選掃描綁定 `--root .` 並禁止 `--path` 縮窄；候選 profile 若改變此契約會 BLOCKED。

## 固定來源與相依

Gate 主程式和 `aips_common` canonical hash helper 來自 AIPS `0.83.0`，來源 revision `6cfcdeb2edaf67b3e48aa70adbcfd8aebd416dbd`。來源檔 SHA-256：

- `integration_gate.py`: `5c896fd30ad0e23303b92db168e2fadec37b3006fa3129696644f4f6470cf1fe`
- `aips_common/__init__.py`: `394ca31dd4b1913246c957990af07696d884f703b20fabdd2d210a20b6029bef`
- `aips_common/canonical.py`: `a46dc7ed8d76e4d7fdc0c4dabaeee79b07e79a588209d646d38db098796e549b`

KOHAMI 修改 Gate 的 repository-root resolution 和子目錄執行工作目錄，並以 fail-closed 的小型本機 helper 處理未採用的遙測／獨立審查選配。更新 vendored code 時，重新記錄來源 revision 與雜湊並重跑完整 Gate。AIPS 安裝目錄未提供可辨識的頂層 LICENSE/COPYING 檔；此 vendoring 僅限本專案內部驗證使用。
