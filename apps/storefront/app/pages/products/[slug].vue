<script setup lang="ts">
import { localizedPath } from '../../utils/storefront-locale'
import { formatProductNumber } from '../../utils/product-number'
type Locale = 'zh-TW' | 'ja' | 'en'
type Measurement = { name: string; valueMm: number; synthetic: boolean }
type CatalogProduct = {
  id: string
  slug: string
  mode: 'mock'
  translations: Array<{ locale: Locale; name: string; description: string; material: string }>
  media: Array<{ media: { url: string; alt: { value: string } } }>
  measurements: Measurement[]
  skus: Array<{ skuId: string; priceTwd: number; colorId: string; garmentSize: string; stock: number; availability: string }>
}
const route = useRoute()
const router = useRouter()
const requestFetch = useRequestFetch()
const { locale } = useStorefrontLocale()
const slug = computed(() => String(route.params.slug || ''))
const { data: product, error } = await useAsyncData(`catalog-${slug.value}`, () => requestFetch<CatalogProduct>(`/api/v1/products/${encodeURIComponent(slug.value)}`))
const translation = computed(() => product.value?.translations.find(item => item.locale === locale.value) || product.value?.translations.find(item => item.locale === 'zh-TW'))
const translationFallback = computed(() => Boolean(product.value
  && !product.value.translations.some(item => item.locale === locale.value)
  && product.value.translations.some(item => item.locale === 'zh-TW')))
