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
const { cart, pending, load: loadCart, setQuantity } = useCommerceCart()
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
  ? { heading: '新着アイテム', note: '毎日に取り入れやすい、デイリーな着こなしをご提案します。', search: '商品名、説明を検索', searchAction: '商品を検索', color: '色', size: 'サイズ', variant: '色・サイズ', any: '指定なし', stock: '在庫', allStock: 'すべて', inStock: '在庫ありのみ', reset: '条件をクリア', view: '商品とサイズ表を見る', add: 'カートに追加', unavailable: '在庫切れ', fallback: '一部の内容は繁体字中国語で表示されます。', noResults: '該当する商品がありません。検索条件を変更してください。', loadFailure: '商品を読み込めません。', loading: '商品を読み込み中…', more: 'さらに表示' }
  : locale.value === 'en'
    ? { heading: 'New arrivals', note: 'Easy, feminine pieces for everyday styling.', search: 'Search product name or description', searchAction: 'Search products', color: 'Color', size: 'Size', variant: 'Color and size', any: 'Any', stock: 'Stock', allStock: 'All items', inStock: 'In stock only', reset: 'Clear filters', view: 'View product and size chart', add: 'Add to cart', unavailable: 'Out of stock', fallback: 'Some content is shown in Traditional Chinese.', noResults: 'No products match these filters. Try changing your search.', loadFailure: 'Products could not be loaded.', loading: 'Loading products…', more: 'Load more' }
    : { heading: '本週新品', note: '精選日系穿搭靈感。價格與可售狀態以商品服務資料為準。', search: '搜尋商品名稱或商品描述', searchAction: '搜尋商品', color: '顏色', size: '尺寸', variant: '顏色與尺寸', any: '不限', stock: '庫存', allStock: '全部狀態', inStock: '只看有庫存', reset: '清除篩選', view: '查看商品與尺寸表', add: '加入購物車', unavailable: '缺貨', fallback: '部分內容以繁體中文顯示。', noResults: '找不到符合條件的商品，請調整搜尋或篩選。', loadFailure: '目前無法載入商品。', loading: '正在載入商品…', more: '載入更多' })

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

function searchCatalog(value: SearchFilters) {
  Object.assign(filters, value)
  catalogNextCursor.value = null
  const query: Record<string, string> = {}
  if (value.q) query.q = value.q
  if (value.colorId) query.colorId = value.colorId
  if (value.garmentSize) query.garmentSize = value.garmentSize
  if (value.availability !== 'all') query.availability = value.availability
  void router.replace({ path: route.path, query })
  void loadCatalog()
}

watch(() => [route.query.q, route.query.colorId, route.query.garmentSize, route.query.availability], ([q, colorId, garmentSize, availability]) => {
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
  if (!cart.value) await loadCart()
  const currentQuantity = cart.value?.items.find(item => item.skuId === skuId)?.quantity ?? 0
  if (cart.value) await setQuantity(skuId, currentQuantity + 1)
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
  </main>
</template>

<style scoped>
.storefront-home { min-height: 65vh; }
</style>
