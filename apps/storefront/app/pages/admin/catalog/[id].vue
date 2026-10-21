<script setup lang="ts">
import AdminProductDetail from '../../../components/admin/AdminProductDetail.vue'
import type { AdminProduct } from '../../../types/admin-catalog'
import { useStorefrontLocale } from '../../../composables/useStorefrontLocale'

definePageMeta({ layout: 'admin' })
const route = useRoute()
const { locale } = useStorefrontLocale()
const product = ref<AdminProduct | null>(null)
const pending = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    product.value = await $fetch<AdminProduct>(`/api/v1/admin/products/${encodeURIComponent(String(route.params.id))}`)
  } catch (cause: any) {
    error.value = cause?.statusCode === 404 || cause?.data?.code === 'RESOURCE_NOT_FOUND'
      ? '找不到這項已上架商品。'
      : cause?.statusCode === 403 || cause?.data?.code === 'PERMISSION_CHANGED'
        ? '此帳號目前沒有商品目錄讀取權限。'
        : '目前無法載入商品，請稍後重試。'
  } finally {
    pending.value = false
  }
})
</script>

<template>
  <div class="detail-page">
    <NuxtLink class="back-link" to="/admin/catalog">← 回商品目錄</NuxtLink>
    <p v-if="pending" class="page-state" role="status">正在載入商品資料…</p>
    <p v-else-if="error" class="page-state page-error" role="alert">{{ error }}</p>
    <AdminProductDetail v-else-if="product" :product="product" :locale="locale" />
  </div>
</template>

<style scoped>
.back-link { display: inline-block; margin-bottom: 1.2rem; color: #856067; text-decoration: none; font-size: .72rem; }
.back-link:hover { text-decoration: underline; }
.page-state { padding: 3rem 1rem; color: #817771; text-align: center; background: #fff; }
.page-error { color: #9b4141; }
</style>
