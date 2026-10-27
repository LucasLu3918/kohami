<script setup lang="ts">
import { localizedPath } from '../../utils/storefront-locale'
import { formatProductNumber } from '../../utils/product-number'
import { localizedProductMediaAlt } from '../../utils/product-media-alt'
import { PhArrowLeft, PhCaretLeft, PhCaretRight } from '@phosphor-icons/vue'
type Locale = 'zh-TW' | 'ja' | 'en'
type Measurement = { name: string; valueMm: number; synthetic: boolean }
type ProductMedia = { media: { url: string; alt: { value: string } }; colorId?: string; kind?: 'product' | 'outfit_concept'; concept?: boolean }
type CatalogProduct = {
  id: string
  slug: string
  mode: 'mock'
  translations: Array<{ locale: Locale; name: string; description: string; material: string }>
  media: ProductMedia[]
  measurements: Measurement[]
  skus: Array<{ skuId: string; priceTwd: number; colorId: string; garmentSize: string; stock: number; availability: string }>
}
const route = useRoute()
const router = useRouter()
const requestFetch = useStorefrontApi()
const { locale } = useStorefrontLocale()
const { source } = useDataSource()
const slug = computed(() => String(route.params.slug || ''))
const { data: product, error } = await useAsyncData(`catalog-${source.value}-${slug.value}`, () => requestFetch<CatalogProduct>(`/api/v1/products/${encodeURIComponent(slug.value)}`))
const translation = computed(() => product.value?.translations.find(item => item.locale === locale.value) || product.value?.translations.find(item => item.locale === 'zh-TW'))
const translationFallback = computed(() => Boolean(product.value
  && !product.value.translations.some(item => item.locale === locale.value)
  && product.value.translations.some(item => item.locale === 'zh-TW')))
