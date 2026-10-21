<script setup lang="ts">
import AdminProductList from '../../../components/admin/AdminProductList.vue'
import type { AdminProductList as ProductListResponse } from '../../../types/admin-catalog'
import { useStorefrontLocale } from '../../../composables/useStorefrontLocale'

definePageMeta({ layout: 'admin' })
const { locale } = useStorefrontLocale()
const products = ref<ProductListResponse['items']>([])
const pending = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    const response = await $fetch<ProductListResponse>('/api/v1/admin/products')
    products.value = response.items
  } catch (cause: any) {
    error.value = cause?.statusCode === 403 || cause?.data?.code === 'PERMISSION_CHANGED'
      ? '此帳號目前沒有商品目錄讀取權限。'
      : '目前無法載入商品，請稍後重試。'
  } finally {
    pending.value = false
  }
})
</script>

<template>
  <div class="catalog-page">
    <div class="page-heading"><div><p class="eyebrow">COLLECTION MANAGEMENT</p><h1>商品目錄</h1><p class="page-description">瀏覽已上架商品、商品資訊與各規格庫存。</p><NuxtLink to="/admin/catalog/wallet">會員儲值金管理</NuxtLink></div><span class="read-only-badge">唯讀權限</span></div>
    <p v-if="pending" class="page-state" role="status">正在載入商品目錄…</p>
    <p v-else-if="error" class="page-state page-error" role="alert">{{ error }}</p>
    <AdminProductList v-else :products="products" :locale="locale" />
  </div>
</template>

<style scoped>
.page-heading { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding-bottom: 1.25rem; border-bottom: 1px solid #e8e3de; }
.eyebrow { margin: 0; color: #9a7778; font-size: .6rem; letter-spacing: .18em; }
h1 { margin: .55rem 0 .45rem; color: #443b37; font-family: Georgia, 'Noto Serif TC', serif; font-size: 1.9rem; font-weight: 400; letter-spacing: .06em; }
.page-description { margin: 0; color: #817771; font-size: .75rem; }
.read-only-badge { margin-bottom: .2rem; padding: .45rem .7rem; color: #805d5d; background: #f1e6e3; font-size: .65rem; white-space: nowrap; }
.page-state { padding: 3rem 1rem; color: #817771; text-align: center; background: #fff; }
.page-error { color: #9b4141; }
</style>
