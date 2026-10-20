<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'

type ProductSku = { skuId: string; colorId: string; garmentSize: string; priceTwd: number; availability: string }
type Product = { slug: string; translations: Array<{ locale: StorefrontLocale; name: string }>; media: Array<{ media: { url: string; alt: { value: string } } }>; skus: ProductSku[] }
type Labels = { size: string; any: string; unavailable: string; view: string; add: string; fallback: string }

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
const colorLabels: Record<string, Record<StorefrontLocale, string>> = {
  oatmeal: { 'zh-TW': '燕麥奶油', ja: 'オートミール', en: 'Oatmeal' },
  dusty_rose: { 'zh-TW': '霧玫瑰', ja: 'ダスティローズ', en: 'Dusty rose' },
  burgundy: { 'zh-TW': '酒紅', ja: 'バーガンディ', en: 'Burgundy' },
  slate_navy: { 'zh-TW': '石板藍', ja: 'スレートネイビー', en: 'Slate navy' },
  sand: { 'zh-TW': '沙色', ja: 'サンド', en: 'Sand' },
  muted_olive: { 'zh-TW': '柔和橄欖', ja: 'オリーブ', en: 'Muted olive' },
  charcoal: { 'zh-TW': '炭灰', ja: 'チャコール', en: 'Charcoal' },
  warm_beige: { 'zh-TW': '暖米色', ja: 'ウォームベージュ', en: 'Warm beige' },
  warm_gray: { 'zh-TW': '暖灰', ja: 'ウォームグレー', en: 'Warm gray' },
  powder_blue: { 'zh-TW': '粉霧藍', ja: 'パウダーブルー', en: 'Powder blue' },
  indigo: { 'zh-TW': '靛藍', ja: 'インディゴ', en: 'Indigo' },
  muted_sage: { 'zh-TW': '鼠尾草綠', ja: 'セージ', en: 'Muted sage' },
  clay_rose: { 'zh-TW': '陶土玫瑰', ja: 'クレイローズ', en: 'Clay rose' },
  deep_taupe: { 'zh-TW': '深灰褐', ja: 'ディープトープ', en: 'Deep taupe' },
}
function colorName(colorId: string) { return colorLabels[colorId]?.[props.locale] || colorId.replaceAll('_', ' ') }
</script>

<template>
  <article class="product-card">
    <NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)" class="product-card__image-link" :aria-label="name">
      <img v-if="product.media[0]" :src="product.media[0].media.url" :alt="product.media[0].media.alt.value" loading="lazy">
      <span class="product-card__view">{{ labels.view }}</span>
    </NuxtLink>
    <div class="product-card__body">
      <h3><NuxtLink :to="localizedPath(locale, `/products/${encodeURIComponent(product.slug)}`)">{{ name }}</NuxtLink></h3>
      <p v-if="missingTranslation" class="product-card__fallback" role="note">{{ labels.fallback }}</p>
      <p class="product-card__price">{{ priceRange }}</p>
      <label class="product-card__sku">
        {{ labels.size }}
        <select :aria-label="`${labels.size}: ${name}`" :value="selectedSkuId" @change="emit('selectSku', product.slug, ($event.target as HTMLSelectElement).value)">
          <option value="">{{ labels.any }}</option>
          <option v-for="sku in product.skus" :key="sku.skuId" :value="sku.skuId" :disabled="sku.availability !== 'InStock'">
            {{ colorName(sku.colorId) }} · {{ sku.garmentSize }}{{ sku.availability !== 'InStock' ? ` · ${labels.unavailable}` : '' }}
          </option>
        </select>
      </label>
      <p v-if="selectedSku" class="product-card__availability" :class="{ 'is-unavailable': selectedSku.availability !== 'InStock' }" aria-live="polite">
        {{ selectedSku.availability === 'InStock' ? selectedSku.garmentSize : labels.unavailable }}
      </p>
      <button class="product-card__add" :disabled="!cartReady || !selectedSku || selectedSku.availability !== 'InStock'" :aria-disabled="pending ? 'true' : undefined" :aria-busy="pending ? 'true' : undefined" @click="emit('addToCart', selectedSkuId)">{{ labels.add }}</button>
    </div>
  </article>
</template>

<style scoped>
.product-card { min-width: 0; border: 0; background: #fff; }
.product-card__image-link { position: relative; display: block; overflow: hidden; background: #f5f2ee; }
.product-card__image-link img { display: block; width: 100%; aspect-ratio: 4 / 5; object-fit: cover; transition: transform .35s ease; }
.product-card__image-link:hover img { transform: scale(1.025); }
.product-card__view { position: absolute; inset-inline: .75rem; inset-block-end: .75rem; padding: .55rem .75rem; background: rgb(255 255 255 / 92%); color: #332d2b; text-align: center; font-size: .75rem; letter-spacing: .05em; }
.product-card__body { display: grid; gap: .5rem; padding: .85rem .1rem .4rem; }
.product-card h3 { margin: 0; font-size: .94rem; font-weight: 500; line-height: 1.5; }
.product-card h3 a { color: #332d2b; text-decoration: none; }
.product-card h3 a:hover { text-decoration: underline; text-underline-offset: .2em; }
.product-card__fallback { margin: 0; color: #756c67; font-size: .75rem; }
.product-card__price { margin: 0; color: #332d2b; font-size: .85rem; font-weight: 600; letter-spacing: .02em; }
.product-card__sku { display: grid; gap: .3rem; color: #756c67; font-size: .73rem; }
.product-card__sku select { width: 100%; min-height: 2.5rem; border: 1px solid #ded8d3; border-radius: 0; background: #fff; padding: .4rem .5rem; color: #332d2b; font: inherit; }
.product-card__availability { margin: -.15rem 0 0; color: #68785f; font-size: .72rem; }
.product-card__availability.is-unavailable { color: #8a4d53; }
.product-card__add { min-height: 2.65rem; border: 1px solid #332d2b; background: #332d2b; color: #fff; font: inherit; font-size: .78rem; letter-spacing: .04em; cursor: pointer; }
.product-card__add:hover:not(:disabled) { background: #76555a; border-color: #76555a; }
.product-card__add:disabled { border-color: #c8c0ba; background: #e9e5e1; color: #625c58; cursor: not-allowed; }
@media (prefers-reduced-motion: reduce) { .product-card__image-link img { transition: none; } }
</style>