const selectedColor = ref(String(route.query.color || ''))
const selectedSize = ref(String(route.query.size || ''))
const cartSuccess = ref(false)
const activeMediaIndex = ref(0)
const galleryMedia = computed(() => {
  const media = product.value?.media || []
  const productMedia = media.filter((item, index) => item.kind ? item.kind === 'product' : index === 0)
  if (!productMedia.some(item => item.colorId)) return productMedia
  return productMedia.filter(item => item.colorId === selectedColor.value)
})
const outfitMedia = computed(() => {
  const media = product.value?.media || []
  return media.filter((item, index) => (item.kind ? item.kind === 'outfit_concept' : index > 0)
    && (!item.colorId || item.colorId === selectedColor.value))
})
const activeMedia = computed(() => galleryMedia.value[activeMediaIndex.value] || galleryMedia.value[0])
let galleryGesture: { pointerId: number; x: number; y: number } | undefined
watch(product, value => {
  activeMediaIndex.value = 0
  if (!selectedColor.value && value?.skus.length) selectedColor.value = value.skus[0]?.colorId || ''
}, { immediate: true })
watch(selectedColor, () => { activeMediaIndex.value = 0 })
function moveMedia(direction: -1 | 1) {
  const count = galleryMedia.value.length
  if (count > 1) activeMediaIndex.value = (activeMediaIndex.value + direction + count) % count
}
function startGalleryGesture(event: PointerEvent) {
  if (event.pointerType === 'mouse') return
  galleryGesture = { pointerId: event.pointerId, x: event.clientX, y: event.clientY }
}
function finishGalleryGesture(event: PointerEvent) {
  const start = galleryGesture
  galleryGesture = undefined
  if (!start || start.pointerId !== event.pointerId) return
  const deltaX = event.clientX - start.x
  const deltaY = event.clientY - start.y
  if (Math.abs(deltaX) < 48 || Math.abs(deltaX) < Math.abs(deltaY) * 1.2) return
  moveMedia(deltaX < 0 ? 1 : -1)
}
function handleGalleryKeydown(event: KeyboardEvent) {
  if (event.target !== event.currentTarget) return
  if (event.key === 'ArrowLeft') { event.preventDefault(); moveMedia(-1) }
  if (event.key === 'ArrowRight') { event.preventDefault(); moveMedia(1) }
}
const { cart, loading: cartLoading, pending, error: cartError, load: loadCart, setQuantity } = useCommerceCart()
const colorOptions = computed(() => [...new Set(product.value?.skus.map(item => item.colorId) || [])])
const sizeOptions = computed(() => {
  const order = ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL']
  return [...new Set(product.value?.skus.filter(item => !selectedColor.value || item.colorId === selectedColor.value).map(item => item.garmentSize) || [])]
    .sort((left, right) => {
      const leftIndex = order.indexOf(left), rightIndex = order.indexOf(right)
      if (leftIndex < 0 && rightIndex < 0) return left.localeCompare(right)
      if (leftIndex < 0) return 1
      if (rightIndex < 0) return -1
      return leftIndex - rightIndex
    })
})
const selectedSku = computed(() => product.value?.skus.find(item => item.colorId === selectedColor.value && item.garmentSize === selectedSize.value))
const labels = computed(() => locale.value === 'zh-TW'
  ? { back: '返回商品目錄', notFound: '找不到已公開的商品。', price: '商品售價', material: '材質估算', size: '服裝尺寸', color: '顏色', productImages: '商品單品圖', noProductImage: '這個顏色的單品圖尚待補上。', outfitHeading: '穿搭示意', noOutfit: '這個顏色的穿搭示意尚待補上。', outfitNumber: '穿搭示意', choose: '請選擇', selectVariant: '請先選擇顏色與尺寸，再加入購物車。', productNumber: '商品編號', stock: '可售庫存', inStock: '有庫存', unavailable: '缺貨', add: '加入購物車', viewCart: '查看購物車', added: '已加入購物車。', localeFallback: '部分內容以繁體中文顯示。', chart: '衣服尺寸表（估算）', note: '商品資訊與價格依系統目前資料顯示；材質比例與衣服尺寸為估算值，並非實測。尺寸單位：公分。' }
  : locale.value === 'ja'
    ? { back: '商品一覧へ', notFound: '公開中の商品が見つかりません。', price: '商品価格', material: '素材情報', size: '服のサイズ', color: '色', productImages: '商品単体画像', noProductImage: 'このカラーの商品画像は準備中です。', outfitHeading: 'コーディネートイメージ', noOutfit: 'このカラーの着用イメージは準備中です。', outfitNumber: 'コーディネート例', choose: '選択してください', selectVariant: 'カラーとサイズを選択してから、カートに追加してください。', productNumber: '商品番号', stock: '販売可能在庫', inStock: '在庫あり', unavailable: '在庫切れ', add: 'カートに追加', viewCart: 'カートを見る', added: 'カートに追加しました。', localeFallback: '一部の内容は繁体字中国語で表示されます。', chart: '衣服サイズ表（推定）', note: '商品情報と価格はシステムの現在の記録に基づいて表示します。素材比率と服のサイズは推定値で、実測値ではありません。単位：cm。' }
    : { back: 'Back to catalog', notFound: 'Published product not found.', price: 'Price', material: 'Material information', size: 'Garment size', color: 'Color', productImages: 'Product-only images', noProductImage: 'Product imagery for this color is being prepared.', outfitHeading: 'Outfit inspiration', noOutfit: 'Outfit inspiration for this color is being prepared.', outfitNumber: 'Outfit idea', choose: 'Select one', selectVariant: 'Choose a color and size before adding this item to your cart.', productNumber: 'Product no.', stock: 'Sellable stock', inStock: 'In stock', unavailable: 'Out of stock', add: 'Add to cart', viewCart: 'View cart', added: 'Added to cart.', localeFallback: 'Some content is shown in Traditional Chinese.', chart: 'Garment size chart (estimated)', note: 'Product information and prices reflect the current system records. Material composition and garment measurements are estimates, not measured values. Units: cm.' })
