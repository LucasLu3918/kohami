<script setup lang="ts">
const $fetch = useStorefrontApi()
import { formatProductNumber } from '../../utils/product-number'
import { localizedProductColor } from '../../utils/product-color'
type Locale = 'zh-TW' | 'ja' | 'en'
type CheckoutLine = { skuId: string; quantity: number; unitPriceTwd: number; colorId?: string; garmentSize?: string; name?: { value?: string } }
type CheckoutStatus = {
  checkoutId: string
  version: string
  orderState: 'AwaitingPayment' | 'Confirmed' | 'Cancelled' | 'Expired'
  paymentState: 'Created' | 'Processing' | 'Succeeded' | 'Failed' | 'Unknown'
  paymentMethod: 'wallet' | 'debit_card' | 'credit_card' | 'card'
  compensationRequired: boolean
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
const { locale } = useStorefrontLocale()
const copy = computed(() => ({
  'zh-TW': {
    title: '結帳結果', notice: '訂單與付款狀態依系統記錄顯示。外部收款服務尚未連接，請勿輸入真實卡號。', summary: '結帳摘要', id: '交易編號', item: '商品', productNumber: '商品編號', quantity: '數量', amount: '金額', merchandise: '商品金額', discount: '優惠', shipping: '配送費用', total: '應付總額', order: '訂單狀態', payment: '付款狀態', paymentMethod: '付款方式', paymentMethods: { wallet: '會員儲值金', debit_card: '金融卡', credit_card: '信用卡', card: '信用卡／金融卡' }, expires: '付款期限', unavailable: '目前無法讀取這筆結帳結果，請稍後重新查詢。', refresh: '重新查詢狀態', refreshing: '查詢中…', refreshFailed: '目前無法更新狀態，畫面保留上次取得的伺服器結果。',
    orderStates: { AwaitingPayment: '等待付款', Confirmed: '已確認', Cancelled: '已取消', Expired: '已到期' },
    paymentStates: { Created: '尚未啟動', Processing: '處理中', Succeeded: '系統已確認', Failed: '未通過', Unknown: '結果待確認' },
    failed: '付款未通過；確認交易狀態後可重新送出。', unknown: '付款結果尚未確認；請查詢這筆交易狀態，不要重複啟動付款。', lateCompensation: '訂單已取消或到期；付款回報較晚，退款處理中，訂單不會重新開啟。', cardNumber: '卡號', cardExpiry: '有效期限 (MM/YY)', cardSecurityCode: '安全碼', cardBrand: '卡別', cardBrands: { unknown: '尚未辨識', visa: 'Visa', mastercard: 'Mastercard', amex: 'American Express', discover: 'Discover', jcb: 'JCB' }, paymentMethodChoice: '選擇付款方式', fillTestCard: '填入範例資料', payCard: '送出付款', retryPayCard: '重新送出付款', paySubmitting: '送出中…', cardNotice: '外部收款服務尚未連接，請勿輸入真實卡號。', cardInvalid: '卡片資料格式不正確。',
  },
  ja: {
    title: '決済結果', notice: '注文と支払いの状態はシステムの記録に基づいて表示されます。外部決済サービスは未接続のため、実際のカード番号を入力しないでください。', summary: '注文概要', id: '取引 ID', item: '商品', productNumber: '商品番号', quantity: '数量', amount: '金額', merchandise: '商品金額', discount: '割引', shipping: '配送費', total: '合計金額', order: '注文状態', payment: '支払状態', paymentMethod: '支払方法', paymentMethods: { wallet: '会員ウォレット', debit_card: 'デビットカード', credit_card: 'クレジットカード', card: 'クレジット／デビットカード' }, expires: '支払期限', unavailable: 'この決済結果を読み込めません。しばらくしてから再確認してください。', refresh: '状態を再確認', refreshing: '確認中…', refreshFailed: '状態を更新できません。前回取得したサーバー結果を表示しています。',
    orderStates: { AwaitingPayment: '支払待ち', Confirmed: '確認済み', Cancelled: 'キャンセル済み', Expired: '期限切れ' },
    paymentStates: { Created: '未開始', Processing: '処理中', Succeeded: '確認済み', Failed: '未承認', Unknown: '確認待ち' },
    failed: '支払いを確認できませんでした。取引状態を確認してから再送信してください。', unknown: '支払結果は未確認です。この取引の状態を確認し、支払を再開しないでください。', lateCompensation: '注文はキャンセルまたは期限切れです。遅れて届いた支払結果の返金を処理中です。注文は再開されません。', cardNumber: 'カード番号', cardExpiry: '有効期限 (MM/YY)', cardSecurityCode: 'セキュリティコード', cardBrand: 'カード種別', cardBrands: { unknown: '未検出', visa: 'Visa', mastercard: 'Mastercard', amex: 'American Express', discover: 'Discover', jcb: 'JCB' }, paymentMethodChoice: '支払方法を選択', fillTestCard: 'サンプル情報を入力', payCard: '支払いを送信', retryPayCard: '支払いを再送信', paySubmitting: '送信中…', cardNotice: '外部決済サービスは未接続です。実際のカード番号を入力しないでください。', cardInvalid: 'カード情報の形式が正しくありません。',
  },
  en: {
    title: 'Checkout result', notice: 'Order and payment status reflect the system record. External payment processing is not connected; do not enter a real card number.', summary: 'Checkout summary', id: 'Transaction ID', item: 'Item', productNumber: 'Product no.', quantity: 'Quantity', amount: 'Amount', merchandise: 'Merchandise', discount: 'Discount', shipping: 'Delivery fee', total: 'Total due', order: 'Order status', payment: 'Payment status', paymentMethod: 'Payment method', paymentMethods: { wallet: 'Member wallet', debit_card: 'Debit card', credit_card: 'Credit card', card: 'Credit or debit card' }, expires: 'Payment deadline', unavailable: 'This checkout result is unavailable. Please check again later.', refresh: 'Refresh status', refreshing: 'Checking…', refreshFailed: 'The status could not be refreshed. Showing the last server result.',
    orderStates: { AwaitingPayment: 'Awaiting payment', Confirmed: 'Confirmed', Cancelled: 'Cancelled', Expired: 'Expired' },
    paymentStates: { Created: 'Not started', Processing: 'Processing', Succeeded: 'Confirmed', Failed: 'Not approved', Unknown: 'Unconfirmed' },
    failed: 'Payment was not approved. Check the transaction status before trying again.', unknown: 'The payment result is unconfirmed. Check this transaction status. Do not start payment again.', lateCompensation: 'The order remains cancelled or expired. A late payment result is being refunded; the order was not reopened.', cardNumber: 'Card number', cardExpiry: 'Expiry date (MM/YY)', cardSecurityCode: 'Security code', cardBrand: 'Card type', cardBrands: { unknown: 'Not detected', visa: 'Visa', mastercard: 'Mastercard', amex: 'American Express', discover: 'Discover', jcb: 'JCB' }, paymentMethodChoice: 'Payment method', fillTestCard: 'Fill sample details', payCard: 'Submit payment', retryPayCard: 'Resubmit payment', paySubmitting: 'Submitting…', cardNotice: 'External payment processing is not connected. Do not enter a real card number.', cardInvalid: 'Card details are not valid.',
  },
}[locale.value]!))
const fallbackNames: Record<string, Record<Locale, string>> = {
  sku_demo_01: { 'zh-TW': '商品 A', ja: '商品 A', en: 'Item A' },
  sku_demo_02: { 'zh-TW': '商品 B', ja: '商品 B', en: 'Item B' },
  sku_demo_03: { 'zh-TW': '商品 C', ja: '商品 C', en: 'Item C' },
}
const requestFetch = useStorefrontApi()
const { data: checkoutPage } = await useAsyncData(`checkout-status-${checkoutId.value}`, async () => {
  try {
    const result = await requestFetch<CheckoutStatus>(`/api/v1/checkouts/${encodeURIComponent(checkoutId.value)}/status`)
    return { result, unavailable: false }
  } catch {
    return { result: null, unavailable: true }
  }
}, { server: false })
const checkout = computed(() => checkoutPage.value?.result || null)
const refreshing = ref(false)
const refreshFailed = ref(false)
const cardNumber = ref('')
const cardExpiry = ref('')
const cardSecurityCode = ref('')
const paying = ref(false)
const paymentError = ref('')
const paymentOperationKey = ref('')
const selectedPaymentMethod = ref<'debit_card' | 'credit_card'>(checkout.value?.paymentMethod === 'debit_card' ? 'debit_card' : 'credit_card')
const itemName = (line: CheckoutLine) => fallbackNames[line.skuId]?.[locale.value] || line.name?.value || formatProductNumber(line.skuId)
const cardBrand = computed<'unknown' | 'visa' | 'mastercard' | 'amex' | 'discover' | 'jcb'>(() => {
  const digits = cardNumber.value.replace(/\D/g, '')
  if (/^3[47]/.test(digits)) return 'amex'
  if (/^(5[1-5]|2(2[2-9]|[3-6]\d|7[01]|720))/.test(digits)) return 'mastercard'
  if (/^(6011|65|64[4-9]|622(12[6-9]|1[3-9]\d|[2-8]\d\d|9[01]\d|92[0-5]))/.test(digits)) return 'discover'
  if (/^35(2[89]|[3-8]\d)/.test(digits)) return 'jcb'
  if (/^4/.test(digits)) return 'visa'
  return 'unknown'
})
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
function fillSampleCard() { cardNumber.value = '4111111111111111'; cardExpiry.value = '12/30'; cardSecurityCode.value = '123'; paymentError.value = '' }
async function submitPayment() {
  if (!checkout.value || paying.value || !cardNumber.value || !cardExpiry.value || !cardSecurityCode.value) return
  paying.value = true
  paymentError.value = ''
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch(`/api/v1/checkouts/${encodeURIComponent(checkoutId.value)}/payment-attempts`, {
      method: 'POST',
      body: { expectedVersion: checkout.value.version, paymentMethod: selectedPaymentMethod.value, cardNumber: cardNumber.value.replace(/\D/g, ''), expiryDate: cardExpiry.value, securityCode: cardSecurityCode.value },
      headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'If-Match': `"${checkout.value.version}"`, 'Idempotency-Key': paymentOperationKey.value || (paymentOperationKey.value = `storefront-${crypto.randomUUID()}`) },
    })
    paymentOperationKey.value = ''
    cardNumber.value = ''
    cardExpiry.value = ''
    cardSecurityCode.value = ''
    for (let attempt = 0; attempt < 10; attempt += 1) {
      await new Promise(resolve => setTimeout(resolve, 500))
      await refreshCheckoutStatus()
      if (checkout.value?.paymentState !== 'Processing') break
    }
  } catch (cause: any) {
    paymentError.value = cause?.data?.code === 'CARD_INVALID' ? copy.value.cardInvalid : copy.value.unavailable
  } finally {
    cardNumber.value = ''
    cardExpiry.value = ''
    cardSecurityCode.value = ''
    paying.value = false
  }
}
useLocalizedPageHead({ title: computed(() => copy.value.title), path: computed(() => route.path), locale, publicPage: false })
</script>

