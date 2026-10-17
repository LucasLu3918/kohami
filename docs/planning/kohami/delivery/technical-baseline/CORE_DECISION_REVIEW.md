# 核心 DEC-01–03 審閱與 M0 範圍確認

## 現行適用範圍（2026-10-15）

本文保留需求、設計與具日期的驗證依據；目前已有 .NET／Nuxt／SQL 程式、core34 採用子集與部分產品實證。完整 131 操作候選已建立，核心細則已部分採用；其餘功能／Phase2 不因此視為已批准或完成。產品逐項狀態、範圍與來源優先順序請由[文件入口](../../../../README.md)查閱；舊段落中的尚未建立／未決／全部未執行須依原日期與案例範圍解讀。


2026-10-13｜REVIEWED_WITH_BLOCKING_CORRECTIONS｜HUMAN_ADOPTED／M0實作進行中

> **採用與執行狀態（2026-10-15）：** 使用者已採用 DEC-01–03 修正方向並批准 M0-A/B/C 本機分段實作。現行範圍為 core34 API、native4 與 12 項驗收；批准紀錄及修訂見 [M0_SCOPE_CONFIRMATION.yaml](APPROVED_SCOPE.yaml)。全104核心、Phase2、真服務與對外部署仍不在本次範圍。實作目前部分完成，驗收證據見 [M0_IMPLEMENTATION_STATUS.yaml](IMPLEMENTATION_STATUS.yaml)。

結論：有條件適合採用核心方向並分段實作M0。現行候選有三項可重現的整合缺口，應先修正候選再綁handler；不能把結構checker的PASS當作語意審阅無問題。[範圍確認包](APPROVED_SCOPE.yaml)固定本次來源hash與34操作清單。這是主Agent審閱，未宣稱獨立安全審查或產品驗收。

## 1. 審閱發現與修正要求

| ID | 證據／影響 | 建議修正與退出條件 |
| --- | --- | --- |
| REV-01 | API_CANONICAL servers=https://localhost/api；AUTH_SECURITY_PROFILE、KEY_CERT計畫及原M0為https://localhost:3000/api/v1。同origin與反向代理路由尚不一致 | 固定單一本機HTTPS入口localhost:3000、API base /api/v1；loopback backend與SQL。server/profile/Origin/cookie/SSR代理一致後才寫handler；錯Host/Origin拒绝 |
| REV-02 | CANONICAL_OPERATION_MAP的createPaymentAttempt要求If-Match/expectedVersion，但version.target=null；cart/Quote/Checkout继承操作的read/version來源亦未明列 | 每操作列aggregate/read操作與body欄位：Cart.version、Quote.id/version/expiry、CheckoutStatus.version→付款expectedVersion；建立操作不造不存在ETag。header/body對同aggregate，dependency versions分開；加入錯aggregate拒例 |
| REV-03 | M0模板仅24個唯一HTTP operation；createCart後無setCartItem/read、native Owner Email驗證無public context/confirm、revoke grant缺grant read來源、guest取消缺驗證入口 | 加10個既有核心operation，共34，不新增URL；以操作和persona實際串接驗收。若僅harness injection，明示其限制不能當瀏覽器旅程已通 |
| REV-04 | map部份非admin stage=CapabilityOrPublic、近期驗證=PurposeBoundAuthStage；原security OR/AND仍在，但泛稱不足以實作scope裁決 | 在M0 projection逐操作固定cookie組合、ownership/intent、current Dataset、auth-stage/rebind能力與resource來源。claim等AND不得變OR；未列入口不註冊 |
| REV-05 | Core與Phase2共用完整candidate，新增Phase2 enum可被讀取；核心開關僅extension敘述 | 建core-only路由／grant-purpose allowlist與serializer/proof測試；M0不接受Phase2值，不從全檔生成默認client。原候選保留可追溯 |

REV-01–03為阻擋實作交接的確定文件缺口；REV-04/05為必要closure。未觀測runtime越權或資料遺失，因產品尚未實作。先修正源builder/候選/map与checker及readiness hashes，再以修正後候選綁來源；不得只更新hash掩蓋差異。

## 2. DEC-01 建議採用內容

採用core95整合方向及CAN-01–09/13/14的共用Problem/Session、不同aggregate ETag、source fingerprint/業務idem、TWD/unit allocation、Unknown/晚到補償、三語与private no-store規則，**以前述修正完成為條件**。shipping.configure與demo.control僅允當前有限Dataset grant；Support不取得refund.execute。