const dimensionOrder = ['chest-width', 'waist-width', 'hip-width', 'shoulder-width', 'body-length', 'sleeve-length', 'skirt-length', 'inseam-length', 'rise']
const dimensionNames: Record<string, Record<Locale, string>> = {
  'chest-width': { 'zh-TW': '胸寬', ja: '身幅', en: 'Chest width' },
  'waist-width': { 'zh-TW': '腰寬', ja: 'ウエスト幅', en: 'Waist width' },
  'hip-width': { 'zh-TW': '臀寬', ja: 'ヒップ幅', en: 'Hip width' },
  'shoulder-width': { 'zh-TW': '肩寬', ja: '肩幅', en: 'Shoulder width' },
  'body-length': { 'zh-TW': '衣長', ja: '着丈', en: 'Body length' },
  'sleeve-length': { 'zh-TW': '袖長', ja: '袖丈', en: 'Sleeve length' },
  'skirt-length': { 'zh-TW': '裙長', ja: 'スカート丈', en: 'Skirt length' },
  'inseam-length': { 'zh-TW': '內側褲長', ja: '股下', en: 'Inseam' },
  rise: { 'zh-TW': '褲襠長', ja: '股上', en: 'Rise' },
}
const measurementColumns = computed(() => dimensionOrder.filter(name => product.value?.measurements.some(item => item.name.endsWith(`-${name}`))))
const measurementRows = computed(() => {
  const rows = new Map<string, Record<string, number>>()
  for (const item of product.value?.measurements || []) {
    const separator = item.name.indexOf('-')
    if (separator < 1) continue
    const size = item.name.slice(0, separator), dimension = item.name.slice(separator + 1)
    const row = rows.get(size) || {}
    row[dimension] = item.valueMm / 10
    rows.set(size, row)
  }
  return ['S', 'M', 'L'].filter(size => rows.has(size)).map(size => ({ size, values: rows.get(size)! }))
})
const priceRange = computed(() => {
  const prices = product.value?.skus.map(item => item.priceTwd) || []
  if (!prices.length) return ''
  const min = Math.min(...prices), max = Math.max(...prices)
  return min === max ? `TWD ${min}` : `TWD ${min}–${max}`
})
const colorNames: Record<string, Record<Locale, string>> = {
  oatmeal: { 'zh-TW': '燕麥奶油', ja: 'オートミール', en: 'Oatmeal' }, dusty_rose: { 'zh-TW': '霧玫瑰', ja: 'ダスティローズ', en: 'Dusty rose' },
  burgundy: { 'zh-TW': '酒紅', ja: 'バーガンディ', en: 'Burgundy' }, slate_navy: { 'zh-TW': '石板藍', ja: 'スレートネイビー', en: 'Slate navy' },
  sand: { 'zh-TW': '沙色', ja: 'サンド', en: 'Sand' }, muted_olive: { 'zh-TW': '柔和橄欖', ja: 'オリーブ', en: 'Muted olive' },
  charcoal: { 'zh-TW': '炭灰', ja: 'チャコール', en: 'Charcoal' }, warm_beige: { 'zh-TW': '暖米色', ja: 'ウォームベージュ', en: 'Warm beige' },
  warm_gray: { 'zh-TW': '暖灰', ja: 'ウォームグレー', en: 'Warm gray' }, powder_blue: { 'zh-TW': '粉霧藍', ja: 'パウダーブルー', en: 'Powder blue' },
  indigo: { 'zh-TW': '靛藍', ja: 'インディゴ', en: 'Indigo' }, muted_sage: { 'zh-TW': '鼠尾草綠', ja: 'セージ', en: 'Muted sage' },
  clay_rose: { 'zh-TW': '陶土玫瑰', ja: 'クレイローズ', en: 'Clay rose' }, deep_taupe: { 'zh-TW': '深灰褐', ja: 'ディープトープ', en: 'Deep taupe' },
  cream: { 'zh-TW': '奶油白', ja: 'クリーム', en: 'Cream' },
}
function colorName(color: string) { return colorNames[color]?.[locale.value] || color }
function swatchColor(color: string) {
  const swatches: Record<string, string> = {
    oatmeal: '#d9c9ad', dusty_rose: '#c98f91', burgundy: '#743346', slate_navy: '#4c596b', sand: '#cbb79a',
    muted_olive: '#777b5e', charcoal: '#494746', warm_beige: '#c8b79e', warm_gray: '#aaa39c', powder_blue: '#b8cbd4',
    indigo: '#4d5d82', muted_sage: '#a4ad9a', clay_rose: '#b77f79', deep_taupe: '#77665a', cream: '#eee7d9',
  }
  return swatches[color] || '#b8afa4'
}
watch(selectedColor, (color, previous) => {
  if (previous) selectedSize.value = ''
  cartSuccess.value = false
})
watch([selectedColor, selectedSize], ([color, size]) => {
  const query = { ...route.query }
  delete query.lang
  if (color) query.color = color
  else delete query.color
  if (size) query.size = size
  else delete query.size
  void router.replace({ path: route.path, query })
})
watch(product, value => {
  if (selectedColor.value && !value?.skus.some(item => item.colorId === selectedColor.value)) selectedColor.value = ''
  if (selectedSize.value && !value?.skus.some(item => item.colorId === selectedColor.value && item.garmentSize === selectedSize.value)) selectedSize.value = ''
}, { immediate: true })
async function addSelectedSku() {
  const sku = selectedSku.value
  if (!sku || sku.availability !== 'InStock' || pending.value) return
  cartSuccess.value = false
  if (!cart.value) await loadCart()
  if (!cart.value) return
  const current = cart.value
  if (!current) return
  const quantity = current.items.find(item => item.skuId === sku.skuId)?.quantity || 0
  await setQuantity(sku.skuId, quantity + 1)
  cartSuccess.value = !cartError.value
    && cart.value?.version !== current.version
    && cart.value?.items.find(item => item.skuId === sku.skuId)?.quantity === quantity + 1
}
function cartErrorMessage() {
  const messages: Record<string, string> = locale.value === 'zh-TW'
    ? { PRODUCT_UNAVAILABLE: '商品目前缺貨或已停售，請重新選擇。', VERSION_CHANGED: '購物車已更新，請確認後再操作。', STOCK_CHANGED: '可售庫存已變更，請重新確認數量。', AUTHENTICATION_REQUIRED: '目前無法建立購物車，請重新載入。', SERVICE_UNAVAILABLE: '目前無法連線至本機服務，請稍後重試。' }
    : locale.value === 'ja'
      ? { PRODUCT_UNAVAILABLE: '商品は在庫切れ、または販売終了です。もう一度選択してください。', VERSION_CHANGED: 'カートが更新されました。確認してから操作してください。', STOCK_CHANGED: '販売可能在庫が変更されました。数量を確認してください。', AUTHENTICATION_REQUIRED: 'カートを作成できません。再読み込みしてください。', SERVICE_UNAVAILABLE: 'ローカルサービスに接続できません。しばらくしてから再試行してください。' }
      : { PRODUCT_UNAVAILABLE: 'This product is out of stock or unavailable. Choose another option.', VERSION_CHANGED: 'The cart changed. Review it before trying again.', STOCK_CHANGED: 'Available stock changed. Review the quantity.', AUTHENTICATION_REQUIRED: 'A cart could not be created. Reload the page.', SERVICE_UNAVAILABLE: 'The local service is unavailable. Try again.' }
  return messages[cartError.value?.code || ''] || messages.SERVICE_UNAVAILABLE
}
onMounted(() => { void loadCart() })
useLocalizedPageHead({ title: computed(() => translation.value?.name || 'KOHAMI 商品'), path: computed(() => route.path), locale, publicPage: computed(() => Boolean(product.value && translation.value)) })
</script>

