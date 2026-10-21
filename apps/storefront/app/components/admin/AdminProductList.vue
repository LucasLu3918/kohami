<script setup lang="ts">
import { computed, ref } from 'vue'
import type { AdminProduct } from '../../types/admin-catalog'

const props = defineProps<{ products: AdminProduct[]; locale: 'zh-TW' | 'ja' | 'en' }>()
const search = ref('')
const filteredProducts = computed(() => {
  const query = search.value.trim().toLocaleLowerCase()
  if (!query) return props.products
  return props.products.filter(product => {
    const names = product.translations.map(item => item.name.toLocaleLowerCase())
    return names.some(name => name.includes(query)) || product.slug.toLocaleLowerCase().includes(query)
  })
})

function label(product: AdminProduct) {
  return product.translations.find(item => item.locale === props.locale)?.name
    || product.translations.find(item => item.locale === 'zh-TW')?.name
    || product.slug
}

function priceRange(product: AdminProduct) {
  const prices = product.skus.map(sku => sku.priceTwd)
  if (!prices.length) return '—'
  const low = Math.min(...prices).toLocaleString('zh-TW')
  const high = Math.max(...prices).toLocaleString('zh-TW')
  return low === high ? `NT$ ${low}` : `NT$ ${low}–${high}`
}
</script>

<template>
  <section class="catalog-list" aria-label="已上架商品">
    <div class="list-toolbar">
      <label class="search-box">
        <span aria-hidden="true">⌕</span>
        <span class="visually-hidden">搜尋商品</span>
        <input v-model="search" type="search" placeholder="搜尋商品名稱或代稱">
      </label>
      <span class="result-count">{{ filteredProducts.length }} 款商品</span>
    </div>
    <div v-if="filteredProducts.length" class="product-grid">
      <article v-for="product in filteredProducts" :key="product.id" class="product-card">
        <NuxtLink class="product-card__image" :to="`/admin/catalog/${encodeURIComponent(product.id)}`" :aria-label="`查看商品 ${label(product)}`">
          <img :src="product.media[0]?.media.url || '/demo-assets/neutral.svg'" :alt="product.media[0]?.media.alt.value || label(product)" loading="lazy">
          <span class="published-tag">已上架</span>
        </NuxtLink>
        <div class="product-card__body">
          <div class="product-card__title"><h2>{{ label(product) }}</h2><span>{{ product.skus.length }} SKU</span></div>
          <p class="product-slug">{{ product.slug }}</p>
          <div class="product-card__bottom"><strong>{{ priceRange(product) }}</strong><NuxtLink :to="`/admin/catalog/${encodeURIComponent(product.id)}`" :aria-label="`查看商品 ${label(product)}`">查看詳情 <span aria-hidden="true">→</span></NuxtLink></div>
        </div>
      </article>
    </div>
    <p v-else class="empty-state" role="status">沒有符合條件的商品。</p>
  </section>
</template>

<style scoped>
.list-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.8rem 0 1.25rem; }
.search-box { width: min(100%, 23rem); height: 42px; display: flex; align-items: center; gap: .6rem; padding: 0 .8rem; color: #8e8580; background: white; border: 1px solid #e7e2dd; }
.search-box > span:first-child { font-size: 1.2rem; }
.search-box input { width: 100%; padding: 0; color: #514843; background: transparent; border: 0; outline: 0; font: inherit; font-size: .8rem; }
.result-count { color: #817873; font-size: .75rem; }
.product-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.product-card { min-width: 0; overflow: hidden; background: white; border: 1px solid #e9e4df; }
.product-card__image { position: relative; display: block; aspect-ratio: 4 / 3; overflow: hidden; background: #eee9e4; }
.product-card__image img { width: 100%; height: 100%; display: block; object-fit: cover; transition: transform .35s ease; }
.product-card__image:hover img { transform: scale(1.025); }
.published-tag { position: absolute; top: .7rem; left: .7rem; padding: .35rem .5rem; color: #675450; background: #fffdfbe8; font-size: .62rem; }
.product-card__body { padding: .9rem .95rem .85rem; }
.product-card__title { display: flex; align-items: baseline; justify-content: space-between; gap: .5rem; }
.product-card__title h2 { overflow: hidden; margin: 0; color: #493f3b; font-size: .9rem; font-weight: 500; text-overflow: ellipsis; white-space: nowrap; }
.product-card__title span, .product-slug { color: #928983; font-size: .65rem; }
.product-slug { margin: .4rem 0 .8rem; }
.product-card__bottom { display: flex; align-items: center; justify-content: space-between; padding-top: .7rem; border-top: 1px solid #f0edea; }
.product-card__bottom strong { color: #68504e; font-family: Georgia, serif; font-size: .83rem; font-weight: 500; }
.product-card__bottom a { color: #8d5c62; text-decoration: none; font-size: .7rem; }
.product-card__bottom a:hover { text-decoration: underline; }
.empty-state { padding: 3rem 1rem; color: #827873; text-align: center; background: #fff; }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 980px) { .product-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { .product-grid { grid-template-columns: 1fr; gap: .8rem; } .product-card { display: grid; grid-template-columns: 38% 1fr; } .product-card__image { aspect-ratio: auto; min-height: 150px; } .product-card__body { display: flex; flex-direction: column; justify-content: center; padding: .7rem; } .product-card__title { align-items: flex-start; flex-direction: column; gap: .2rem; } .product-card__bottom { margin-top: .15rem; } }
</style>
