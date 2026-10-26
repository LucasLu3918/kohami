<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import { localizedProductColor } from '../../utils/product-color'

type ProductSku = { skuId: string; colorId: string; garmentSize: string; priceTwd: number; availability: string }
type Product = { slug: string; translations: Array<{ locale: StorefrontLocale; name: string }>; media: Array<{ media: { url: string; alt: { value: string } }; colorId?: string }>; skus: ProductSku[] }
type Labels = { variant: string; color: string; size: string; inStockStatus: string; any: string; unavailable: string; view: string; add: string; fallback: string; selectVariant: string }

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
const activeMediaIndex = ref(0)
const colorOptions = computed(() => [...new Set(props.product.skus.map(item => item.colorId))])
const selectedSku = computed(() => props.product.skus.find(item => item.skuId === props.selectedSkuId))
const selectedColor = computed(() => selectedSku.value?.colorId || colorOptions.value[0] || '')
const galleryMedia = computed(() => {
  const media = props.product.media
  if (!media.some(item => item.colorId)) return media
  const matching = media.filter(item => !item.colorId || item.colorId === selectedColor.value)
  return matching.length ? matching : media.filter(item => !item.colorId)
})
const activeMedia = computed(() => galleryMedia.value[activeMediaIndex.value] || galleryMedia.value[0])
watch(selectedColor, () => { activeMediaIndex.value = 0 })
function showMedia(index: number) { activeMediaIndex.value = index }
function toggleMedia() { activeMediaIndex.value = activeMediaIndex.value === 0 ? (galleryMedia.value.length > 1 ? 1 : 0) : 0 }
const mediaToggleLabel = computed(() => props.locale === 'ja'
  ? activeMediaIndex.value === 0 ? '着用イメージを見る' : '商品画像を見る'
  : props.locale === 'en'
    ? activeMediaIndex.value === 0 ? 'Show on-model view' : 'Show product view'
    : activeMediaIndex.value === 0 ? '查看模特穿搭' : '查看商品平拍')
const missingTranslation = computed(() => !props.product.translations.some(item => item.locale === props.locale)
  && props.product.translations.some(item => item.locale === 'zh-TW'))
const priceRange = computed(() => {
  const prices = props.product.skus.map(item => item.priceTwd)
  const min = Math.min(...prices), max = Math.max(...prices)
  return min === max ? `TWD ${min.toLocaleString(props.locale)}` : `TWD ${min.toLocaleString(props.locale)}–${max.toLocaleString(props.locale)}`
})
const sizeOptions = computed(() => [...new Set(props.product.skus
  .filter(item => item.colorId === selectedColor.value)
  .map(item => item.garmentSize))])
function selectColor(colorId: string) {
  const preferred = props.product.skus.find(item => item.colorId === colorId && item.garmentSize === selectedSku.value?.garmentSize && item.availability === 'InStock')
  const next = preferred || props.product.skus.find(item => item.colorId === colorId && item.availability === 'InStock')
  if (next) emit('selectSku', props.product.slug, next.skuId)
}
function selectSize(size: string) {
  const next = props.product.skus.find(item => item.colorId === selectedColor.value && item.garmentSize === size)
  if (next) emit('selectSku', props.product.slug, next.skuId)
}
function swatchColor(colorId: string) {
  const colors: Record<string, string> = {
    oatmeal: '#d9c9ad', dusty_rose: '#c98f91', burgundy: '#743346', slate_navy: '#4c596b', sand: '#cbb79a', muted_olive: '#777b5e', charcoal: '#494746', warm_beige: '#c8b79e', warm_gray: '#aaa39c', powder_blue: '#b8cbd4', indigo: '#4d5d82', muted_sage: '#a4ad9a', clay_rose: '#b77f79', deep_taupe: '#77665a', cream: '#eee7d9', gray: '#96999b', blue: '#667c9c', beige: '#cbb89d', black: '#302e2d', demo_black: '#302e2d',
  }
  return colors[colorId] || '#b8afa4'
}
function colorName(colorId: string) { return localizedProductColor(colorId, props.locale) }
</script>

