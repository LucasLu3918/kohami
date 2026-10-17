<script setup lang="ts">
type Locale = 'zh-TW' | 'ja' | 'en'
type CheckoutLine = { skuId: string; quantity: number; unitPriceTwd: number; name?: { value?: string } }
type CheckoutStatus = {
  checkoutId: string
  mode: 'mock'
  orderState: 'AwaitingPayment' | 'Confirmed' | 'Cancelled' | 'Expired'
  paymentState: 'Created' | 'Processing' | 'Succeeded' | 'Failed' | 'Unknown'
  expiresAt: string
  snapshot: {
    merchandiseTwd: number
    discountTwd: number
    shippingTwd: number
    totalTwd: number
    lines: CheckoutLine[]
  }
}

const route = useRoute()
const checkoutId = computed(() => String(route.params.id))
const locale = computed<Locale>(() => {
  const requested = String(route.query.lang)
  return requested === 'ja' || requested === 'en' || requested === 'zh-TW' ? requested : 'zh-TW'
})
const copy = computed(() => ({
  'zh-TW': {
    title: '模擬結帳結果', notice: '全流程模擬，沒有實際付款、退款、配送或有效發票。', summary: '結帳摘要', id: '交易編號', item: '商品', quantity: '數量', amount: '金額', merchandise: '商品金額', discount: '優惠', shipping: '模擬運費', total: '模擬應付總額', order: '訂單狀態', payment: '模擬付款狀態', expires: '付款期限', mock: '模擬模式', unavailable: '目前無法讀取這筆結帳結果，請稍後重新查詢。', refresh: '重新查詢狀態', refreshing: '查詢中…', refreshFailed: '目前無法更新狀態，畫面保留上次取得的伺服器結果。',
    orderStates: { AwaitingPayment: '等待付款', Confirmed: '已確認', Cancelled: '已取消', Expired: '已到期' },
    paymentStates: { Created: '尚未啟動', Processing: '處理中', Succeeded: '模擬成功', Failed: '模擬未通過', Unknown: '結果待確認' },
    failed: '模擬付款未通過。沒有實際付款，請確認原交易狀態。', unknown: '付款結果尚未確認；請查詢這筆交易狀態，不要重複啟動付款。',
  },
  ja: {
    title: '模擬チェックアウト結果', notice: '全処理はシミュレーションです。実際の支払・返金・配送・有効な請求書はありません。', summary: 'チェックアウト概要', id: '取引 ID', item: '商品', quantity: '数量', amount: '金額', merchandise: '商品金額', discount: '割引', shipping: '模擬送料', total: '模擬合計', order: '注文状態', payment: '模擬支払状態', expires: '支払期限', mock: '模擬モード', unavailable: 'このチェックアウト結果を読み込めません。しばらくしてから再確認してください。', refresh: '状態を再確認', refreshing: '確認中…', refreshFailed: '状態を更新できません。前回取得したサーバー結果を表示しています。',
    orderStates: { AwaitingPayment: '支払待ち', Confirmed: '確認済み', Cancelled: 'キャンセル済み', Expired: '期限切れ' },
    paymentStates: { Created: '未開始', Processing: '処理中', Succeeded: '模擬成功', Failed: '模擬拒否', Unknown: '確認待ち' },
    failed: '模擬支払は拒否されました。実際の支払はありません。', unknown: '支払結果は未確認です。この取引の状態を確認し、支払を再開しないでください。',
  },
  en: {
    title: 'Mock checkout result', notice: 'All transactions are simulated. No real payments, refunds, deliveries or valid tax invoices.', summary: 'Checkout summary', id: 'Transaction ID', item: 'Item', quantity: 'Quantity', amount: 'Amount', merchandise: 'Merchandise', discount: 'Discount', shipping: 'Mock shipping', total: 'Mock total due', order: 'Order status', payment: 'Mock payment status', expires: 'Payment deadline', mock: 'Mock mode', unavailable: 'This checkout result is unavailable. Please check again later.', refresh: 'Refresh status', refreshing: 'Checking…', refreshFailed: 'The status could not be refreshed. Showing the last server result.',
    orderStates: { AwaitingPayment: 'Awaiting payment', Confirmed: 'Confirmed', Cancelled: 'Cancelled', Expired: 'Expired' },
    paymentStates: { Created: 'Not started', Processing: 'Processing', Succeeded: 'Mock success', Failed: 'Mock declined', Unknown: 'Unconfirmed' },
    failed: 'Mock payment was declined. No real payment was made.', unknown: 'The payment result is unconfirmed. Check this transaction status. Do not start payment again.',
  },
}[locale.value]!))
const demoNames: Record<string, Record<Locale, string>> = {
  sku_demo_01: { 'zh-TW': '合成示意商品 A', ja: 'デモ商品 A', en: 'Demo item A' },
  sku_demo_02: { 'zh-TW': '合成示意商品 B', ja: 'デモ商品 B', en: 'Demo item B' },
  sku_demo_03: { 'zh-TW': '合成示意商品 C', ja: 'デモ商品 C', en: 'Demo item C' },
}
const requestFetch = useRequestFetch()
const { data: checkoutPage } = await useAsyncData(`checkout-status-${checkoutId.value}`, async () => {
  try {
    const result = await requestFetch<CheckoutStatus>(`/api/v1/checkouts/${encodeURIComponent(checkoutId.value)}/status`)
    return { result, unavailable: false }
  } catch {
    return { result: null, unavailable: true }
  }
})
const checkout = computed(() => checkoutPage.value?.result || null)
const refreshing = ref(false)
const refreshFailed = ref(false)
const itemName = (line: CheckoutLine) => demoNames[line.skuId]?.[locale.value] || line.name?.value || line.skuId
async function refreshCheckoutStatus() {
  if (refreshing.value) return
  refreshing.value = true
  refreshFailed.value = false
  try {
    const result = await requestFetch<CheckoutStatus>(`/api/v1/checkouts/${encodeURIComponent(checkoutId.value)}/status`)
    checkoutPage.value = { result, unavailable: false }
  } catch {
    refreshFailed.value = true
  } finally {
    refreshing.value = false
  }
}
useHead(() => ({ title: copy.value.title, htmlAttrs: { lang: locale.value } }))
</script>

