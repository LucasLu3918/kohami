# M1 商品上架、探索、購物車與報價詳規

> **現行 MFA 決議（2026-10-13）：** 使用者明確「先採用B」，第一版後台採 Email／密碼＋驗證器 TOTP，Passkey 留後續。下文先前「待決／Passkey優先」是歷史狀態，已由此決議取代。方案選擇阻擋已解除；註冊／proof／復原機器契約與產品實證仍待完成，不能標為 PASS。詳見 [身份詳規](AUTH_SECURITY_PROFILE.md)。

日期：2026-10-13｜READY_FOR_REVIEW｜新細節為提案，未實作產品

來源：[現行政策](../../proposals/kohami/04-demo-commerce-policy.md)、[畫面](EXPERIENCE_DESIGN.md)、[API](API_SPEC.md)、[合成fixture](fixtures/M1_CATALOG.json)、[三語內容契約](CONTENT_LOCALE_CONTRACT.md)。MFA待決不妨礙文件規劃；真正後台實作仍不能繞過M0／票06依賴。

## 1. 商品發布與展示邊界

候選Product狀態Draft→Published→Unpublished；更新內容以新Revision與Version保存。發布／下架為受限command，不提供任意status PATCH。Owner或具商品範圍Merchandiser＋MFA可維護；Support／Analyst不得修改價、SKU／庫存或媒體權利。

商品可發布檢查：唯一slug／SKU、至少一SKU、合法TWD示意價、材質／實際衣服量測欄位與單位、可用語系內容、公開媒體來源與替代文字、mode=mock及明顯示意標示。庫存0仍可發布為缺貨；不能把缺貨和下架混成一個狀態。mock fixture允許自有中性占位圖及合成量測，但明示非實物，不冒稱商品真實性查核已完成。正式商品照片／素材授權另須真實證據。

上架失敗回逐項code／欄位，不僅「儲存失敗」。來源授權文件不回public DTO；published product內容不可含管理備註、PII、secret。媒體停用／權利撤銷同步停止受影響展示，保留有權照片或中性占位說明；換裝資格仍獨立，不因商品圖可載入就設定fitReady。

## 2. SKU、庫存與搜尋

- SKU由product＋colorId＋garmentSize明確唯一對應；更正命名不覆寫原交易快照。身形與衣服尺碼維持獨立；商品頁初次不自選SKU，只明示價格／範圍及「請選顏色與尺碼」。
- 可售判斷＝Product published、SKU active、必要商品／價格有效、SellableStock>0；FittingReady只依素材資格。不公開內部Reserved、Movement／供應商成本；mock測試fixture可記這些欄位，public DTO不能直接回整份fixture。
- 加車前確認SKU存在與可售；cart不保留庫存，查詢可售不保證結帳成功。庫存變動由權威SQL及版本，後續checkout原子重驗。已在cart而後缺貨／下架，保留項目及原因供移除或重選，禁止確認。
- 搜尋提案：q長度≤100，trim並正規化Unicode；搜尋已發布的name／SKU公開描述，category/color/garmentSize/availability為allowlist。未知篩選422；顏色＋尺碼必須同一active SKU符合，不能用不同SKU交叉誤配。
- 列表缺貨商品仍可出現，選in_stock才只留存在至少一合格可售SKU的商品。商品priceMin/Max只取有效公開SKU，不能以草稿價或不可售活動價誘導。
- 查詢語系進SQL／read model條件；限制20/100分頁，穩定sort＋ProductId tie-break，不做客戶端先取所有產品再篩未發布。商品改價／庫存造成分頁變動須明示refresh條件，cursor不是保證不變快照。

## 3. 購物車與Quote契約補充

OP-03–06沿M0機器契約；M1增加移除、單一優惠碼、商品完整public DTO及管理發布command。新增／更新／移除cart需Idempotency-Key與If-Match，回新version／ETag；同key不同quantity拒絕，合法重送不重做。原匿名BrowserId能力控制自己的cart，不按locale換cart。

Cart item回skuId、productSlug、公開localized名稱／顏色、GarmentSize、Quantity、目前示意價、可售判斷／原因、價格與庫存版本；不是不可變訂單快照。下架不能刪已成立OrderSnapshot；cart價變會逐項提示，不默默當作原價保證。

Quote提案有效期5分鐘，與checkout成立後的15分鐘Reservation分開；Quote不保留庫存。這是工程預設，待採用／實證。新Quote由server計算商品總額→單優惠→原單位分攤→示意運費→總額；返回price／fee／promotion版本與expires。免費門檻仍關閉。數量／小數／溢位／負fee／無報價／不支持區域拒絕，不以0運費回退。