<template>
  <article class="product-card">
    <div class="product-card__visual" @pointerenter="showMedia(galleryMedia.length > 1 ? 1 : 0)" @pointerleave="showMedia(0)">
      <NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)" class="product-card__image-link" :aria-label="`${labels.view}: ${name}`" @focus="showMedia(galleryMedia.length > 1 ? 1 : 0)" @blur="showMedia(0)">
        <Transition name="product-media" mode="out-in">
          <img v-if="activeMedia" :key="activeMedia.media.url" :src="activeMedia.media.url" :alt="activeMedia.media.alt.value" width="1024" height="1024" loading="lazy" decoding="async">
        </Transition>
        <span v-if="galleryMedia.length > 1" class="product-card__media-hint" aria-hidden="true">{{ activeMediaIndex === 0 ? (locale === 'ja' ? 'アイテム' : locale === 'en' ? 'The piece' : '單品細節') : (locale === 'ja' ? '着用イメージ' : locale === 'en' ? 'On-model view' : '模特穿搭') }}</span>
        <span class="product-card__view" aria-hidden="true">{{ labels.view }} <span>↗</span></span>
      </NuxtLink>
      <button v-if="galleryMedia.length > 1" type="button" class="product-card__media-toggle" :aria-label="mediaToggleLabel" @click="toggleMedia">{{ locale === 'ja' ? '画像を切替' : locale === 'en' ? 'Switch view' : '切換圖片' }}</button>
    </div>
    <div class="product-card__body">
      <h3><NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)">{{ name }}</NuxtLink></h3>
      <p v-if="missingTranslation" class="product-card__fallback" role="note">{{ labels.fallback }}</p>
      <p class="product-card__price">{{ priceRange }}</p>
      <div v-if="colorOptions.length" class="product-card__options">
        <div class="product-card__option-heading"><span>{{ labels.color }}</span><span>{{ colorName(selectedColor) }}</span></div>
        <div class="product-card__swatches" role="group" :aria-label="`${labels.color}: ${name}`">
          <button v-for="color in colorOptions" :key="color" type="button" class="product-card__swatch" :class="{ 'is-selected': selectedColor === color }" :style="{ '--swatch-color': swatchColor(color) }" :aria-label="colorName(color)" :aria-pressed="selectedColor === color" @click="selectColor(color)" />
        </div>
        <div class="product-card__option-heading product-card__size-heading"><span>{{ labels.size }}</span><span v-if="selectedSku?.availability === 'InStock'" class="product-card__stock">{{ labels.inStockStatus }}</span></div>
        <div class="product-card__sizes" role="group" :aria-label="`${labels.size}: ${name}`">
          <button v-for="size in sizeOptions" :key="size" type="button" :class="{ 'is-selected': selectedSku?.garmentSize === size }" :aria-pressed="selectedSku?.garmentSize === size" :disabled="!product.skus.some(item => item.colorId === selectedColor && item.garmentSize === size && item.availability === 'InStock')" @click="selectSize(size)">{{ size }}</button>
        </div>
      </div>
      <p class="product-card__selection-state" aria-live="polite">
        <template v-if="selectedSku">
          <span class="product-card__availability" :class="{ 'is-unavailable': selectedSku.availability !== 'InStock' }">
            {{ selectedSku.availability === 'InStock' ? `${colorName(selectedSku.colorId)} · ${selectedSku.garmentSize}` : labels.unavailable }}
          </span>
        </template>
        <span v-else-if="cartReady" class="product-card__hint">{{ labels.selectVariant }}</span>
      </p>
      <button class="product-card__add" :disabled="!cartReady || !selectedSku || selectedSku.availability !== 'InStock'" :aria-disabled="pending ? 'true' : undefined" :aria-busy="pending ? 'true' : undefined" @click="emit('addToCart', selectedSkuId)">{{ labels.add }}</button>
    </div>
  </article>
</template>

