# PNG 解碼、加工、hash 與画布詳規

2026-10-13｜PROPOSED／READY_FOR_REVIEW｜SOURCE_UNBOUND／產品 NOT_EXECUTED

適配性：有條件適合。採受限worker處理既定非AI三視圖，不在API process或Nuxt瀏覽器處理不可信原圖。此設計補齊NEXT-04；容量、畫布與資源上限均候選，仍需DEC-06/07與真素材pilot。[profile](PNG_PROCESSING_PROFILE.yaml)記版本與待綁定欄位，不安装library、不解碼真素材、不宣稱安全PNG驗證通過。

## 1. 官方依據與library候選

W3C PNG第三版涵蓋靜態及動畫圖像，包含chunk次序、CRC、alpha與色彩資訊；本產品只接受受限靜態子集，不表示其他合法PNG不合法。[W3C PNG](https://www.w3.org/TR/png-3/)（核對2026-10-13）。CRC不是SHA-256或授權證據。

System.Drawing.Common只支持Windows，不符合macOS本機與Linux候選worker。[Microsoft](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only)。首選研究候選SkiaSharp 4.153.1：官方release可定位、wrapper LICENSE為MIT，仍要核對native notices、RID與漏洞。[release](https://github.com/mono/SkiaSharp/releases/tag/v4.153.1)、[license](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt)。備選ImageSharp可限制格式/allocator/parallelism，但Split License適用資格與成本須另確認，不能當作無條件免費。[配置](https://docs.sixlabors.com/articles/imagesharp/configuration.html)、[license](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE)。library可用並不提供OS隔離或CPU/記憶體硬上限。

SkiaSharp版本是待pilot候選，非已採用dependency；native package版本/RID/hash/lock、有效license/notice與CVE核對、實際worker source/container digest均UNBOUND。實作前先固定上述來源，不能浮動latest或退回有問題舊版。缺任一綁定拒真素材接受；不引入雲端解碼、shell影像命令或自寫完整PNG decoder。

## 2. 接收及有限格式政策

先串流到同Import Session私有staging，按實際byte數計上限並算sourceSha256；不信Content-Type、client hash或Content-Length。超限即停止接收、未引用物件受控清理。原檔驗完封存不可變，worker只能讀這份hash綁定物件，拒路徑/URL/symlink替換。

`KOHAMI_PNG_STATIC_V1`候選：PNG signature；IHDR第一且唯一、IEND唯一且終結；IDAT连续且非空、每chunk長度/CRC/次序有界核對；拒截斷、尾隨bytes與unknown chunk。IHDR只允8-bit truecolor RGB/RGBA（colorType2/6）、compression/filter method0、non-interlaced；拒palette/gray/16-bit/Adam7。它是較窄匯入政策，其他合法來源須在外部製作流程轉成新PNG後重新提供與審核，不在server偷偷轉檔。

chunk allowlist只有IHDR、sRGB、IDAT、IEND；sRGB必須唯一、IDAT之前且renderingIntent=0。拒APNG的acTL/fcTL/fdAT、iCCP/cICP/HDR、gAMA/cHRM與EXIF/text/time/private chunks；metadata被拒不能直接刪除後假稱原圖合格。這避免未知色彩／方向／動畫語意，可能增加製作者交付成本，須pilot確認。網站仍只交付重新編碼server產物。

檢查不是讀IHDR就結束：先用checked arithmetic算width×height與stride/RGBA bytes；有界inflate/完整decode核實scanline/filter/尺寸、結尾與完整像素；只接受成功全圖decode，不接受partial/incomplete結果。decoder返回frameCount與實際格式亦核一致；unsupported或unknown拒絕。前置scanner是最小格式限制器，不取代library full decode。

沿原候選每檔10MiB、每组三圖30MiB、width/height≤4096、pixels≤16,777,216；另提案chunk數≤256、每chunk≤10MiB、worker同時1張、memory硬上限512MiB、CPU10秒、wall15秒、no network、只讀輸入/隔離寫出。RGBA最大64MiB只是pixel buffer，不代表process總記憶體；硬上限必須OS/container enforce，managed timeout非硬隔離。時間/記憶體未知或達上限則Rejected，保留非秘密reason，不回stack或原路徑。

三圖循序處理，共用session lineage而非並行三份buffer；每張lease綁封存Session version、slot hash與dispatch epoch，cancel/expiry/restore後舊結果不可commit。worker不持SQL admin/使用者cookie/key原件；API只排工作與核結果。隔離機制必須在macOS host及Linux candidate分別驗證，不把docker名稱當無網路/硬限制證据。

## 3. 不可變畫布與加工順序

`NORMALIZED_CANVAS_V1`詳規候選每view共用Width/Height、sRGB、8-bit RGBA直通alpha（straight alpha）；Front/Right/Back仍原定義，不自動左右鏡射。pilot參考画布2048×3072，不是已公告素材規格；政策未配置實際尺寸則拒Accept，不能由第一張圖默認決定。三圖/角色與服裝必須同一canvas spec revision，variant/SKU/profile亦需精確綁定。

匯入不自動crop、resize、stretch、rotate或mirror；尺寸不符拒該processing candidate。RGB解碼後alpha補255；RGBA保留，完全透明pixel的RGB歸零避免隱藏色差。全透明圖拒品質批准；有效非透明邊界由人工核三方向，不只看bytes成功。safe re-encode後回到decode檢查尺寸、格式和pixelSha256一致；畫面品質與權利仍另審，Validated不等於Approved。

Alignment只在換裝合成套用，不烘焙進immutable素材：畫布左上為(0,0)，pixel中心(x+0.5,y+0.5)，先以左上為pivot scale，再平移。精確公式 `x'=scaleX*x+offsetX*W`、`y'=scaleY*y+offsetY*H`；view各別scale/offset，沒有隱藏fit-to-window。原scale(0,10]、offset[-1,1]仍工程候選；非等比例需人工檢查且不代表合身保證。

參數fingerprint使用固定micro-unit整數：round-half-away-from-zero到1e-6，僅有限JSON number；NaN/Infinity拒。候選canonical若仍送number，server正規化後read回實際使用值，審核綁其精確整數版本；不得hash文化區域不同的decimal字串。規格/採样kernel候選bilinear/premultiplied-alpha合成需精確engine/RID/golden pixels實證，未綁前不跨OS保證bit相同。採樣／source-over合成候選先以sRGB標準EOTF轉線性光，再premultiply alpha、bilinear及合成，輸出unpremultiply/轉sRGB，8-bit舍入half-away-from-zero並clamp；完全透明RGB歸零。EOTF/OETF實作與golden結果仍需綁engine版本，不以瀏覽器色彩管理代替。邊界外sample=透明；不自動裁掉非透明越界；有越界內容拒Compatibility批准，維持fallback。

層次固定base avatar（含適當Base Garment）→單件garment；若須遮擋mask，另不可變受審mask/spec版本，未提供就不假設自動修穿幫。匯出背景、品牌聲明、字型與擺放另是export spec，須bind字型授權/hash及canvas，不能拿import加工hash代替完整export hash；不新增AI/多層穿搭。

## 4. 四類hash與重現

| 名稱 | 精確輸入 | 用途／禁止混用 |
| --- | --- | --- |
| sourceSha256 | 實際接收原PNG bytes，無metadata移除 | slot身分與原件證據；client header僅assertion |
| pixelSha256 | ASCII `KOHAMI-RGBA8-V1`＋NUL＋uint32大端W/H＋row-major straight RGBA8（透明RGB=0） | 解碼／重新編碼像素一致性；不當原檔hash |
| recipeSha256 | ASCII `KOHAMI-RECIPE-V1`＋NUL＋受限canonical recipe UTF-8 | engine/version/RID、spec、視角/輸入hash、整數params與處理順序綁定 |
| deliverySha256 | 實際新編碼交付PNG bytes | 不可變物件/交付完整性；不保證不同encoder產同bytes |

recipe只含ASCII keys/IDs、bool、null、safe integer與SHA字串，禁止浮點、重複key、未知欄位、未排序view集合與使用者文字。JSON無空白、keys ASCII ordinal排序、UTF-8無BOM、整數十進位；不要把一般sort_keys稱完整JCS。[RFC8785](https://www.rfc-editor.org/rfc/rfc8785.html)提供canonicalization參考，本產品採明確受限subset。三視圖固定Front/Right/Back順序，set hash綁每view source/pixel/delivery hash及recipe；不同視角相同圖不因hash吻合就合格。

encoder候選輸出IHDR RGBA8、sRGB intent0、IDAT、IEND，non-interlaced、無metadata/time；filter/compression/IDAT分片及native encoder細節需綁定engine，不能虛構跨版本byte deterministic。重新生成若delivery bytes不同，建新processing revision並重審，不覆原object。原收藏繼續精確版本或顯示失效，不靜默換圖。若原物件存在，同idem/recipe取原實物，不重算後偷替換hash。

SQL／檔案界線沿既有匯入：先隔離輸出、核hash與redecode，後同交易commit精確refs/Audit/Outbox；失敗無公開URL，orphan清理重驗引用。recipe/原件/加工結果不符、依賴失效或權威讀取失敗拒Accept/render/delivery。restore驗source/delivery物件及recipe/engine binding、cutoff後rights/hold與新dispatch epoch；缺任何來源不得用相近圖補。

## 5. 產品驗收計畫（全部NOT_EXECUTED）

PNG-AC01：截斷/CRC錯/尾隨bytes/unknown chunk/APNG拒。PNG-AC02：超byte/pixel/stride/chunk/CPU/wall/memory受硬限制且API不受crash拖垮。PNG-AC03：同header不同實物拒，原件替換/symlink越界拒。PNG-AC04：不支援bit depth/interlace/color metadata拒，未配置畫布拒。PNG-AC05：三圖同spec／方向可定位，不自動縮放镜射，RGB/alpha/透明像素规则一致。PNG-AC06：safe re-encode redecode同pixel hash、無metadata，顏色/邊緣由人工Review。PNG-AC07：recipe key順序等價、負零/非有限/duplicate/float拒、micro-unit舍入邊界可重現。PNG-AC08：scale先於offset、pixel中心/越界/單層golden matrix與三view實證。PNG-AC09：source/pixel/recipe/delivery四hash錯用拒，同recipe不保證不同engine byte同一。PNG-AC10：舊lease/expiry/revoke/restore不commit，SQL crash不公開orphan、不刪已引用檔。PNG-AC11：library/native/hash/notice/CVE與隔離source能重現；未綁拒真素材。PNG-AC12：匯出最終交付核current rights与字型/聲明spec，失效原收藏不靜默换圖。

本轮只有profile結構及契約候選驗證，尚無decoder、PNG corpus、process限額、golden畫面或裝置測量結果。