<template>
  <main id="main-content" class="product-page" tabindex="-1">
    <header class="product-header">
      <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`"><PhArrowLeft :size="16" weight="regular" aria-hidden="true" />{{ labels.back }}</NuxtLink>
    </header>
    <p v-if="error">{{ labels.notFound }}</p>
    <article v-else-if="product && translation">
      <div class="product-layout">
        <div class="product-gallery">
          <p class="product-gallery__color">{{ labels.productImages }} · {{ colorName(selectedColor) }}</p>
          <div class="product-gallery__stage" role="group" :aria-label="`${labels.productImages} · ${colorName(selectedColor)}`" tabindex="0" @pointerdown="startGalleryGesture" @pointerup="finishGalleryGesture" @pointercancel="galleryGesture = undefined" @keydown="handleGalleryKeydown">
            <img v-if="activeMedia" class="product-gallery__main" :src="activeMedia.media.url" :alt="localizedProductMediaAlt(activeMedia.media.alt.value, translation.name, locale, activeMedia.colorId)" fetchpriority="high">
            <p v-else class="product-gallery__empty" role="status">{{ labels.noProductImage }}</p>
            <div v-if="galleryMedia.length > 1" class="product-gallery__controls" role="group" :aria-label="locale === 'zh-TW' ? '切換商品圖片' : locale === 'ja' ? '商品画像を切り替える' : 'Browse product images'">
              <button type="button" :aria-label="locale === 'zh-TW' ? '上一張圖片' : locale === 'ja' ? '前の画像' : 'Previous image'" @click="moveMedia(-1)"><PhCaretLeft :size="18" weight="regular" aria-hidden="true" /></button>
              <span aria-live="polite">{{ activeMediaIndex + 1 }} / {{ galleryMedia.length }}</span>
              <button type="button" :aria-label="locale === 'zh-TW' ? '下一張圖片' : locale === 'ja' ? '次の画像' : 'Next image'" @click="moveMedia(1)"><PhCaretRight :size="18" weight="regular" aria-hidden="true" /></button>
            </div>
          </div>
          <div v-if="galleryMedia.length > 1" class="product-gallery__thumbs" role="group" :aria-label="locale === 'zh-TW' ? '商品圖片' : locale === 'ja' ? '商品画像' : 'Product images'">
            <button v-for="(item, index) in galleryMedia" :key="item.media.url" type="button" :aria-pressed="activeMediaIndex === index" :aria-label="`${item.media.alt.value} · ${index + 1}/${galleryMedia.length}`" @click="activeMediaIndex = index">
              <img :src="item.media.url" alt="" aria-hidden="true" width="100" height="100" loading="lazy">
            </button>
          </div>
          <section class="outfit-gallery" aria-labelledby="outfit-heading">
            <div class="outfit-gallery__heading">
              <h2 id="outfit-heading">{{ labels.outfitHeading }} · {{ colorName(selectedColor) }}</h2>
            </div>
            <div v-if="outfitMedia.length" class="outfit-gallery__grid">
              <figure v-for="(item, index) in outfitMedia" :key="item.media.url">
                <img :src="item.media.url" :alt="localizedProductMediaAlt(item.media.alt.value, translation.name, locale, item.colorId)" width="480" height="600" loading="lazy">
                <figcaption>{{ labels.outfitNumber }} {{ index + 1 }}</figcaption>
              </figure>
            </div>
            <p v-else class="product-gallery__pending" role="status">{{ labels.noOutfit }}</p>
          </section>
        </div>
        <div class="product-info">
          <p class="eyebrow">KOHAMI · {{ labels.price }}</p>
          <h1>{{ translation.name }}</h1>
          <p v-if="translationFallback" class="locale-fallback" role="note">{{ labels.localeFallback }}</p>
          <p class="price">{{ priceRange }}</p>
          <p>{{ translation.description }}</p>
          <section class="sku-selection" aria-labelledby="selection-heading">
            <h2 id="selection-heading">{{ locale === 'zh-TW' ? '選擇商品規格' : locale === 'ja' ? '商品を選択' : 'Choose product options' }}</h2>
            <fieldset class="color-options" :disabled="pending">
              <legend>{{ labels.color }}</legend>
              <button v-for="color in colorOptions" :key="color" type="button" class="color-option" :class="{ 'is-selected': selectedColor === color }" :aria-label="colorName(color)" :aria-pressed="selectedColor === color" @click="selectedColor = color">
                <span class="color-option__swatch" :style="{ '--swatch-color': swatchColor(color) }" aria-hidden="true" />
                <span>{{ colorName(color) }}</span>
              </button>
            </fieldset>
            <label>{{ labels.size }}
              <select v-model="selectedSize" :aria-label="labels.size" :disabled="!selectedColor">
                <option value="">{{ labels.choose }}</option>
                <option v-for="size in sizeOptions" :key="size" :value="size" :disabled="!product?.skus.some(item => item.colorId === selectedColor && item.garmentSize === size && item.availability === 'InStock')">{{ size }}</option>
              </select>
            </label>
            <p v-if="selectedSku" class="selected-sku">{{ labels.productNumber }}：{{ formatProductNumber(selectedSku.skuId) }} · {{ colorName(selectedSku.colorId) }} · {{ labels.size }} {{ selectedSku.garmentSize }} · {{ labels.stock }} {{ selectedSku.stock }} · TWD {{ selectedSku.priceTwd }}</p>
            <p v-else class="selection-hint">{{ labels.selectVariant }}</p>
            <p v-if="cartLoading" role="status">{{ locale === 'zh-TW' ? '載入購物車…' : locale === 'ja' ? 'カートを読み込み中…' : 'Loading cart…' }}</p>
            <p v-if="cartError" class="error" role="alert">{{ cartErrorMessage() }}</p>
            <button type="button" :disabled="pending || cartLoading || !cart || !selectedSku || selectedSku.availability !== 'InStock'" :aria-busy="pending" @click="addSelectedSku">{{ labels.add }}</button>
            <p v-if="cartSuccess" class="cart-confirmation" role="status" aria-live="polite">{{ labels.added }} <NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.viewCart }}</NuxtLink></p>
            <p v-else><NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.viewCart }}</NuxtLink></p>
          </section>
          <h2>{{ labels.material }}</h2>
          <p>{{ translation.material }}</p>
        </div>
      </div>
      <section class="size-chart" aria-labelledby="size-heading">
        <h2 id="size-heading">{{ labels.chart }}</h2>
        <div class="table-wrap">
          <table>
            <thead><tr><th scope="col">{{ labels.size }}</th><th v-for="column in measurementColumns" :key="column" scope="col">{{ dimensionNames[column]?.[locale] || column }}</th></tr></thead>
            <tbody><tr v-for="row in measurementRows" :key="row.size"><th scope="row">{{ row.size }}</th><td v-for="column in measurementColumns" :key="column">{{ row.values[column]?.toFixed(1) ?? '—' }}</td></tr></tbody>
          </table>
        </div>
        <p class="synthetic-note">{{ labels.note }}</p>
      </section>
    </article>
  </main>
</template>

<style scoped>
.product-page { width: min(100% - 2rem, 78rem); box-sizing: border-box; margin: 1.5rem auto 3rem; padding: 0; color: var(--store-ink); }
.product-header { display: flex; align-items: center; min-height: 2.5rem; margin-bottom: 1.25rem; }
.product-header > a { display: inline-flex; min-block-size: 2.5rem; align-items: center; gap: .4rem; color: var(--store-muted); font-size: .82rem; text-decoration: none; }
.product-header > a:hover { color: var(--store-ink); text-decoration: underline; text-underline-offset: .25rem; }
.product-layout { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(19rem, .8fr); align-items: start; gap: clamp(2rem, 6vw, 5rem); margin-top: 1rem; }
.product-gallery { min-width: 0; }
.product-gallery__color { margin: 0 0 .55rem; color: var(--store-muted-strong); font-size: .82rem; letter-spacing: .04em; }
.product-gallery__stage { position: relative; overflow: hidden; background: var(--store-linen); touch-action: pan-y; }
.product-gallery__stage:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.product-gallery__main { display: block; width: 100%; aspect-ratio: 4 / 5; object-fit: contain; background: var(--store-linen); }
.product-gallery__empty { display: grid; min-height: 20rem; margin: 0; padding: 2rem; place-items: center; color: var(--store-muted-strong); text-align: center; }
.product-gallery__controls { position: absolute; inset-inline: .75rem; inset-block-end: .75rem; display: flex; justify-content: flex-end; align-items: center; gap: .4rem; pointer-events: none; }
.product-gallery__controls button { display: grid; width: 2.75rem; height: 2.75rem; place-items: center; padding: 0; border: 1px solid var(--store-border); border-radius: 50%; background: rgb(255 255 255 / 92%); color: var(--store-ink); font: inherit; font-size: 1.35rem; cursor: pointer; pointer-events: auto; }
.product-gallery__controls button:hover, .product-gallery__controls button:focus-visible { background: var(--store-accent-strong); color: white; }
.product-gallery__controls span { padding: .4rem .6rem; background: rgb(255 255 255 / 92%); color: var(--store-ink); font-size: .72rem; font-variant-numeric: tabular-nums; }
.product-gallery__thumbs { display: flex; gap: .65rem; overflow-x: auto; padding-top: .75rem; }
.product-gallery__thumbs button { flex: 0 0 5rem; padding: 0; border: 1px solid var(--store-border); background: white; cursor: pointer; scroll-snap-align: start; }
.product-gallery__thumbs button[aria-pressed="true"] { border: 2px solid var(--store-accent-strong); }
.product-gallery__thumbs img { display: block; width: 100%; aspect-ratio: 1; object-fit: cover; }
.product-gallery__pending { margin: .6rem 0; color: var(--store-muted-strong); font-size: .82rem; }
.outfit-gallery { margin-top: 1.5rem; padding-top: 1.15rem; border-top: 1px solid var(--store-border-subtle); }
.outfit-gallery__heading h2 { margin: 0; font-family: var(--store-font-display); font-size: 1.2rem; font-weight: 500; }
.outfit-gallery__heading p { margin: .45rem 0 .8rem; color: var(--store-muted-strong); font-size: .78rem; line-height: 1.55; }
.outfit-gallery__grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 9rem), 1fr)); gap: .7rem; }
.outfit-gallery figure { min-width: 0; margin: 0; background: var(--store-paper); }
.outfit-gallery img { display: block; width: 100%; aspect-ratio: 4 / 5; object-fit: cover; }
.outfit-gallery figcaption { padding: .5rem .6rem; color: var(--store-muted-strong); font-size: .75rem; }
.product-info { padding: .5rem 0 1rem; }
.product-info h1 { margin: .65rem 0 1rem; color: var(--store-ink); font-family: var(--store-font-display); font-size: clamp(1.8rem, 3vw, 2.5rem); font-weight: 500; line-height: 1.3; }
.product-info h2, .size-chart h2 { margin-top: 1.6rem; font-size: 1.08rem; font-weight: 500; }
.sku-selection { margin-block: 1.5rem; padding: 1.15rem 1.25rem; border: 1px solid var(--store-border-subtle); background: var(--store-paper); }
.cart-confirmation { display: flex; align-items: center; gap: .5rem; color: #31543a; font-weight: 600; }
.cart-confirmation a { color: inherit; text-underline-offset: .2em; }
.color-options { display: flex; flex-wrap: wrap; gap: .5rem; margin: .85rem 0; padding: 0; border: 0; }
.color-options legend { width: 100%; margin-bottom: .35rem; color: var(--store-muted-strong); font-size: .88rem; }
.color-option { display: inline-flex; min-height: 2.75rem; align-items: center; gap: .5rem; padding: .35rem .65rem; border: 1px solid var(--store-border); border-radius: 999px; background: var(--store-surface); color: var(--store-ink); font: inherit; font-size: .82rem; cursor: pointer; }
.color-option.is-selected { border-color: var(--store-accent-strong); box-shadow: 0 0 0 1px var(--store-accent-strong); }
.color-option:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.color-option:disabled { cursor: not-allowed; opacity: .6; }
.color-option__swatch { width: 1.05rem; height: 1.05rem; border: 1px solid rgb(0 0 0 / 14%); border-radius: 50%; background: var(--swatch-color); }
.sku-selection > button { width: 100%; min-height: 3rem; margin-top: .65rem; padding: .7rem 1rem; background: var(--store-accent-strong); color: #fff; border: 1px solid var(--store-accent-strong); border-radius: var(--store-radius-sm); font: inherit; font-size: .9rem; letter-spacing: .04em; cursor: pointer; }
.sku-selection > button:hover:not(:disabled) { background: var(--store-accent-hover); border-color: var(--store-accent-hover); }
.sku-selection > button:disabled { background: var(--store-paper); color: var(--store-muted-strong); border-color: var(--store-border); opacity: 1; cursor: not-allowed; }
.sku-selection a { color: var(--store-accent-strong); }
.error { color: var(--store-danger); }
.eyebrow { color: var(--store-accent-strong); font-size: .73rem; letter-spacing: .12em; }
.locale-fallback { color: var(--store-muted-strong); }
.price { margin-block: 1.2rem; font-size: 1.35rem; font-weight: 600; letter-spacing: .03em; }
.selected-sku { padding: .75rem; border-left: 2px solid var(--store-accent); background: var(--store-paper); font-size: .8rem; }
.selection-hint { margin: .6rem 0; color: var(--store-muted-strong); font-size: .8rem; }
.size-chart { margin-top: 2rem; padding: 1.25rem 0; border-top: 1px solid var(--store-border-subtle); background: var(--store-surface); }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; text-align: left; }
th, td { padding: .7rem; border-bottom: 1px solid var(--store-border-subtle); font-size: .9rem; }
.product-page :focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.synthetic-note { margin-top: 1rem; color: var(--store-muted); font-size: .78rem; }
@media (max-width: 42rem) { .product-page { width: calc(100% - 1.5rem); margin-top: .8rem; } .product-layout { grid-template-columns: minmax(0, 1fr); gap: 1rem; } .product-info { padding-top: .25rem; } .product-gallery__main { aspect-ratio: 4 / 4.5; } }
@media (prefers-reduced-motion: reduce) { .product-gallery__controls button { transition: none; } }
</style>