<style scoped>
.product-card { display: grid; min-width: 0; height: 100%; grid-template-rows: auto 1fr; background: var(--store-surface); }
.product-card__visual { position: relative; min-width: 0; overflow: hidden; background: var(--store-linen); }
.product-card__image-link { position: relative; display: block; overflow: hidden; background: var(--store-linen); }
.product-card__image-link img { display: block; width: 100%; aspect-ratio: 1 / 1.08; object-fit: cover; transition: transform .35s ease; }
.product-card__image-link:hover img { transform: scale(1.025); }
.product-media-enter-active, .product-media-leave-active { transition: opacity .16s ease; }
.product-media-enter-from, .product-media-leave-to { opacity: 0; }
.product-card__media-hint { position: absolute; inset-inline-start: .65rem; inset-block-end: .65rem; padding: .4rem .55rem; background: rgb(255 255 255 / 92%); color: var(--store-ink); font-size: .68rem; }
.product-card__view { position: absolute; inset-inline-end: .65rem; inset-block-end: .65rem; padding: .45rem .6rem; background: var(--store-paper); color: var(--store-ink); font-size: .67rem; letter-spacing: .035em; }
.product-card__view span { padding-left: .45rem; }
.product-card__media-toggle { position: absolute; inset-block-start: .65rem; inset-inline-end: .65rem; display: grid; min-width: 2.75rem; min-height: 2.75rem; place-items: center; padding: .45rem .65rem; border: 1px solid rgb(255 255 255 / 78%); background: rgb(255 255 255 / 94%); color: var(--store-ink); font: inherit; font-size: .68rem; cursor: pointer; }
.product-card__media-toggle:hover, .product-card__media-toggle:focus-visible { background: var(--store-accent-strong); color: #fff; }
.product-card__body { display: grid; grid-template-rows: auto auto auto minmax(2.5rem, auto) auto; align-content: start; gap: .5rem; padding: .8rem .05rem .25rem; }
.product-card h3 { min-height: 2.8rem; margin: 0; font-size: .92rem; font-weight: 500; line-height: 1.55; }
.product-card h3 a { color: var(--store-ink); text-decoration: none; }
.product-card h3 a:hover { text-decoration: underline; text-underline-offset: .2em; }
.product-card__fallback { margin: 0; color: var(--store-muted); font-size: .75rem; }
.product-card__price { margin: 0; color: var(--store-ink); font-size: .85rem; font-weight: 600; letter-spacing: .02em; }
.product-card__options { display: grid; align-content: start; gap: .3rem; color: var(--store-muted); font-size: .72rem; }
.product-card__option-heading { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.product-card__swatches, .product-card__sizes { display: flex; flex-wrap: wrap; align-items: center; gap: .45rem; }
.product-card__swatch { width: 1.1rem; height: 1.1rem; padding: 0; border: 1px solid rgb(31 27 27 / 18%); border-radius: 50%; background: var(--swatch-color); cursor: pointer; }
.product-card__swatch.is-selected { outline: 1px solid var(--store-ink); outline-offset: 2px; }
.product-card__size-heading { margin-top: .25rem; }
.product-card__stock { color: #68785f; }
.product-card__sizes button { min-width: 2.25rem; min-height: 2rem; padding: .2rem .45rem; border: 1px solid var(--store-border); background: var(--store-paper); color: var(--store-ink); font: inherit; font-size: .72rem; cursor: pointer; }
.product-card__sizes button.is-selected { border-color: var(--store-ink); background: var(--store-ink); color: #fff; }
.product-card__sizes button:disabled { color: #aaa39c; text-decoration: line-through; cursor: not-allowed; }
.product-card__selection-state { display: flex; min-height: 2.5rem; align-items: flex-start; margin: 0; font-size: .72rem; line-height: 1.5; }
.product-card__availability { color: #68785f; }
.product-card__hint { color: var(--store-muted-strong); }
.product-card__availability.is-unavailable { color: #8a4d53; }
.product-card__add { min-height: 2.75rem; border: 1px solid var(--store-accent-strong); border-radius: var(--store-radius-sm); background: var(--store-accent-strong); color: #fff; font: inherit; font-size: .78rem; letter-spacing: .04em; cursor: pointer; transition: background-color .16s ease, border-color .16s ease; }
.product-card__add:hover:not(:disabled) { background: var(--store-accent-hover); border-color: var(--store-accent-hover); }
.product-card__add:disabled { border-color: var(--store-border); background: var(--store-paper); color: var(--store-muted-strong); cursor: not-allowed; }
.product-card :focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
@media (max-width: 600px) { .product-card__body { grid-template-rows: auto auto auto minmax(2.5rem, auto) auto; gap: .4rem; padding: .6rem .05rem .2rem; } .product-card h3 { min-height: 3rem; font-size: .84rem; } .product-card__price { font-size: .78rem; } .product-card__options { font-size: .68rem; } .product-card__add { min-height: 2.6rem; font-size: .7rem; } }
@media (prefers-reduced-motion: reduce) { .product-card__image-link img, .product-card__add, .product-media-enter-active, .product-media-leave-active { transition: none; } }
</style>
