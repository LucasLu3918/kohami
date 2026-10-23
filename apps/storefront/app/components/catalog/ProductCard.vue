<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import { localizedProductColor } from '../../utils/product-color'

type ProductSku = { skuId: string; colorId: string; garmentSize: string; priceTwd: number; availability: string }
type Product = { slug: string; translations: Array<{ locale: StorefrontLocale; name: string }>; media: Array<{ media: { url: string; alt: { value: string } } }>; skus: ProductSku[] }
type Labels = { variant: string; any: string; unavailable: string; view: string; add: string; fallback: string; selectVariant: string }

const props = defineProps<{
  product: Product
  locale: StorefrontLocale
  selectedSkuId: string
  labels: Labels
  cartReady: boolean
  pending: boolean
}>()
const emit = defineEmits<{
  selectSku: [slug: string, skuId: string]
  addToCart: [skuId: string]
}>()
const name = computed(() => props.product.translations.find(item => item.locale === props.locale)?.name
  || props.product.translations.find(item => item.locale === 'zh-TW')?.name
  || props.product.skus[0]?.skuId
  || '')
const missingTranslation = computed(() => !props.product.translations.some(item => item.locale === props.locale)
  && props.product.translations.some(item => item.locale === 'zh-TW'))
const priceRange = computed(() => {
  const prices = props.product.skus.map(item => item.priceTwd)
  const min = Math.min(...prices), max = Math.max(...prices)
  return min === max ? `TWD ${min.toLocaleString(props.locale)}` : `TWD ${min.toLocaleString(props.locale)}–${max.toLocaleString(props.locale)}`
})
const selectedSku = computed(() => props.product.skus.find(item => item.skuId === props.selectedSkuId))
function colorName(colorId: string) { return localizedProductColor(colorId, props.locale) }
</script>

<template>
  <article class="product-card">
    <NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)" class="product-card__image-link" :aria-label="`${labels.view}: ${name}`">
      <img v-if="product.media[0]" :src="product.media[0].media.url" :alt="product.media[0].media.alt.value" width="1024" height="1024" loading="lazy" decoding="async">
      <span class="product-card__view">{{ labels.view }}</span>
    </NuxtLink>
    <div class="product-card__body">
      <h3><NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)">{{ name }}</NuxtLink></h3>
      <p v-if="missingTranslation" class="product-card__fallback" role="note">{{ labels.fallback }}</p>
      <p class="product-card__price">{{ priceRange }}</p>
      <label class="product-card__sku">
        {{ labels.variant }}
        <select :aria-label="`${labels.variant}: ${name}`" :value="selectedSkuId" @change="emit('selectSku', product.slug, ($event.target as HTMLSelectElement).value)">
          <option value="">{{ labels.any }}</option>
          <option v-for="sku in product.skus" :key="sku.skuId" :value="sku.skuId" :disabled="sku.availability !== 'InStock'">
            {{ colorName(sku.colorId) }} · {{ sku.garmentSize }}{{ sku.availability !== 'InStock' ? ` · ${labels.unavailable}` : '' }}
          </option>
        </select>
      </label>
      <p v-if="selectedSku" class="product-card__availability" :class="{ 'is-unavailable': selectedSku.availability !== 'InStock' }" aria-live="polite">
        {{ selectedSku.availability === 'InStock' ? selectedSku.garmentSize : labels.unavailable }}
      </p>
      <p v-else-if="cartReady" class="product-card__hint">{{ labels.selectVariant }}</p>
      <button class="product-card__add" :disabled="!cartReady || !selectedSku || selectedSku.availability !== 'InStock'" :aria-disabled="pending ? 'true' : undefined" :aria-busy="pending ? 'true' : undefined" @click="emit('addToCart', selectedSkuId)">{{ labels.add }}</button>
    </div>
  </article>
</template>

<style scoped>
.product-card { min-width: 0; border: 0; background: var(--store-surface); }
.product-card__image-link { position: relative; display: block; overflow: hidden; background: var(--store-linen); }
.product-card__image-link img { display: block; width: 100%; aspect-ratio: 4 / 5; object-fit: cover; transition: transform .35s ease; }
.product-card__image-link:hover img { transform: scale(1.025); }
.product-card__view { position: absolute; inset-inline: .75rem; inset-block-end: .75rem; padding: .6rem .75rem; border: 1px solid rgb(255 255 255 / 78%); background: rgb(255 255 255 / 94%); color: var(--store-ink); text-align: center; font-size: .75rem; letter-spacing: .04em; }
.product-card__body { display: grid; gap: .5rem; padding: .85rem .1rem .4rem; }
.product-card h3 { margin: 0; font-size: .94rem; font-weight: 500; line-height: 1.5; }
.product-card h3 a { color: var(--store-ink); text-decoration: none; }
.product-card h3 a:hover { text-decoration: underline; text-underline-offset: .2em; }
.product-card__fallback { margin: 0; color: var(--store-muted); font-size: .75rem; }
.product-card__price { margin: 0; color: var(--store-ink); font-size: .85rem; font-weight: 600; letter-spacing: .02em; }
.product-card__sku { display: grid; gap: .3rem; color: var(--store-muted); font-size: .73rem; }
.product-card__sku select { width: 100%; min-height: 2.75rem; border: 1px solid var(--store-border); border-radius: var(--store-radius-sm); background: var(--store-surface); padding: .4rem .5rem; color: var(--store-ink); font: inherit; }
.product-card__availability { margin: -.15rem 0 0; color: #68785f; font-size: .72rem; }
.product-card__hint { margin: -.15rem 0 0; color: var(--store-muted-strong); font-size: .72rem; }
.product-card__availability.is-unavailable { color: #8a4d53; }
.product-card__add { min-height: 2.75rem; border: 1px solid var(--store-accent-strong); border-radius: var(--store-radius-sm); background: var(--store-accent-strong); color: #fff; font: inherit; font-size: .78rem; letter-spacing: .04em; cursor: pointer; transition: background-color .16s ease, border-color .16s ease; }
.product-card__add:hover:not(:disabled) { background: var(--store-accent-hover); border-color: var(--store-accent-hover); }
.product-card__add:disabled { border-color: var(--store-border); background: var(--store-paper); color: var(--store-muted-strong); cursor: not-allowed; }
.product-card :focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
@media (prefers-reduced-motion: reduce) { .product-card__image-link img, .product-card__add { transition: none; } }
</style>
