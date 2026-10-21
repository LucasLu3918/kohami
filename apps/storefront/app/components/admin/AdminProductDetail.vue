<script setup lang="ts">
import type { AdminProduct } from '../../types/admin-catalog'

const props = defineProps<{ product: AdminProduct; locale: 'zh-TW' | 'ja' | 'en' }>()
const title = computed(() => props.product.translations.find(item => item.locale === props.locale)?.name
  || props.product.translations.find(item => item.locale === 'zh-TW')?.name
  || props.product.slug)
const translation = computed(() => props.product.translations.find(item => item.locale === props.locale)
  || props.product.translations.find(item => item.locale === 'zh-TW'))
</script>

<template>
  <article class="product-detail">
    <div class="detail-hero">
      <img :src="product.media[0]?.media.url || '/demo-assets/neutral.svg'" :alt="product.media[0]?.media.alt.value || title">
      <div class="detail-hero__copy">
        <span class="eyebrow">PUBLISHED PRODUCT</span>
        <h1>{{ title }}</h1>
        <p>{{ translation?.description || '—' }}</p>
        <dl class="product-meta"><div><dt>商品代稱</dt><dd>{{ product.slug }}</dd></div><div><dt>狀態</dt><dd><span class="published-dot" />已上架</dd></div><div v-if="translation?.material"><dt>材質</dt><dd>{{ translation.material }}</dd></div></dl>
      </div>
    </div>
    <section class="sku-section" aria-labelledby="sku-title">
      <div class="section-heading"><div><span class="eyebrow">VARIANTS</span><h2 id="sku-title">SKU 與庫存</h2></div><span>{{ product.skus.length }} 個規格</span></div>
      <div class="sku-table-wrap"><table><thead><tr><th scope="col">SKU 編號</th><th scope="col">顏色</th><th scope="col">尺寸</th><th scope="col">售價</th><th scope="col">庫存</th><th scope="col">供應狀態</th></tr></thead><tbody><tr v-for="sku in product.skus" :key="sku.skuId"><td class="sku-id">{{ sku.skuId }}</td><td>{{ sku.colorId }}</td><td>{{ sku.garmentSize }}</td><td>NT$ {{ sku.priceTwd.toLocaleString('zh-TW') }}</td><td>{{ sku.stock }} 件</td><td><span class="availability" :class="sku.availability === 'InStock' ? 'available' : 'unavailable'">{{ sku.availability === 'InStock' ? '有庫存' : '售完' }}</span></td></tr></tbody></table></div>
    </section>
  </article>
</template>

<style scoped>
.detail-hero { display: grid; grid-template-columns: minmax(240px, 390px) minmax(0, 1fr); gap: clamp(1.5rem, 4vw, 3.5rem); align-items: center; padding: 1.5rem; background: #fff; border: 1px solid #e9e4df; }
.detail-hero > img { width: 100%; aspect-ratio: 4 / 3; display: block; object-fit: cover; background: #eee9e4; }
.eyebrow { color: #9a7778; font-size: .61rem; letter-spacing: .19em; }
.detail-hero__copy h1 { margin: .65rem 0 .8rem; color: #453c38; font-family: Georgia, 'Noto Serif TC', serif; font-size: clamp(1.5rem, 3vw, 2.3rem); font-weight: 400; }
.detail-hero__copy > p { max-width: 32rem; color: #796f69; font-size: .85rem; line-height: 1.8; }
.product-meta { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; margin: 1.5rem 0 0; padding-top: 1rem; border-top: 1px solid #eeeae6; }
.product-meta dt { color: #988e88; font-size: .65rem; }
.product-meta dd { margin: .35rem 0 0; color: #504641; font-size: .75rem; }
.published-dot { display: inline-block; width: 7px; height: 7px; margin-right: .4rem; background: #6c927c; border-radius: 50%; }
.sku-section { margin-top: 2.5rem; }
.section-heading { display: flex; align-items: end; justify-content: space-between; margin-bottom: .9rem; }
.section-heading h2 { margin: .4rem 0 0; color: #453c38; font-family: Georgia, 'Noto Serif TC', serif; font-size: 1.35rem; font-weight: 400; }
.section-heading > span { color: #8c827c; font-size: .72rem; }
.sku-table-wrap { overflow-x: auto; background: #fff; border: 1px solid #e9e4df; }
table { width: 100%; min-width: 620px; border-collapse: collapse; text-align: left; }
th { padding: .8rem 1rem; color: #837973; background: #f7f5f2; font-size: .67rem; font-weight: 500; }
td { padding: .9rem 1rem; color: #554b46; border-top: 1px solid #eeeae6; font-size: .75rem; }
.sku-id { color: #83736c; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: .68rem; }
.availability { display: inline-block; padding: .3rem .5rem; font-size: .65rem; }
.available { color: #52745e; background: #edf4ef; }
.unavailable { color: #8b6967; background: #f5eeee; }
@media (max-width: 760px) { .detail-hero { grid-template-columns: 1fr; padding: 1rem; gap: 1.3rem; } .detail-hero > img { max-height: 360px; } }
</style>