<template>
  <main>
    <header>
      <h1>KOHAMI</h1>
      <p class="notice" role="note">{{ copy.notice }}</p>
      <nav aria-label="Language">
        <NuxtLink v-for="lang in ['zh-TW', 'ja', 'en']" :key="lang" :to="{ path: route.path, query: { ...route.query, lang } }">{{ lang }}</NuxtLink>
      </nav>
    </header>

    <section aria-labelledby="checkout-title">
      <h2 id="checkout-title">{{ copy.title }}</h2>
      <p v-if="checkoutPage?.unavailable" role="alert">{{ copy.unavailable }}</p>
      <template v-else-if="checkout">
        <p><strong>{{ copy.id }}:</strong> <code>{{ checkout.checkoutId }}</code></p>
        <p><strong>{{ copy.mock }}:</strong> {{ checkout.mode }}</p>
        <button type="button" :disabled="refreshing" @click="refreshCheckoutStatus">{{ refreshing ? copy.refreshing : copy.refresh }}</button>
        <p v-if="refreshFailed" role="alert">{{ copy.refreshFailed }}</p>

        <h3>{{ copy.summary }}</h3>
        <ul>
          <li v-for="(line, index) in checkout.snapshot.lines" :key="`${line.skuId}-${index}`">
            {{ itemName(line) }} · {{ copy.quantity }} {{ line.quantity }} · TWD {{ line.unitPriceTwd * line.quantity }}
          </li>
        </ul>
        <dl>
          <dt>{{ copy.merchandise }}</dt><dd>TWD {{ checkout.snapshot.merchandiseTwd }}</dd>
          <dt>{{ copy.discount }}</dt><dd>TWD {{ checkout.snapshot.discountTwd }}</dd>
          <dt>{{ copy.shipping }}</dt><dd>TWD {{ checkout.snapshot.shippingTwd }}</dd>
          <dt><strong>{{ copy.total }}</strong></dt><dd><strong>TWD {{ checkout.snapshot.totalTwd }}</strong></dd>
          <dt>{{ copy.order }}</dt><dd aria-live="polite">{{ copy.orderStates[checkout.orderState] }}</dd>
          <dt>{{ copy.payment }}</dt><dd aria-live="polite">{{ copy.paymentStates[checkout.paymentState] }}</dd>
          <dt>{{ copy.expires }}</dt><dd>{{ checkout.expiresAt }}</dd>
        </dl>

        <p v-if="checkout.paymentState === 'Failed'" role="status">{{ copy.failed }}</p>
        <p v-else-if="checkout.paymentState === 'Unknown'" role="status">{{ copy.unknown }}</p>
      </template>
      <p v-else role="alert">{{ copy.unavailable }}</p>
    </section>
  </main>
</template>

<style scoped>
main { margin: 2rem auto; max-width: 48rem; padding: 0 1rem; }
header, section { margin-block: 1.5rem; }
.notice { border-inline-start: 0.25rem solid #83334f; padding: 0.75rem; }
nav { display: flex; gap: 1rem; }
dl { display: grid; grid-template-columns: minmax(9rem, 1fr) 2fr; gap: 0.5rem 1rem; }
dt, dd { margin: 0; }
code { overflow-wrap: anywhere; }
</style>