M0只完成34操作切片；95是後續核心總範圍，不是首批全數交付。完整candidate131、Phase2 enum和九事件不因本次core決議啟用。client generator、toolchain版本与native library綁定由工程依已選架構調查、鎖版及驗證；不存在的source或無法實現的邊界須先回報，不能自行切換重大架構。

## 3. DEC-02 建議採用內容與取捨

| 項目 | 建議採用的本機核心細則 |
| --- | --- |
| 密碼與登入 | 密碼15–256字元、server hash/blocklist；身份stage受限且不自行授予業務permissions |

| 密碼限次 | account5次/15分及browser/IP20次/15分作起始候選，觀察正常使用與防濫用；不靠restart清計數 |
| 復原 | 10組各≥128bit單次離線碼，只存hash/setVersion/UsedAt；僅RebindRequired，重綁再登入且查現行grant；不延grant或抹已用紀錄 |
| Owner業務角色 | Owner 仍是退款等受控業務操作的角色；帳號初始化及首次授權由受控維護流程管理 |
| native維護例外 | M0保留有限grant續期／移交、key rotation與restore checkpoint；每action精確target/epoch/preview差異、OS重新驗證與一次permit10分鐘；網站不自授，grant renewal/transfer不復活因事故撤銷的權限 |
| keys／SQL／TLS | session ring受保護並保留必要舊私key；選配登入憑證細節見option；API/worker/migration分principal、不授runtime db_owner；產品SQL驗chain/SAN，不靜默TrustServerCertificate fallback |

仍在 M0 的有限 grant 與 native key／restore 操作須經精確目標、OS 確認與可追溯授權；這不等於簽發實際permit、授SQL admin、信任憑證或指定真人。

## 4. DEC-03 建議採用內容

M0先有zh-TW/ja/en的SSR基本摘要、登入、操作結果與明示mock/noindex；固定合成persona，cookie/stage不足不讀私有資料，切actor/返回頁面不洩舊內容。語系切換保留SKU/cart/operation，失敗/Unknown呈現下一步查詢，不重建付款。

M0驗收必要：適用的M0-AC、真SQL20client最後一件/多SKU原子、退款原來源不超退、兩worker/重送/撤權/重啟。獨立 clean-checkout restore/build／第二位操作者 handoff、資料庫 restore／cutoff 對帳及 live native OS trust/user-presence 為選配改善，不作 M0/M4 整體結案門檻。基本鍵盤、label、焦點、錯誤提示與縮放先隨M0頁面完成；M4完整WCAG2.2 AA適用性與真Safari人工證據仍保留，不能以掃描無錯聲稱全部AA。

Q-01/02/09數值（頁面體驗、讀API P95≤500ms、總記憶體≤8GiB）採為**核心M4驗收目標**；M0固定環境先量測與報偏差，不把小樣本lab當正式使用者percentile或SLA。高風險完整性/越權/秘密洩漏沒有容許額度；可調品質數值需人工審閱新證據，不能因慢就自行降標。工時為工作量估算，非截止承諾。

## 5. M0 三段實作與完成界線

- M0-A：修REV-01–05與core projection；綁toolchain、隔離產品SQL與最小principals、同originNuxt SSR、native key維護、Email/password/session與撤權。
- M0-B：34操作的剩餘cart/quote/checkout/自己的訂單、受控mock支付／退款、持久outbox/worker与source ledger；SQL併發/Unknown/晚到及不同退款來源完整性。
- M0-C：故障／兩worker／期限邊界、clean setup/seed/start/stop、適用 M0 情境證據與人工交付；restore／cutoff 與 native OS 人工證據列選配。

包含四項受保護native維護動作的實作/測試，但每次真維護操作仍按native確認與精確target，不允web呼叫。新產品DB不覆KohamiTest或既有volume；研究/測試依賴可在隔離工作區鎖版，不擴張為真供應商/雲端/採購。

不含M1–M3完整UI、全95核心操作、Phase2素材/PNG/收藏匯出、Passkey、真金流/物流/發票、正式費率/人員、對外部署或Git remote發布。通過M0不自動關票06、不代表LOCAL_COMPLETE；原38票依賴與完成條件保持。

## 6. 原審閱版決議欄（歷史）

本節原先記錄待確認的 DEC-01–03 與 M0 實作授權。使用者已於 2026-10-13 採用修正方向並批准分段實作。現行批准範圍以 [M0_SCOPE_CONFIRMATION.yaml](APPROVED_SCOPE.yaml) 為準：core34、native4、12 項驗收。
