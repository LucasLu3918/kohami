<script setup lang="ts">
const $fetch = useStorefrontApi()
import ProductDiscovery from '../components/catalog/ProductDiscovery.vue'
import StorefrontHero from '../components/storefront/StorefrontHero.vue'
import { useCommerceCart } from '../composables/useCommerceCart'
import { useLocalizedPageHead } from '../composables/useLocalizedPageHead'
import type { StorefrontLocale } from '../utils/storefront-locale'

type Product = {
  productId: string
  slug: string
  translations: Array<{ locale: StorefrontLocale; name: string; description?: string }>
  media: Array<{ media: { url: string; alt: { value: string } } }>
  skuId: string
  colorId: string
  garmentSize: string
  priceTwd: number
  availability: string
  mode: 'mock'
}
type SearchFilters = { q: string; colorId: string; garmentSize: string; availability: 'all' | 'in_stock' }

const route = useRoute()
const router = useRouter()
const { locale } = useStorefrontLocale()
const { source, readOnly } = useDataSource()
const pageLimit = readOnly.value ? '100' : '20'
const { cart, pending, error: cartError, load: loadCart, setQuantity } = useCommerceCart()
const cartAdded = ref(false)
const cartAttempted = ref(false)
useLocalizedPageHead({ title: computed(() => locale.value === 'ja' ? 'KOHAMI セレクトショップ' : locale.value === 'en' ? 'KOHAMI Japanese fashion' : 'KOHAMI 日系選品'), path: computed(() => route.path), locale, publicPage: true })

const initialParams = new URLSearchParams({ locale: locale.value, limit: pageLimit, sort: 'name' })
for (const key of ['q', 'colorId', 'garmentSize'] as const) {
  const value = route.query[key]
  if (typeof value === 'string' && value) initialParams.set(key, value)
}
if (route.query.availability === 'in_stock') initialParams.set('availability', 'in_stock')
const requestFetch = useStorefrontApi()
const { data: initialCatalog, error: initialCatalogError } = await useAsyncData(
  `initial-catalog-${source.value}-${route.fullPath}`,
  () => requestFetch<{ items: Product[]; nextCursor: string | null }>(`/api/v1/products?${initialParams.toString()}`),
)
const catalog = ref<Product[]>(initialCatalog.value?.items ?? [])
const catalogLoading = ref(false)
const catalogError = ref(Boolean(initialCatalogError.value))
const catalogNextCursor = ref<string | null>(initialCatalog.value?.nextCursor ?? null)
const filters = reactive<SearchFilters>({
  q: String(route.query.q || ''),
  colorId: String(route.query.colorId || ''),
  garmentSize: String(route.query.garmentSize || ''),
  availability: route.query.availability === 'in_stock' ? 'in_stock' : 'all',
})
let requestId = 0
const labels = computed(() => locale.value === 'ja'
  ? { kicker: 'KOHAMI SELECT', heading: '新着アイテム', note: '毎日に取り入れやすい、やわらかな着こなしを集めました。', search: '商品名、説明を検索', searchAction: '商品を検索', color: '色', size: 'サイズ', variant: '色・サイズ', any: '指定なし', stock: '在庫', allStock: 'すべて', inStock: '在庫ありのみ', reset: '条件をクリア', view: '商品とサイズ表を見る', add: 'カートに追加', unavailable: '在庫切れ', fallback: '一部の内容は繁体字中国語で表示されます。', selectVariant: 'カートに追加する前に色とサイズを選択してください。', added: 'カートに追加しました。', viewCart: 'カートを見る', cartFailure: 'カートに追加できません。接続を確認して再試行してください。', noResults: '該当する商品がありません。検索条件を変更してください。', loadFailure: '商品を読み込めません。', loading: '商品を読み込み中…', more: 'さらに表示' }
  : locale.value === 'en'
    ? { kicker: 'KOHAMI EDIT', heading: 'New arrivals', note: 'Easy pieces and soft layers for your everyday wardrobe.', search: 'Search product name or description', searchAction: 'Search products', color: 'Color', size: 'Size', variant: 'Color and size', any: 'Any', stock: 'Stock', allStock: 'All items', inStock: 'In stock only', reset: 'Clear filters', view: 'View product and size chart', add: 'Add to cart', unavailable: 'Out of stock', fallback: 'Some content is shown in Traditional Chinese.', selectVariant: 'Choose a color and size before adding this item to your cart.', added: 'Added to cart.', viewCart: 'View cart', cartFailure: 'Could not add this item. Check your connection and try again.', noResults: 'No products match these filters. Try changing your search.', loadFailure: 'Products could not be loaded.', loading: 'Loading products…', more: 'Load more' }
    : { kicker: 'KOHAMI 衣櫥選品', heading: '本週新品', note: '為每天的穿搭，挑一些柔和又自在的選擇。價格與可售狀態以商品服務資料為準。', search: '搜尋商品名稱或商品描述', searchAction: '搜尋商品', color: '顏色', size: '尺寸', variant: '顏色與尺寸', any: '不限', stock: '庫存', allStock: '全部狀態', inStock: '只看有庫存', reset: '清除篩選', view: '查看商品與尺寸表', add: '加入購物車', unavailable: '缺貨', fallback: '部分內容以繁體中文顯示。', selectVariant: '加入購物車前，請先選擇顏色與尺寸。', added: '已加入購物車。', viewCart: '查看購物車', cartFailure: '目前無法加入購物車，請檢查連線後再試。', noResults: '找不到符合條件的商品，請調整搜尋或篩選。', loadFailure: '目前無法載入商品。', loading: '正在載入商品…', more: '載入更多' })