固定fixture：500×1＋650×2＝1800；DEMO15折270、商品實付1530、示意運費60，總額1590。SKU第二行兩單位實付552/553，原單位ordinal保存。這些是合成算例，不是正式價／費用。

checkout重新驗Quote版本／expiry、SKU／Quantity、費用與身份。若任一變動，保留表單與cart，409 QUOTE_CHANGED／STOCK_CHANGED等；返回可以重取報價的資訊，不替顧客自動接受新總額，不延續舊操作偽造成新成功。

## 4. 草稿、改價、下架與cache

Published內容更新先DraftRevision審閱，再原子切public revision；失敗不部分發布。某語翻譯缺漏按內容契約fallback，不能悄悄更改slug／重建商品身份。下架後新checkout拒絕，已確認交易按原snapshot履約／售後，不因下架被任意取消。

商品可用短效public cache，但key含dataset/locale/ProductRevision，無member cookie私人payload；Price/Stock在Quote／checkout由server重驗。public catalog失效策略須追版本／下架／media revoke，不能靠5分鐘cache掩蓋已禁止展示的素材。具體快取期間由M1效能實證決定，未作正式性能承諾。

Admin畫面顯示已承諾Reserved但不允許任意覆寫；StockAdjustment只動OnHand與唯一Movement，OnHand不得低於Reserved。沒有「一鍵把庫存清零」解除既有承諾的路徑。

## 5. 驗收追蹤（全部產品NOT_EXECUTED）

| ID | 可觀察情境與預期 | 來源 |
| --- | --- | --- |
| M1-AC01 | 草稿不可public查；完整mock可發布，缺SKU／來源／替代文字／價／內容拒絕，越權不能上架 | 08、OP-31、DOM-01 |
| M1-AC02 | color/size唯一SKU，未選不加車；同名尺碼不帶出角色身形；下架後歷史單保持 | 08/11、SCR-03 |
| M1-AC03 | 在庫／缺貨／下架／fitReady分開；StockAdjustment不低於Reserved且可稽核 | 09、OP-32 |
| M1-AC04 | color＋size由同SKU匹配；未知filter／空結果／query失敗有清楚下一步；未發布不洩漏 | 10、OP-01 |
| M1-AC05 | 三語SSR／切語保留cart及operation；缺翻譯fallback明示，不宣稱完整翻譯 | 17、locale contract |
| M1-AC06 | cart新增／修改／移除及同key重送唯一，舊version不覆寫新quantity；無Reservation | 11、OP-03–05 |
| M1-AC07 | Quote算例1590、固定單位分攤與5分鐘有效期；無fee／小數／zero total不確認 | 11、OP-06、DEMO-04 |
| M1-AC08 | 改價／下架／缺貨／fee變動使Quote失效，保留輸入重新確認、不擅自多扣 | 11/12、OP-07 |
| M1-AC09 | 來源／素材撤銷停展示與cache，商品獨立可售評估；不假造換裝合格素材 | 08/31、DEMO-01 |
| M1-AC10 | 真SQL／瀏覽器旅程、mobile／鍵盤／錯誤焦點；fixture結構通過不代表產品通過 | Q-03–06、CHK-02–05 |

## 6. 交付與線框

[桌面商品線框](wireframes/M1-product-desktop.svg)與[手機購物車線框](wireframes/M1-cart-mobile.svg)是靜態規劃，不可點擊交易、不含app/API。畫面是在明確選好SKU與已有fixture cart後的狀態；不代表初進頁面自動選尺碼。欄位全部標mock／示意；後續還需完整視覺稿／真正device與interaction驗收。

M1依票06完成後，按08→09→10/11實作；不從本文件解鎖依賴或標resolved。下一個機器契約子集須由本詳規映射OP-01/02/05/31/32/33，與M0已存在path一致；本輪先完成內容／行為與線框，不生成產品或跑migration。

## 本輪文件／素材檢查

2026-10-13：三商品六SKU／三語完整、灰色＋L必須同SKU的篩選反例、1590總額與425/552/553固定單位實付、SVG/PNG格式與尺寸、內部連結／diff whitespace及票08–11狀態／依賴不變檢查通過。兩張線框已用既有離線Chromium渲染並目視確認；首次直接SVG截圖逾時，改HTML包覆後成功，沒有安裝套件。唯讀政策複核無必要矛盾，日文fallback文字已修正。全部產品M1-AC仍NOT_EXECUTED；這些不是SQL／網站E2E或素材真實性通過。
