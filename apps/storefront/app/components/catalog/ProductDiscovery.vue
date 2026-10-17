<script setup lang="ts">
import { localizedPath } from '../../utils/storefront-locale'
type Locale = 'zh-TW' | 'ja' | 'en'
type Product = {
  productId: string
  slug: string
  translations: Array<{ locale: Locale; name: string; description?: string }>
  media: Array<{ media: { url: string; alt: { value: string } } }>
  skuId: string
  colorId: string
  garmentSize: string
  priceTwd: number
  availability: string
  mode: 'mock'
}
type SearchFilters = { q: string; colorId: string; garmentSize: string; availability: 'all' | 'in_stock' }

const props = defineProps<{
  items: Product[]
  locale: Locale
  loading: boolean
  error: boolean
  nextCursor: string | null
  cartReady: boolean
  pending: boolean
  labels: { heading: string; note: string; search: string; searchAction: string; color: string; size: string; any: string; stock: string; allStock: string; inStock: string; reset: string; view: string; add: string; unavailable: string; fallback: string; noResults: string; loadFailure: string; loading: string; more: string }
}>()
const emit = defineEmits<{
  search: [filters: SearchFilters]
  retry: []
  loadMore: []
  addToCart: [skuId: string]
}>()

const q = ref('')
const colorId = ref('')
const garmentSize = ref('')
const availability = ref<'all' | 'in_stock'>('all')
const selectedSkuByProduct = ref<Record<string, string>>({})
const route = useRoute()
const router = useRouter()
watch(() => route.query, query => {
  q.value = String(query.q || '')
  colorId.value = String(query.colorId || '')
  garmentSize.value = String(query.garmentSize || '')
  availability.value = query.availability === 'in_stock' ? 'in_stock' : 'all'
  selectedSkuByProduct.value = Object.fromEntries(Object.entries(query)
    .filter(([key, value]) => key.startsWith('sku.') && typeof value === 'string')
    .map(([key, value]) => [key.slice(4), value as string]))
}, { immediate: true })
const products = computed(() => {
  const groups = new Map<string, { slug: string; translations: Product['translations']; media: Product['media']; skus: Product[] }>()
  for (const item of props.items) {
    let group = groups.get(item.slug)
    if (!group) {
      group = { slug: item.slug, translations: item.translations, media: item.media, skus: [] }
      groups.set(item.slug, group)
    }
    group.skus.push(item)
  }
  return [...groups.values()]
})
const colors = computed(() => [...new Set(props.items.map(item => item.colorId))].sort())
const sizes = computed(() => [...new Set(props.items.map(item => item.garmentSize))].sort())

function search() {
  emit('search', { q: q.value.trim(), colorId: colorId.value, garmentSize: garmentSize.value, availability: availability.value })
}
function selectSku(slug: string, skuId: string) {
  selectedSkuByProduct.value[slug] = skuId
  const query = { ...route.query }
  const key = `sku.${slug}`
  if (skuId) query[key] = skuId
  else delete query[key]
  void router.replace({ path: route.path, query })
}
function productName(product: { translations: Product['translations']; skus: Product[] }) {
  return product.translations.find(item => item.locale === props.locale)?.name
    || product.translations.find(item => item.locale === 'zh-TW')?.name
    || product.skus[0]?.skuId
    || ''
}
function missingTranslation(product: { translations: Product['translations'] }) {
  return !product.translations.some(item => item.locale === props.locale)
    && product.translations.some(item => item.locale === 'zh-TW')
}
function priceRange(skus: Product[]) {
  const prices = skus.map(item => item.priceTwd)
  const min = Math.min(...prices), max = Math.max(...prices)
  return min === max ? `TWD ${min}` : `TWD ${min}–${max}`
}
</script>