async function loadCatalog(append = false) {
  const currentRequest = ++requestId
  catalogLoading.value = true
  catalogError.value = false
  const params = new URLSearchParams({ locale: locale.value, limit: pageLimit, sort: 'name' })
  if (filters.q) params.set('q', filters.q)
  if (filters.colorId) params.set('colorId', filters.colorId)
  if (filters.garmentSize) params.set('garmentSize', filters.garmentSize)
  if (filters.availability !== 'all') params.set('availability', filters.availability)
  if (append && catalogNextCursor.value) params.set('cursor', catalogNextCursor.value)
  try {
    const result = await $fetch<{ items: Product[]; nextCursor: string | null }>(`/api/v1/products?${params}`)
    if (currentRequest === requestId) {
      catalog.value = append ? [...catalog.value, ...result.items] : result.items
      catalogNextCursor.value = result.nextCursor
    }
  } catch {
    if (currentRequest === requestId) catalogError.value = true
  } finally {
    if (currentRequest === requestId) catalogLoading.value = false
  }
}

async function searchCatalog(value: SearchFilters) {
  Object.assign(filters, value)
  catalogNextCursor.value = null
  const query = { ...route.query }
  delete query.q
  delete query.colorId
  delete query.garmentSize
  delete query.availability
  if (value.q) query.q = value.q
  if (value.colorId) query.colorId = value.colorId
  if (value.garmentSize) query.garmentSize = value.garmentSize
  if (value.availability !== 'all') query.availability = value.availability
  void router.replace({ path: route.path, query })
  await loadCatalog()
}

watch([() => route.query.q, () => route.query.colorId, () => route.query.garmentSize, () => route.query.availability], ([q, colorId, garmentSize, availability]) => {
  const next: SearchFilters = {
    q: String(q || ''),
    colorId: String(colorId || ''),
    garmentSize: String(garmentSize || ''),
    availability: availability === 'in_stock' ? 'in_stock' : 'all',
  }
  if (next.q === filters.q && next.colorId === filters.colorId && next.garmentSize === filters.garmentSize && next.availability === filters.availability) return
  Object.assign(filters, next)
  catalogNextCursor.value = null
  void loadCatalog()
})

async function addCatalogSku(skuId: string) {
  if (pending.value) return
  cartAdded.value = false
  cartAttempted.value = true
  if (!cart.value) await loadCart()
  const current = cart.value
  if (!current) return
  const currentQuantity = current.items.find(item => item.skuId === skuId)?.quantity ?? 0
  await setQuantity(skuId, currentQuantity + 1)
  const updated = cart.value
  cartAdded.value = !cartError.value
    && updated?.version !== current.version
    && updated?.items.find(item => item.skuId === skuId)?.quantity === currentQuantity + 1
}

onMounted(() => { void loadCart() })
watch(locale, () => { void loadCatalog() })
</script>

<template>
  <main id="main-content" class="storefront-home" tabindex="-1">
    <StorefrontHero :locale="locale" />
    <ProductDiscovery
      id="catalog"
      :items="catalog"
      :locale="locale"
      :loading="catalogLoading"
      :error="catalogError"
      :next-cursor="catalogNextCursor"
      :cart-ready="Boolean(cart)"
      :pending="pending"
      :labels="labels"
      @search="searchCatalog"
      @retry="loadCatalog()"
      @load-more="loadCatalog(true)"
      @add-to-cart="addCatalogSku"
    />
    <p v-if="cartAdded" class="cart-added" role="status" aria-live="polite">
      {{ labels.added }} <NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.viewCart }}</NuxtLink>
    </p>
    <p v-else-if="cartAttempted && cartError" class="cart-added cart-added--error" role="alert">{{ labels.cartFailure }}</p>
  </main>
</template>

<style scoped>
.storefront-home { min-height: 65vh; }
.cart-added { position: fixed; z-index: 20; inset-inline-end: 1rem; inset-block-end: 1rem; max-width: min(28rem, calc(100vw - 2rem)); margin: 0; padding: .9rem 1.1rem; border: 1px solid #d5e3d1; border-radius: .5rem; background: #f2f7f0; color: #263a28; box-shadow: 0 .4rem 1.5rem rgb(39 35 34 / 14%); animation: cart-added-enter .2s ease-out both; }
.cart-added a { margin-inline-start: .45rem; color: inherit; font-weight: 700; text-underline-offset: .2em; }
.cart-added--error { border-color: #e7c9c5; background: #fff5f3; color: #792a23; }
@keyframes cart-added-enter { from { opacity: 0; transform: translateY(.4rem); } to { opacity: 1; transform: translateY(0); } }
@media (prefers-reduced-motion: reduce) { .cart-added { animation: none; } }
</style>
