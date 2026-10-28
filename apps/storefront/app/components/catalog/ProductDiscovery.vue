<script setup lang="ts">
import ProductCard from './ProductCard.vue'
import { localizedProductColor } from '../../utils/product-color'
import { PhMagnifyingGlass, PhX } from '@phosphor-icons/vue'
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
  labels: { heading: string; eyebrow: string; search: string; searchAction: string; clearSearch: string; color: string; size: string; variant: string; selectVariant: string; any: string; stock: string; allStock: string; inStock: string; reset: string; view: string; add: string; unavailable: string; inStockStatus: string; fallback: string; noResults: string; loadFailure: string; loading: string; more: string }
}>()
const emit = defineEmits<{
  search: [filters: SearchFilters]
  retry: []
  loadMore: []
  addToCart: [skuId: string]
}>()

const q = ref('')
const searchInput = ref<HTMLInputElement | null>(null)
const colorId = ref('')
const garmentSize = ref('')
const availability = ref<'all' | 'in_stock'>('all')
const selectedSkuByProduct = ref<Record<string, string>>({})
const route = useRoute()
const router = useRouter()
let queryUpdate: Promise<unknown> = Promise.resolve()
watch([() => route.query.q, () => route.query.colorId, () => route.query.garmentSize, () => route.query.availability], ([query, queryColorId, queryGarmentSize, queryAvailability]) => {
  q.value = String(query || '')
  colorId.value = String(queryColorId || '')
  garmentSize.value = String(queryGarmentSize || '')
  availability.value = queryAvailability === 'in_stock' ? 'in_stock' : 'all'
}, { immediate: true })
watch(() => route.query, query => {
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

async function search(event?: Event) {
  const target = event?.currentTarget as HTMLElement | null
  const form = target?.closest('form')
  const values = form ? new FormData(form) : null
  const filters = {
    q: String(values?.get('q') ?? q.value).trim(),
    colorId: String(values?.get('colorId') ?? colorId.value),
    garmentSize: String(values?.get('garmentSize') ?? garmentSize.value),
    availability: values?.get('availability') === 'in_stock' ? 'in_stock' as const : 'all' as const,
  }
  emit('search', filters)
}
async function clearSearch() {
  q.value = ''
  await search()
  await nextTick()
  searchInput.value?.focus()
}
function selectSku(slug: string, skuId: string) {
  selectedSkuByProduct.value[slug] = skuId
  const key = `sku.${slug}`
  queryUpdate = queryUpdate.then(() => {
    const query = { ...route.query }
    if (skuId) query[key] = skuId
    else delete query[key]
    return router.replace({ path: route.path, query, hash: route.hash })
  })
}
</script>

<template>
  <section class="discovery" aria-labelledby="catalog-heading">
    <header class="discovery__heading">
      <p class="discovery__eyebrow">{{ labels.eyebrow }}</p>
      <h2 id="catalog-heading">{{ labels.heading }}</h2>
    </header>
    <form class="filters" role="search" @submit.prevent="search">
      <div class="search-field">
        <label class="visually-hidden" for="catalog-search">{{ labels.search }}</label>
        <span class="search-field__control">
          <input id="catalog-search" ref="searchInput" v-model="q" type="search" name="q" maxlength="100" autocomplete="off" :placeholder="labels.search" />
          <button v-if="q" class="search-field__clear" type="button" :aria-label="labels.clearSearch" :title="labels.clearSearch" @click="clearSearch"><PhX :size="17" weight="regular" aria-hidden="true" /></button>
          <button type="submit" :aria-label="labels.searchAction" :title="labels.searchAction"><PhMagnifyingGlass :size="19" weight="regular" aria-hidden="true" /></button>
        </span>
      </div>
      <label>
        <span class="visually-hidden">{{ labels.color }}</span>
        <select v-model="colorId" name="colorId" :aria-label="labels.color" @change="search">
          <option value="">{{ labels.any }}</option>
          <option v-for="color in colors" :key="color" :value="color">{{ localizedProductColor(color, locale) }}</option>
        </select>
      </label>
      <label>
        <span class="visually-hidden">{{ labels.size }}</span>
        <select v-model="garmentSize" name="garmentSize" :aria-label="labels.size" @change="search">
          <option value="">{{ labels.any }}</option>
          <option v-for="size in sizes" :key="size" :value="size">{{ size }}</option>
        </select>
      </label>
      <label>
        <span class="visually-hidden">{{ labels.stock }}</span>
        <select v-model="availability" name="availability" :aria-label="labels.stock" @change="search">
          <option value="all">{{ labels.allStock }}</option>
          <option value="in_stock">{{ labels.inStock }}</option>
        </select>
      </label>
      <button class="filters__reset" type="button" @click="q = ''; colorId = ''; garmentSize = ''; availability = 'all'; search()">{{ labels.reset }}</button>
    </form>
    <p v-if="loading" role="status" aria-live="polite">{{ labels.loading }}</p>
    <p v-if="error" class="error" role="alert">{{ labels.loadFailure }} <button type="button" @click="emit('retry')">{{ labels.searchAction }}</button></p>
    <p v-if="!loading && !error && products.length === 0" role="status">{{ labels.noResults }}</p>

    <div class="product-grid">
      <ProductCard
        v-for="product in products"
        :key="product.slug"
        :product="product"
        :locale="locale"
        :selected-sku-id="selectedSkuByProduct[product.slug] || ''"
        :labels="labels"
        :cart-ready="cartReady"
        :pending="pending"
        @select-sku="selectSku"
        @add-to-cart="emit('addToCart', $event)"
      />
    </div>
    <button v-if="nextCursor" type="button" :disabled="loading" @click="emit('loadMore')">{{ labels.more }}</button>
  </section>
</template>

<style scoped>
.discovery { max-width: 88rem; margin: 0 auto 3.5rem; padding: clamp(2.5rem, 4.5vw, 4rem) clamp(1rem, 3vw, 2.5rem); color: var(--store-ink); }
.discovery__heading { margin: 0 0 1.1rem; }
.discovery__eyebrow { margin: 0 0 .3rem; color: var(--store-accent-strong); font-size: .72rem; font-weight: 600; letter-spacing: .16em; }
.discovery__heading h2 { margin: 0; font-family: var(--store-font-display); font-size: clamp(1.8rem, 3vw, 2.55rem); font-weight: 500; letter-spacing: .035em; }
.filters { display: grid; grid-template-columns: minmax(14rem, 1.8fr) repeat(3, minmax(6rem, 1fr)) auto; gap: .65rem; align-items: center; min-width: 0; margin: 0 0 1.1rem; }
.filters label { display: block; min-width: 0; }
.filters input, .filters select, .product-card select, .filters button { min-height: 2.75rem; }
.search-field__control { display: flex; min-width: 0; min-height: 2.75rem; border: 1px solid var(--store-border); background: var(--store-surface); }
.search-field__control input { width: 100%; min-width: 0; border: 0; padding: .55rem .7rem; background: transparent; color: var(--store-ink); font: inherit; font-size: .86rem; }
.search-field__control button[type="submit"] { display: grid; width: 2.75rem; flex: 0 0 2.75rem; place-items: center; border: 0; background: var(--store-accent-strong); color: #fff; cursor: pointer; }
.search-field__control button[type="submit"]:hover, .search-field__control button[type="submit"]:focus-visible { background: var(--store-accent-hover); }
.search-field__control .search-field__clear { display: grid; width: 2.5rem; flex: 0 0 2.5rem; place-items: center; padding: 0; border: 0; background: transparent; color: var(--store-muted-strong); cursor: pointer; }
.search-field__control .search-field__clear:hover, .search-field__control .search-field__clear:focus-visible { background: var(--store-linen); color: var(--store-ink); }
.filters select { width: 100%; min-height: 2.75rem; border: 1px solid var(--store-border); padding: .45rem .6rem; background: var(--store-surface); color: var(--store-ink); font: inherit; font-size: .84rem; }
.filters__reset { align-self: end; min-height: 2.75rem; padding-inline: .3rem; border: 0; background: transparent; color: var(--store-accent-strong); font: inherit; font-size: .8rem; text-decoration: underline; text-underline-offset: .2em; cursor: pointer; white-space: nowrap; }
.filters :focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.product-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: clamp(1rem, 2vw, 1.75rem); }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
.error { color: #a32626; }
@media (max-width: 1100px) { .product-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } .filters { grid-template-columns: minmax(10rem, 2fr) repeat(3, minmax(5.5rem, 1fr)) auto; } }
@media (max-width: 900px) { .filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } .search-field { grid-column: 1 / -1; } }
@media (max-width: 600px) { .discovery { padding-top: 2.25rem; } .discovery__heading { gap: 1rem; } .discovery__note { font-size: .82rem; } .product-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.2rem .7rem; } .filters { grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .65rem .55rem; } .search-field { grid-column: 1 / 3; grid-row: 1; } .filters__reset { grid-column: 3; grid-row: 1; justify-self: end; padding-inline: .3rem; } .filters button { padding-inline: .5rem; } }
</style>
