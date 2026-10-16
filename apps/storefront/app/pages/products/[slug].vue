<script setup lang="ts">
type Locale = 'zh-TW' | 'ja' | 'en'
type CatalogProduct = { id: string; slug: string; mode: 'mock'; translations: Array<{ locale: Locale; name: string; description: string; material: string }>; media: Array<{ media: { url: string; alt: { value: string } } }>; measurements: Array<{ name: string; valueMm: number }>; skus: Array<{ skuId: string; priceTwd: number; colorId: string; garmentSize: string; stock: number; availability: string }> }
const route = useRoute()
const requestFetch = useRequestFetch()
const locale = computed<Locale>(() => ['zh-TW', 'ja', 'en'].includes(String(route.query.lang)) ? String(route.query.lang) as Locale : 'zh-TW')
const slug = computed(() => String(route.params.slug || ''))
const { data: product, error } = await useAsyncData(`catalog-${slug.value}`, () => requestFetch<CatalogProduct>(`/api/v1/products/${encodeURIComponent(slug.value)}`))
const translation = computed(() => product.value?.translations.find(item => item.locale === locale.value) || product.value?.translations.find(item => item.locale === 'zh-TW'))
useHead(() => ({ title: translation.value?.name || 'KOHAMI 商品', htmlAttrs: { lang: locale.value } }))
</script>

<template>
  <main class="product-page">
    <NuxtLink :to="`/?lang=${locale}`">← KOHAMI</NuxtLink>
    <p v-if="error">找不到已公開的商品。</p>
    <article v-else-if="product && translation">
      <img v-if="product.media[0]" :src="product.media[0].media.url" :alt="product.media[0].media.alt.value">
      <h1>{{ translation.name }}</h1>
      <p>{{ translation.description }}</p>
      <p>{{ translation.material }}</p>
      <ul><li v-for="sku in product.skus" :key="sku.skuId">{{ sku.colorId }} · {{ sku.garmentSize }} · TWD {{ sku.priceTwd }} · {{ sku.availability === 'InStock' ? '有庫存' : '已售完' }}</li></ul>
      <p>合成展示資料，無真實付款或配送。</p>
    </article>
  </main>
</template>

<style scoped>
.product-page { max-width: 48rem; margin: 2rem auto; padding: 1rem; font-family: system-ui, sans-serif; }
img { width: min(100%, 32rem); background: #f4f2ed; }
</style>
