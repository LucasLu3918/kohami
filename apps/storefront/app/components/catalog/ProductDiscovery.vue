<script setup lang="ts">
import ProductCard from './ProductCard.vue'
import { PhMagnifyingGlass } from '@phosphor-icons/vue'
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
  labels: { heading: string; note: string; search: string; searchAction: string; color: string; size: string; variant: string; selectVariant: string; any: string; stock: string; allStock: string; inStock: string; reset: string; view: string; add: string; unavailable: string; fallback: string; noResults: string; loadFailure: string; loading: string; more: string }
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
function selectSku(slug: string, skuId: string) {
  selectedSkuByProduct.value[slug] = skuId
  const key = `sku.${slug}`
  queryUpdate = queryUpdate.then(() => {
    const query = { ...route.query }
    if (skuId) query[key] = skuId
    else delete query[key]
    return router.replace({ path: route.path, query })
  })
}
</script>

<template>
  <section class="discovery" aria-labelledby="catalog-heading">
    <h2 id="catalog-heading">{{ labels.heading }}</h2>
    <p>{{ labels.note }}</p>
    <form class="filters" role="search" @submit.prevent="search">
      <div class="search-field">
        <label for="catalog-search">{{ labels.search }}</label>
        <span class="search-field__control">
          <input id="catalog-search" v-model="q" type="search" name="q" maxlength="100" autocomplete="off" />
          <button type="submit" :aria-label="labels.searchAction" :title="labels.searchAction"><PhMagnifyingGlass :size="19" weight="regular" aria-hidden="true" /></button>
        </span>
      </div>
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
.discovery { max-width: 84rem; margin: 0 auto 3rem; padding: clamp(1.5rem, 3vw, 2.75rem); color: var(--store-ink); }
.discovery > h2 { margin: 0; font-family: var(--store-font-display); font-size: clamp(1.55rem, 3vw, 2.2rem); font-weight: 500; letter-spacing: .045em; text-align: center; }
.discovery > p { margin: .65rem auto 1.5rem; color: var(--store-muted-strong); font-size: .9rem; line-height: 1.7; text-align: center; }
.filters { display: grid; grid-template-columns: minmax(14rem, 2fr) repeat(3, minmax(8rem, 1fr)) auto; gap: .75rem; align-items: end; margin-block: 1rem 1.5rem; padding: 1rem; border: 1px solid var(--store-border-subtle); background: var(--store-paper); }
.filters label, .product-card label { display: grid; gap: .35rem; }
.filters input, .filters select, .product-card select, .filters button { min-height: 2.75rem; }
.filters label { color: var(--store-muted-strong); font-size: .76rem; }
.search-field__control { display: flex; min-width: 0; min-height: 2.75rem; border: 1px solid var(--store-border); background: var(--store-surface); }
.search-field__control input { width: 100%; min-width: 0; border: 0; padding: .55rem .7rem; background: transparent; color: var(--store-ink); font: inherit; font-size: .86rem; }
.search-field__control button { display: grid; width: 2.75rem; flex: 0 0 2.75rem; place-items: center; border: 0; background: var(--store-accent-strong); color: #fff; cursor: pointer; }
.search-field__control button:hover, .search-field__control button:focus-visible { background: var(--store-accent-hover); }
.filters select { width: 100%; border: 1px solid var(--store-border); padding: .45rem .6rem; background: var(--store-surface); color: var(--store-ink); font: inherit; font-size: .84rem; }
.filters__reset { align-self: end; min-width: 2.75rem; border: 0; background: transparent; color: var(--store-accent-strong); font: inherit; font-size: .8rem; text-decoration: underline; text-underline-offset: .2em; cursor: pointer; }
.filters :focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.product-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: clamp(.7rem, 1.5vw, 1.35rem); }
.error { color: #a32626; }
@media (max-width: 1100px) { .product-grid { grid-template-columns: repeat(4, minmax(0, 1fr)); } }
@media (max-width: 900px) { .discovery { padding-inline: 1rem; } .filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } .search-field { grid-column: 1 / -1; } .product-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (max-width: 600px) { .product-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .85rem .55rem; } .filters button { padding-inline: .5rem; } }
</style>