<template>
  <main id="main-content" tabindex="-1">
    <header>
      <h1>KOHAMI</h1>
      <p class="notice" role="note">{{ copy.notice }}</p>
    </header>

    <section aria-labelledby="checkout-title">
      <h2 id="checkout-title">{{ copy.title }}</h2>
        <button v-if="checkoutPage?.unavailable || checkout" type="button" :aria-disabled="refreshing" :aria-busy="refreshing" @click="refreshCheckoutStatus">{{ refreshing ? copy.refreshing : copy.refresh }}</button>
        <p v-if="refreshFailed" role="alert">{{ copy.refreshFailed }}</p>
        <template v-if="checkout">
        <p><strong>{{ copy.id }}:</strong> <code>{{ checkout.checkoutId }}</code></p>
        <h3>{{ copy.summary }}</h3>
        <ul>
          <li v-for="(line, index) in checkout.snapshot.lines" :key="`${line.skuId}-${index}`">
            {{ itemName(line) }} · {{ copy.productNumber }} {{ formatProductNumber(line.skuId) }}<span v-if="localizedProductColor(line.colorId, locale)"> · {{ localizedProductColor(line.colorId, locale) }}</span><span v-if="line.garmentSize"> · {{ line.garmentSize }}</span> · {{ copy.quantity }} {{ line.quantity }} · TWD {{ line.unitPriceTwd * line.quantity }}
          </li>
        </ul>
        <dl>
          <dt>{{ copy.merchandise }}</dt><dd>TWD {{ checkout.snapshot.merchandiseTwd }}</dd>
          <dt>{{ copy.discount }}</dt><dd>TWD {{ checkout.snapshot.discountTwd }}</dd>
          <dt>{{ copy.shipping }}</dt><dd>TWD {{ checkout.snapshot.shippingTwd }}</dd>
          <dt><strong>{{ copy.total }}</strong></dt><dd><strong>TWD {{ checkout.snapshot.totalTwd }}</strong></dd>
          <dt>{{ copy.order }}</dt><dd aria-live="polite">{{ copy.orderStates[checkout.orderState] }}</dd>
          <dt>{{ copy.payment }}</dt><dd aria-live="polite">{{ copy.paymentStates[checkout.paymentState] }}</dd>
          <dt>{{ copy.paymentMethod }}</dt><dd>{{ copy.paymentMethods[checkout.paymentMethod] }}</dd>
          <dt>{{ copy.expires }}</dt><dd>{{ checkout.expiresAt }}</dd>
        </dl>

        <p v-if="checkout.paymentState === 'Failed'" role="status">{{ copy.failed }}</p>
        <p v-else-if="checkout.paymentState === 'Unknown'" role="status">{{ copy.unknown }}</p>
        <p v-else-if="checkout.compensationRequired && checkout.paymentState === 'Succeeded' && ['Cancelled', 'Expired'].includes(checkout.orderState)" role="status">{{ copy.lateCompensation }}</p>
        <form v-if="checkout.orderState === 'AwaitingPayment' && ['Created', 'Failed'].includes(checkout.paymentState)" class="payment-form" @submit.prevent="submitPayment">
          <fieldset class="payment-methods">
            <legend>{{ copy.paymentMethodChoice }}</legend>
            <label><input v-model="selectedPaymentMethod" type="radio" value="credit_card" />{{ copy.paymentMethods.credit_card }}</label>
            <label><input v-model="selectedPaymentMethod" type="radio" value="debit_card" />{{ copy.paymentMethods.debit_card }}</label>
          </fieldset>
          <label for="test-card-number">{{ copy.cardNumber }}<input id="test-card-number" v-model="cardNumber" inputmode="numeric" autocomplete="cc-number" maxlength="23" /></label>
          <p class="card-brand" aria-live="polite">{{ copy.cardBrand }}: {{ copy.cardBrands[cardBrand] }}</p>
          <label for="test-card-expiry">{{ copy.cardExpiry }}<input id="test-card-expiry" v-model.trim="cardExpiry" inputmode="numeric" autocomplete="cc-exp" placeholder="MM/YY" maxlength="5" /></label>
          <label for="test-card-security-code">{{ copy.cardSecurityCode }}<input id="test-card-security-code" v-model.trim="cardSecurityCode" inputmode="numeric" autocomplete="cc-csc" maxlength="4" /></label>
          <button type="button" :disabled="paying" @click="fillSampleCard">{{ copy.fillTestCard }}</button>
          <p>{{ copy.cardNotice }}</p>
          <button type="submit" :disabled="paying || !cardNumber || !cardExpiry || !cardSecurityCode">{{ paying ? copy.paySubmitting : checkout.paymentState === 'Failed' ? copy.retryPayCard : copy.payCard }}</button>
          <p v-if="paymentError" role="alert">{{ paymentError }}</p>
        </form>
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
.payment-form { display: grid; max-width: 32rem; gap: .75rem; margin-block: 1.5rem; padding: 1rem; border: 1px solid var(--store-border-strong); border-radius: .5rem; }
.payment-form label { display: grid; gap: .35rem; }
.payment-methods { display: grid; gap: .5rem; border: 0; padding: 0; }
.payment-methods label { display: flex; align-items: center; gap: .5rem; }
.payment-form .card-brand { margin: 0; font-weight: 600; }
.payment-form input { min-height: 2.75rem; padding: .5rem; font: inherit; }
.payment-methods input { min-height: auto; padding: 0; }
.payment-form button { min-height: 2.75rem; padding: .5rem 1rem; font: inherit; }
dt, dd { margin: 0; }
dd { min-width: 0; overflow-wrap: anywhere; }
code { overflow-wrap: anywhere; }
@media (max-width: 15rem) { main { padding-inline: .5rem; } dl { grid-template-columns: minmax(0, 1fr); } dt { font-weight: 600; } }
</style>
