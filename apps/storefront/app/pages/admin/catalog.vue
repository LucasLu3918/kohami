<script setup lang="ts">
const name = ref('合成展示商品')
const slug = ref('synthetic-product')
const price = ref(1200)
const color = ref('demo-black')
const size = ref('M')
const pending = ref(false)
const status = ref('')
const productId = ref('')
const etag = ref('')
const productState = ref('')
const skuId = ref('')
const stockDelta = ref(1)
const stockReason = ref('')
type StockView = { onHand: number; reserved: number; sellableStock: number; availability: string; movements: Array<{ id: string; actorId: string; quantityDelta: number; reason: string; createdAt: string }> }
const stock = ref<StockView | null>(null)
const stockStatus = ref('')

function makeDraft() {
  return {
      slug: slug.value.trim(),
      translations: [
        { locale: 'zh-TW', name: name.value, description: '合成展示商品說明。', material: '合成材質資料' },
        { locale: 'ja', name: '合成デモ商品', description: '合成デモ商品の説明。', material: '合成素材情報' },
        { locale: 'en', name: 'Synthetic demo product', description: 'Description for a synthetic demo product.', material: 'Synthetic material data' },
      ],
      skus: [{ id: `${slug.value.trim()}-${color.value}-${size.value}`.toLowerCase(), colorId: color.value, garmentSize: size.value, unitPriceTwd: price.value, active: true }],
      media: [{ media: { id: 'neutral-demo-image', url: '/demo-assets/neutral.svg', alt: { value: name.value, requestedLocale: 'zh-TW', sourceLocale: 'zh-TW', revision: 'demo-v1', fallback: false }, synthetic: true, revision: 'demo-v1' }, sourceRightsRef: 'synthetic_owned_demo' }],
      measurements: [{ name: 'demo-length', valueMm: 500, synthetic: true }],
    }
}
async function requestToken() { return (await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')).requestToken }
function showError(error: any, fallback: string) { return error?.data?.code === 'PERMISSION_CHANGED' ? '權限不足：Owner 可建立商品；維護／發布需商品範圍授權。' : `${fallback}：${error?.data?.code || 'SERVICE_UNAVAILABLE'}` }
async function createProduct() {
  if (pending.value || !slug.value.trim() || !Number.isSafeInteger(price.value) || price.value < 1) return
  pending.value = true; status.value = ''
  try {
    const draft = makeDraft()
    const result = await $fetch.raw<{ id: string; state: string }>('/api/v1/admin/products', { method: 'POST', body: draft, headers: { 'X-CSRF-TOKEN': await requestToken(), 'Idempotency-Key': `catalog-create-${crypto.randomUUID()}` } })
    productId.value = result._data!.id; productState.value = result._data!.state; skuId.value = draft.skus[0]?.id || ''; etag.value = result.headers.get('etag') || ''
    status.value = `已建立草稿 ${productId.value}。`
  } catch (error: any) { status.value = showError(error, '建立失敗') }
  finally { pending.value = false }
}
async function loadDraft() {
  if (!productId.value.trim() || pending.value) return
  pending.value = true; status.value = ''; etag.value = ''
  try {
    const result = await $fetch.raw<any>(`/api/v1/admin/products/${encodeURIComponent(productId.value.trim())}`)
    const draft = result._data!
    slug.value = draft.slug; name.value = draft.translations.find((item: any) => item.locale === 'zh-TW')?.name || ''
    price.value = draft.skus[0]?.unitPriceTwd || 1; color.value = draft.skus[0]?.colorId || ''; size.value = draft.skus[0]?.garmentSize || ''
    skuId.value = draft.skus[0]?.id || ''; stock.value = null; stockStatus.value = ''
    productState.value = draft.state; etag.value = result.headers.get('etag') || ''; status.value = `已載入 ${productId.value}（${productState.value}）。`
  } catch (error: any) { status.value = showError(error, '載入失敗') }
  finally { pending.value = false }
}
async function loadStock() {
  if (!skuId.value || pending.value) return
  pending.value = true; stockStatus.value = ''
  try {
    const result = await $fetch.raw<StockView>(`/api/v1/admin/stock/${encodeURIComponent(skuId.value)}`)
    stock.value = result._data!
    stockStatus.value = '庫存與最近異動已更新。'
  } catch (error: any) { stockStatus.value = `讀取庫存失敗：${error?.data?.code || 'SERVICE_UNAVAILABLE'}` }
  finally { pending.value = false }
}
async function adjustStock() {
  if (!skuId.value || pending.value || !Number.isSafeInteger(stockDelta.value) || stockDelta.value === 0 || !stockReason.value.trim()) return
  pending.value = true; stockStatus.value = ''
  try {
    const result = await $fetch<{ id: string }>('/api/v1/admin/stock-adjustments', { method: 'POST', body: { skuId: skuId.value, quantityDelta: stockDelta.value, reason: stockReason.value.trim() }, headers: { 'X-CSRF-TOKEN': await requestToken(), 'Idempotency-Key': `stock-adjust-${crypto.randomUUID()}` } })
    stockStatus.value = `庫存異動 ${result.id} 已記錄。`; stockReason.value = ''
    stock.value = (await $fetch.raw<StockView>(`/api/v1/admin/stock/${encodeURIComponent(skuId.value)}`))._data!
  } catch (error: any) { stockStatus.value = `調整失敗：${error?.data?.code || 'SERVICE_UNAVAILABLE'}` }
  finally { pending.value = false }
}
async function saveDraft() {
  if (!productId.value || !etag.value || pending.value) return
  pending.value = true
  try {
    const result = await $fetch.raw<any>(`/api/v1/admin/products/${encodeURIComponent(productId.value)}`, { method: 'PUT', body: makeDraft(), headers: { 'X-CSRF-TOKEN': await requestToken(), 'If-Match': etag.value, 'Idempotency-Key': `catalog-update-${crypto.randomUUID()}` } })
    etag.value = result.headers.get('etag') || ''; productState.value = result._data!.state; skuId.value = result._data!.skus[0]?.id || skuId.value; stock.value = null; status.value = '草稿已儲存。若商品已上架，公開版本仍維持至重新發布。'
  } catch (error: any) { status.value = showError(error, '儲存失敗') }
  finally { pending.value = false }
}
async function publication(action: 'publish' | 'unpublish') {
  if (!productId.value || !etag.value || pending.value) return
  pending.value = true
  try {
    const result = await $fetch.raw<{ state: string }>(`/api/v1/admin/products/${encodeURIComponent(productId.value)}/publication-commands`, { method: 'POST', body: { action }, headers: { 'X-CSRF-TOKEN': await requestToken(), 'If-Match': etag.value, 'Idempotency-Key': `catalog-${action}-${crypto.randomUUID()}` } })
    etag.value = result.headers.get('etag') || ''; productState.value = result._data!.state; status.value = action === 'publish' ? '商品已發布。' : '商品已下架。'
  } catch (error: any) { status.value = showError(error, action === 'publish' ? '發布失敗' : '下架失敗') }
  finally { pending.value = false }
}
</script>

<template>
  <main id="main-content" class="catalog-admin" tabindex="-1">
    <NuxtLink to="/">← 返回商店</NuxtLink>
    <h1>商品目錄管理</h1>
    <p>僅建立合成資料草稿。Owner 可建立商品；Merchandiser 需有商品範圍授權才能維護及發布。</p>
    <form @submit.prevent="loadDraft">
      <label>已授權商品 ID<input v-model="productId" required maxlength="100"></label>
      <button :disabled="pending">載入草稿</button>
    </form>
    <form @submit.prevent="createProduct">
      <label>繁體中文商品名稱<input v-model="name" required maxlength="200"></label>
      <label>商品網址代稱<input v-model="slug" required pattern="[A-Za-z0-9_-]+" maxlength="100"></label>
      <label>SKU 顏色代碼<input v-model="color" required maxlength="80"></label>
      <label>服裝尺寸<input v-model="size" required maxlength="40"></label>
      <label>新台幣單價<input v-model.number="price" type="number" min="1" max="9000000000000000" required></label>
      <button :disabled="pending">{{ pending ? '處理中…' : '建立商品草稿' }}</button>
    </form>
    <section v-if="productId && etag" aria-label="商品操作">
      <p>商品狀態：{{ productState }}</p>
      <button :disabled="pending" @click="saveDraft">儲存變更</button>
      <button :disabled="pending" @click="publication('publish')">發布</button>
      <button :disabled="pending" @click="publication('unpublish')">下架</button>
      <NuxtLink :to="`/products/${encodeURIComponent(slug)}`">顧客商品頁</NuxtLink>
    </section>
    <section v-if="skuId" aria-labelledby="stock-heading">
      <h2 id="stock-heading">SKU 庫存</h2>
      <p>初期倉庫：demo_default。調整採正負數異動；保留庫存不可被扣減，顧客查詢與結帳由伺服器即時重驗。</p>
      <p>SKU：{{ skuId }}</p>
      <button type="button" :disabled="pending" @click="loadStock">讀取庫存與異動</button>
      <dl v-if="stock">
        <dt>在庫</dt><dd>{{ stock.onHand }}</dd>
        <dt>已保留</dt><dd>{{ stock.reserved }}</dd>
        <dt>可售</dt><dd>{{ stock.sellableStock }}（{{ stock.availability === 'InStock' ? '有庫存' : '缺貨' }}）</dd>
      </dl>
      <ul v-if="stock?.movements.length" aria-label="最近庫存異動">
        <li v-for="movement in stock.movements" :key="movement.id">{{ movement.createdAt }} · {{ movement.quantityDelta > 0 ? '+' : '' }}{{ movement.quantityDelta }} · {{ movement.reason }} · {{ movement.actorId }}</li>
      </ul>
      <form @submit.prevent="adjustStock">
        <label>異動數量（正數增加，負數減少）<input v-model.number="stockDelta" type="number" min="-2147483647" max="2147483647" step="1" required></label>
        <label>異動原因<textarea v-model="stockReason" minlength="1" maxlength="500" required></textarea></label>
        <button :disabled="pending || !Number.isSafeInteger(stockDelta) || stockDelta === 0 || !stockReason.trim()">{{ pending ? '處理中…' : '記錄庫存異動' }}</button>
      </form>
      <p role="status">{{ stockStatus }}</p>
    </section>
    <p role="status">{{ status }}</p>
  </main>
</template>

<style scoped>
.catalog-admin { max-width: 40rem; margin: 2rem auto; padding: 1rem; font-family: system-ui, sans-serif; }
form { display: grid; gap: 1rem; margin-block: 1.5rem; }
label { display: grid; gap: .35rem; }
input, button { padding: .6rem; font: inherit; }
</style>