<template>
  <section class="discovery" aria-labelledby="catalog-heading">
    <h2 id="catalog-heading">{{ labels.heading }}</h2>
    <p>{{ labels.note }}</p>
    <form class="filters" role="search" @submit.prevent="search">
      <label class="search-field">
        {{ labels.search }}
        <input v-model="q" type="search" name="q" maxlength="100" autocomplete="off" />
      </label>
      <label>
        {{ labels.color }}
        <select v-model="colorId" name="colorId" @change="search">
          <option value="">{{ labels.any }}</option>
          <option v-for="color in colors" :key="color" :value="color">{{ color }}</option>
        </select>
      </label>
      <label>
        {{ labels.size }}
        <select v-model="garmentSize" name="garmentSize" @change="search">
          <option value="">{{ labels.any }}</option>
          <option v-for="size in sizes" :key="size" :value="size">{{ size }}</option>
        </select>
      </label>
      <label>
        {{ labels.stock }}
        <select v-model="availability" name="availability" @change="search">
          <option value="all">{{ labels.allStock }}</option>
          <option value="in_stock">{{ labels.inStock }}</option>
        </select>
      </label>
      <button type="submit">{{ labels.searchAction }}</button>
      <button type="button" @click="q = ''; colorId = ''; garmentSize = ''; availability = 'all'; search()">{{ labels.reset }}</button>
    </form>

    <p v-if="loading" role="status" aria-live="polite">{{ labels.loading }}</p>
    <p v-if="error" class="error" role="alert">{{ labels.loadFailure }} <button type="button" @click="emit('retry')">{{ labels.searchAction }}</button></p>
    <p v-if="!loading && !error && products.length === 0" role="status">{{ labels.noResults }}</p>

    <div class="product-grid">
      <article v-for="product in products" :key="product.slug" class="product-card">
        <NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)" class="product-image-link">
          <img v-if="product.media[0]" :src="product.media[0].media.url" :alt="product.media[0].media.alt.value" loading="lazy">
          <span>{{ labels.view }}</span>
        </NuxtLink>
        <h3><NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)">{{ productName(product) }}</NuxtLink></h3>
        <p v-if="missingTranslation(product)" class="fallback-note" role="note">{{ labels.fallback }}</p>
        <p class="product-price">{{ priceRange(product.skus) }}</p>
        <label>
          {{ labels.size }}
          <select :aria-label="`${labels.size}: ${productName(product)}`" :value="selectedSkuByProduct[product.slug] || ''" @change="selectSku(product.slug, ($event.target as HTMLSelectElement).value)">
            <option value="">{{ labels.any }}</option>
            <option v-for="sku in product.skus" :key="sku.skuId" :value="sku.skuId" :disabled="sku.availability !== 'InStock'">{{ sku.colorId }} · {{ sku.garmentSize }}{{ sku.availability !== 'InStock' ? ` · ${labels.unavailable}` : '' }}</option>
          </select>
        </label>
        <button :disabled="pending || !cartReady || !selectedSkuByProduct[product.slug] || product.skus.find(sku => sku.skuId === selectedSkuByProduct[product.slug])?.availability !== 'InStock'" @click="emit('addToCart', selectedSkuByProduct[product.slug]!)">{{ labels.add }}</button>
      </article>
    </div>
    <button v-if="nextCursor" type="button" :disabled="loading" @click="emit('loadMore')">{{ labels.more }}</button>
  </section>
</template>

<style scoped>
.filters { display: grid; grid-template-columns: minmax(14rem, 2fr) repeat(3, minmax(8rem, 1fr)) auto auto; gap: .75rem; align-items: end; margin-block: 1rem; }
.filters label, .product-card label { display: grid; gap: .35rem; }
.filters input, .filters select, .product-card select, button { min-height: 2.75rem; }
.product-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 14rem), 1fr)); gap: 1rem; }
.product-card { display: grid; align-content: start; gap: .65rem; padding: 1rem; border: 1px solid #dedbd6; border-radius: .75rem; }
.product-image-link img { display: block; width: 100%; aspect-ratio: 4 / 3; object-fit: contain; background: #f7f5f1; }
.error { color: #a32626; }
.fallback-note { margin: 0; color: #625c58; font-size: .875rem; }
@media (max-width: 700px) { .filters { grid-template-columns: 1fr 1fr; } .search-field { grid-column: 1 / -1; } }
</style>