const selectedColor = ref(String(route.query.color || ''))
const selectedSize = ref(String(route.query.size || ''))
const cartSuccess = ref(false)
const { cart, loading: cartLoading, pending, error: cartError, load: loadCart, setQuantity } = useCommerceCart()
const colorOptions = computed(() => [...new Set(product.value?.skus.map(item => item.colorId) || [])].sort())
const sizeOptions = computed(() => [...new Set(product.value?.skus.filter(item => !selectedColor.value || item.colorId === selectedColor.value).map(item => item.garmentSize) || [])].sort())
const selectedSku = computed(() => product.value?.skus.find(item => item.colorId === selectedColor.value && item.garmentSize === selectedSize.value))
const labels = computed(() => locale.value === 'zh-TW'
  ? { back: '← 返回商品目錄', notFound: '找不到已公開的商品。', price: '示意售價', material: '合成材質估算', size: '服裝尺寸', color: '顏色', choose: '請選擇', productNumber: '商品編號', stock: '可售庫存', inStock: '有庫存', unavailable: '缺貨', add: '加入購物車', viewCart: '查看購物車', added: '已加入購物車。', localeFallback: '部分內容以繁體中文顯示。', chart: '衣服尺寸表（估算）', note: '本頁為展示網站的合成商品。材質比例、衣服尺寸與新台幣價格皆為估算值，非實際商品資料或實測；圖片由本地生成模型製作。尺寸單位：公分。' }
  : locale.value === 'ja'
    ? { back: '← 商品一覧へ', notFound: '公開中の商品が見つかりません。', price: '表示価格', material: '合成素材の推定', size: '服のサイズ', color: '色', choose: '選択してください', productNumber: '商品番号', stock: '販売可能在庫', inStock: '在庫あり', unavailable: '在庫切れ', add: 'カートに追加', viewCart: 'カートを見る', added: 'カートに追加しました。', localeFallback: '一部の内容は繁体字中国語で表示されます。', chart: '衣服サイズ表（推定）', note: '展示サイト用の合成商品です。素材比率、衣服寸法、台湾ドル価格は推定値で、実物や実測値ではありません。画像はローカル生成モデルで作成しました。単位：cm。' }
    : { back: '← Back to catalog', notFound: 'Published product not found.', price: 'Display price', material: 'Estimated synthetic material', size: 'Garment size', color: 'Color', choose: 'Select one', productNumber: 'Product no.', stock: 'Sellable stock', inStock: 'In stock', unavailable: 'Out of stock', add: 'Add to cart', viewCart: 'View cart', added: 'Added to cart.', localeFallback: 'Some content is shown in Traditional Chinese.', chart: 'Garment size chart (estimated)', note: 'Synthetic product for a demo website. Material composition, garment measurements and TWD prices are estimates, not real product data or physical measurements. Images were created with a local generation model. Units: cm.' })
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
}
function colorName(color: string) { return colorNames[color]?.[locale.value] || color }
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
  const quantity = cart.value?.items.find(item => item.skuId === sku.skuId)?.quantity || 0
  await setQuantity(sku.skuId, quantity + 1)
  cartSuccess.value = !cartError.value
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
      <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ labels.back }}</NuxtLink>
    </header>
    <p v-if="error">{{ labels.notFound }}</p>
    <article v-else-if="product && translation">
      <div class="product-layout">
        <img v-if="product.media[0]" :src="product.media[0].media.url" :alt="product.media[0].media.alt.value">
        <div class="product-info">
          <p class="eyebrow">KOHAMI · {{ labels.price }}</p>
          <h1>{{ translation.name }}</h1>
          <p v-if="translationFallback" class="locale-fallback" role="note">{{ labels.localeFallback }}</p>
          <p class="price">{{ priceRange }}</p>
          <p>{{ translation.description }}</p>
          <section class="sku-selection" aria-labelledby="selection-heading">
            <h2 id="selection-heading">{{ locale === 'zh-TW' ? '選擇商品規格' : locale === 'ja' ? '商品を選択' : 'Choose product options' }}</h2>
            <label>{{ labels.color }}
              <select v-model="selectedColor" :aria-label="labels.color" :disabled="pending">
                <option value="">{{ labels.choose }}</option>
                <option v-for="color in colorOptions" :key="color" :value="color">{{ colorName(color) }}</option>
              </select>
            </label>
            <label>{{ labels.size }}
              <select v-model="selectedSize" :aria-label="labels.size" :disabled="!selectedColor">
                <option value="">{{ labels.choose }}</option>
                <option v-for="size in sizeOptions" :key="size" :value="size" :disabled="!product?.skus.some(item => item.colorId === selectedColor && item.garmentSize === size && item.availability === 'InStock')">{{ size }}</option>
              </select>
            </label>
            <p v-if="selectedSku" class="selected-sku">{{ labels.productNumber }}：{{ formatProductNumber(selectedSku.skuId) }} · {{ colorName(selectedSku.colorId) }} · {{ labels.size }} {{ selectedSku.garmentSize }} · {{ labels.stock }} {{ selectedSku.stock }} · TWD {{ selectedSku.priceTwd }}</p>
            <p v-if="cartLoading" role="status">{{ locale === 'zh-TW' ? '載入購物車…' : locale === 'ja' ? 'カートを読み込み中…' : 'Loading cart…' }}</p>
            <p v-if="cartError" class="error" role="alert">{{ cartErrorMessage() }}</p>
            <button type="button" :disabled="pending || cartLoading || !cart || !selectedSku || selectedSku.availability !== 'InStock'" @click="addSelectedSku">{{ labels.add }}</button>
            <p v-if="cartSuccess" role="status">{{ labels.added }} <NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.viewCart }}</NuxtLink></p>
            <p v-else><NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.viewCart }}</NuxtLink></p>
          </section>
          <h2>{{ labels.material }}</h2>
          <p>{{ translation.material }}</p>
        <ul class="sku-list">
          <li v-for="sku in product.skus" :key="sku.skuId">{{ labels.productNumber }}：{{ formatProductNumber(sku.skuId) }} · {{ colorName(sku.colorId) }} · {{ labels.size }} {{ sku.garmentSize }} · {{ labels.stock }} {{ sku.stock }} · {{ sku.availability === 'InStock' ? labels.inStock : labels.unavailable }}</li>
        </ul>
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
.product-page { width: min(100% - 2rem, 78rem); box-sizing: border-box; margin: 1.5rem auto 3rem; padding: 0; color: #332d2b; }
.product-header { display: flex; align-items: center; min-height: 2.5rem; margin-bottom: 1.25rem; }
.product-header > a { display: inline-flex; min-block-size: 2.5rem; align-items: center; color: #756c67; font-size: .82rem; text-decoration: none; }
.product-header > a:hover { color: #332d2b; text-decoration: underline; text-underline-offset: .25rem; }
.product-layout { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(19rem, .8fr); align-items: start; gap: clamp(2rem, 6vw, 5rem); margin-top: 1rem; }
.product-layout > img { display: block; width: 100%; aspect-ratio: 4 / 5; object-fit: contain; background: #f5f2ee; }
.product-info { padding: .5rem 0 1rem; }
.product-info h1 { margin: .65rem 0 1rem; color: #332d2b; font-family: Georgia, 'Times New Roman', serif; font-size: clamp(1.8rem, 3vw, 2.5rem); font-weight: 500; line-height: 1.3; }
.product-info h2, .size-chart h2 { margin-top: 1.6rem; font-size: 1.08rem; font-weight: 500; }
.sku-selection { margin-block: 1.5rem; padding: 1.15rem 0; border-block: 1px solid #e8e2de; background: transparent; }
.sku-selection label { display: grid; gap: .4rem; margin-block: .85rem; color: #625a56; font-size: .82rem; }
.sku-selection select { width: 100%; min-height: 2.9rem; padding: .65rem .7rem; border: 1px solid #ded8d3; border-radius: 0; background: #fff; color: #332d2b; font: inherit; }
.sku-selection button { width: 100%; min-height: 3rem; margin-top: .65rem; padding: .7rem 1rem; background: #332d2b; color: #fff; border: 1px solid #332d2b; border-radius: 0; font: inherit; font-size: .85rem; letter-spacing: .04em; }
.sku-selection button:disabled { background: #e9e5e1; color: #625c58; border-color: #c8c0ba; opacity: 1; cursor: not-allowed; }
.sku-selection a { color: #76555a; }
.error { color: #a11919; }
.eyebrow { color: #8b6e69; font-size: .73rem; letter-spacing: .12em; }
.locale-fallback { color: #625c58; }
.price { margin-block: 1.2rem; font-size: 1.35rem; font-weight: 600; letter-spacing: .03em; }
.selected-sku { padding: .75rem; background: #f8f6f3; font-size: .8rem; }
.sku-list { padding-left: 1.2rem; color: #756c67; font-size: .78rem; }
.sku-list li { margin: .4rem 0; }
.size-chart { margin-top: 2rem; padding: 1.25rem 0; border-top: 1px solid #eee9e4; background: #fff; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; text-align: left; }
th, td { padding: .7rem; border-bottom: 1px solid #e8e2de; font-size: .85rem; }
.synthetic-note { margin-top: 1rem; color: #756c67; font-size: .78rem; }
@media (max-width: 42rem) { .product-page { width: calc(100% - 1.5rem); margin-top: .8rem; } .product-layout { grid-template-columns: minmax(0, 1fr); gap: 1rem; } .product-info { padding-top: .25rem; } .product-layout > img { aspect-ratio: 4 / 4.5; } }
</style>
