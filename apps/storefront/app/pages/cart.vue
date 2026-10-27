<script setup lang="ts">
const $fetch = useStorefrontApi()
import { localizedPath } from '../utils/storefront-locale'
import { useCommerceCart } from '../composables/useCommerceCart'
import { formatProductNumber } from '../utils/product-number'
import { localizedProductColor } from '../utils/product-color'

type Locale = 'zh-TW' | 'ja' | 'en'
type CatalogSku = {
  slug: string
  skuId: string
  colorId: string
  garmentSize: string
  priceTwd: number
  availability: string
  translations: Array<{ locale: Locale; name: string }>
}
type CatalogPage = { items: CatalogSku[]; nextCursor: string | null }

const route = useRoute()
const { locale } = useStorefrontLocale()
const { cart, quote, loading, pending, error, load, setQuantity, remove, setPromotion, createQuote, createCheckout } = useCommerceCart()
const member = useMemberSession()
const wallet = useMemberWallet()
const { source } = useDataSource()
// Real members order with their account email; the local mock member orders with its synthetic id.
const orderContact = computed(() => member.session.value.email?.trim() || (source.value === 'mock' ? member.session.value.userId?.trim() : '') || '')
const paymentMethod = ref<'wallet' | 'credit_card'>('credit_card')
const catalogSkus = ref<CatalogSku[]>([])
const catalogLoading = ref(false)
const catalogError = ref(false)
const deliveryMethod = ref<'mock_home' | 'mock_pickup'>('mock_home')
const promotionInput = ref('')
const promotionSaved = ref(false)
const confirmed = ref(false)
const router = useRouter()
const quoteButton = ref<HTMLButtonElement | null>(null)
const checkoutButton = useTemplateRef<HTMLButtonElement>('checkoutButton')

const copy = computed(() => locale.value === 'zh-TW'
  ? { title: '購物車明細', back: '← 繼續選購', empty: '購物車目前是空的。', loading: '載入購物車…', retry: '重新載入', quantity: '數量', remove: '移除商品', subtotal: '小計', details: '商品規格', productNumber: '商品編號', unitPrice: '單價', stockChanged: '商品目前缺貨，購物車不會保留庫存；請移除或重新選擇。', skuUnavailable: '商品已不存在或目前停售，請移除或重新選擇。', notReserved: '購物車尚未保留庫存', priceChanged: '商品價格已更新；請重新試算並確認費用。', promotion: '優惠碼', promotionHint: '優惠碼 DEMO15 折抵商品金額 15%；折抵金額以系統試算為準。', applyPromotion: '套用優惠碼', clearPromotion: '清除優惠碼', promotionApplied: '優惠已套用', delivery: '配送方式', home: '宅配', pickup: '店面自取', quote: '重新試算費用', summary: '費用明細', merchandise: '商品金額', discount: '優惠', shipping: '配送費用', total: '應付總額', expires: '報價有效至', accountEmail: '訂單 Email', confirm: '我確認這份報價與交易條件，並同意建立訂單及保留庫存。', placeOrder: '建立訂單並保留庫存', reservation: '成功建立後才會保留庫存，期限為 15 分鐘。', pendingPayment: '待付款', catalogError: '無法讀取商品規格；可重新載入購物車或繼續選購。', localeFallback: '商品名稱部分內容以繁體中文顯示。', errors: { VERSION_CHANGED: '購物車已更新，已重新載入最新內容。', STOCK_CHANGED: '可售庫存不足，請確認商品數量後重試。', PRODUCT_UNAVAILABLE: '商品目前缺貨或已停售；請移除或重新選擇。', QUOTE_CHANGED: '商品價格已更新，請重新試算並確認新金額。', QUOTE_EXPIRED: '報價已過期，請重新試算並確認金額。', FEE_UNAVAILABLE: '此交付方式目前無可用費用。', PRECONDITION_REQUIRED: '購物車版本資訊缺漏，請重新載入。', VALIDATION_ERROR: '請重新檢查報價與確認欄位。', KEY_CONFLICT: '送出內容與先前操作不同，請重新確認報價。', AUTHENTICATION_REQUIRED: '請登入會員後繼續結帳。', SERVICE_UNAVAILABLE: '目前無法連線至本機服務，請稍後重試。' } }
  : locale.value === 'ja'
    ? { title: 'カート明細', back: '← 商品を選び続ける', empty: 'カートは空です。', loading: 'カートを読み込み中…', retry: '再読み込み', quantity: '数量', remove: '商品を削除', subtotal: '小計', details: '商品情報', productNumber: '商品番号', unitPrice: '単価', stockChanged: '在庫切れです。カートでは在庫を確保しません。削除するか選び直してください。', skuUnavailable: '商品が見つからないか販売終了です。削除するか選び直してください。', notReserved: 'カートでは在庫を確保しません', priceChanged: '商品価格が更新されました。再見積もりして金額を確認してください。', promotion: 'クーポンコード', promotionHint: 'コード DEMO15 は商品金額から 15% 割引します。割引額はシステム計算に基づきます。', applyPromotion: 'コードを適用', clearPromotion: 'コードを削除', promotionApplied: '適用中のコード', delivery: '受取方法', home: '配送', pickup: '店頭受取', quote: '費用を再計算', summary: '費用明細', merchandise: '商品金額', discount: '割引', shipping: '送料', total: '合計', expires: '見積もり期限', accountEmail: '注文 Email', confirm: 'この見積もりと条件を確認し、注文の作成と在庫確保を依頼します。', placeOrder: '注文を作成して在庫を確保', reservation: '注文成立後に 15 分間在庫を確保します。', pendingPayment: '支払い待ち', catalogError: '商品情報を読み込めません。再読み込みするか商品を選び続けてください。', localeFallback: '一部の商品名は繁体字中国語で表示されます。', errors: { VERSION_CHANGED: 'カートが更新されたため、最新内容を読み込みました。', STOCK_CHANGED: '販売可能在庫が不足しています。数量を確認してください。', PRODUCT_UNAVAILABLE: '商品は在庫切れ、または販売終了です。削除するか選び直してください。', QUOTE_CHANGED: '価格が更新されました。再見積もりして確認してください。', QUOTE_EXPIRED: '見積もりの期限が切れました。再見積もりして金額を確認してください。', FEE_UNAVAILABLE: 'この受取方法の費用は利用できません。', PRECONDITION_REQUIRED: 'カートのバージョンがありません。再読み込みしてください。', VALIDATION_ERROR: '見積もりと確認欄を再度ご確認ください。', KEY_CONFLICT: '前回と異なる内容です。見積もりを確認してください。', AUTHENTICATION_REQUIRED: '会員ログイン後に注文できます。', SERVICE_UNAVAILABLE: 'ローカルサービスに接続できません。しばらくしてから再試行してください。' } }
    : { title: 'Cart details', back: '← Continue shopping', empty: 'Your cart is empty.', loading: 'Loading cart…', retry: 'Reload', quantity: 'Quantity', remove: 'Remove item', subtotal: 'Subtotal', details: 'Product details', productNumber: 'Product no.', unitPrice: 'Unit price', stockChanged: 'This item is out of stock. Cart does not reserve inventory; remove it or choose another option.', skuUnavailable: 'This item is no longer listed or available. Remove it or choose another option.', notReserved: 'Cart does not reserve inventory', priceChanged: 'The display price changed. Request a new quote and review the updated amount.', promotion: 'Promo code', promotionHint: 'Code DEMO15 takes 15% off merchandise. The discount is calculated by the system.', applyPromotion: 'Apply code', clearPromotion: 'Clear code', promotionApplied: 'Applied code', delivery: 'Delivery method', home: 'Home delivery', pickup: 'Store pickup', quote: 'Recalculate fees', summary: 'Fee summary', merchandise: 'Merchandise', discount: 'Discount', shipping: 'Delivery fee', total: 'Total due', expires: 'Quote expires', accountEmail: 'Order email', confirm: 'I confirm this quote and authorize the order and inventory reservation.', placeOrder: 'Place order and reserve stock', reservation: 'Stock is reserved for 15 minutes only after the order is created.', pendingPayment: 'Awaiting payment', catalogError: 'Product details could not be loaded. Reload or continue shopping.', localeFallback: 'Some product names are shown in Traditional Chinese.', errors: { VERSION_CHANGED: 'The cart changed. The latest contents have been loaded.', STOCK_CHANGED: 'Available stock is too low. Review the quantity.', PRODUCT_UNAVAILABLE: 'This item is out of stock or unavailable. Remove it or choose another option.', QUOTE_CHANGED: 'The price changed. Request a new quote and review it.', QUOTE_EXPIRED: 'The quote expired. Request a new quote and review the amount.', FEE_UNAVAILABLE: 'No fee is available for this delivery option.', PRECONDITION_REQUIRED: 'Cart version is missing. Reload the cart.', VALIDATION_ERROR: 'Review the quote and confirmation field.', KEY_CONFLICT: 'The retry differs from the original request. Review the quote.', AUTHENTICATION_REQUIRED: 'Sign in as a member to continue checkout.', SERVICE_UNAVAILABLE: 'The local service is unavailable. Try again shortly.' } })

const checkoutCopy = computed(() => locale.value === 'en'
  ? { items: 'Cart items', signInTitle: 'Sign in to continue checkout', signInHint: 'You can review your cart and recalculate fees as a guest. Sign in or create an account to place an order. After signing in, we will refresh your quote and continue checkout.', signIn: 'Sign in', register: 'Create an account' }
  : locale.value === 'ja'
    ? { items: 'カート内の商品', signInTitle: 'ログインして注文手続きへ', signInHint: 'ゲストのままカートを確認し、費用を再計算できます。注文の作成にはログインまたは会員登録が必要です。ログイン後に見積もりを更新して、注文手続きへ進みます。', signIn: 'ログイン', register: '新規会員登録' }
    : { items: '購物車商品', signInTitle: '登入會員以繼續結帳', signInHint: '訪客可以查看購物車並試算費用；建立訂單前需登入或建立會員。登入後會自動更新報價並接續結帳。', signIn: '會員登入', register: '建立會員' })
const paymentCopy = computed(() => locale.value === 'en'
  ? { title: 'Payment method', wallet: 'Member wallet', balance: 'Available', creditCard: 'Credit card', cardNote: 'You can choose a credit card on the payment page. External payment processing is not connected; do not enter a real card number.', insufficient: 'Insufficient balance. Add balance in your account or choose a credit card.' }
  : locale.value === 'ja'
    ? { title: '支払方法', wallet: '会員ウォレット', balance: '利用可能残高', creditCard: 'クレジットカード', cardNote: '次の画面でクレジットカードを選択できます。外部決済サービスは未接続です。', insufficient: '残高が不足しています。会員ページで追加するか、クレジットカードを選択してください。' }
    : { title: '付款方式', wallet: '會員儲值金', balance: '可用餘額', creditCard: '信用卡', cardNote: '下一頁可選擇信用卡或會員儲值金付款。外部收款服務尚未連接，請勿輸入真實卡號。', insufficient: '餘額不足。請先至會員中心新增餘額，或改選信用卡。' })
const walletCanPay = computed(() => (wallet.snapshot.value?.availableTwd || 0) >= (quote.value?.snapshot.totalTwd || Number.MAX_SAFE_INTEGER))

async function loadCatalog() {
  catalogLoading.value = true
  catalogError.value = false
  try {
    const params = new URLSearchParams({ limit: '100', locale: locale.value, sort: 'name' })
    const result = await $fetch<CatalogPage>(`/api/v1/products?${params.toString()}`)
    catalogSkus.value = result.items
    if (result.nextCursor) catalogError.value = true
  } catch {
    catalogError.value = true
  } finally {
    catalogLoading.value = false
  }
}

function skuDetails(skuId: string) { return catalogSkus.value.find(item => item.skuId === skuId) }
function productName(skuId: string) {
  const sku = skuDetails(skuId)
  return sku?.translations.find(item => item.locale === locale.value)?.name
    || sku?.translations.find(item => item.locale === 'zh-TW')?.name
    || formatProductNumber(skuId)
}
function fallbackName(skuId: string) { const sku = skuDetails(skuId); return Boolean(sku && !sku.translations.some(item => item.locale === locale.value)) }
function errorMessage() {
  if (error.value?.code === 'WALLET_INSUFFICIENT_BALANCE') {
    return locale.value === 'en' ? 'Your member balance is insufficient. No order was created and no wallet funds were debited.'
      : locale.value === 'ja' ? '会員残高が不足しています。注文は作成されず、残高も引き落とされていません。'
        : '會員餘額不足，訂單尚未建立，錢包未扣款。'
  }
  return copy.value.errors[error.value?.code as keyof typeof copy.value.errors] || copy.value.errors.SERVICE_UNAVAILABLE
}
async function onQuantityChange(skuId: string, event: Event) {
  const input = event.currentTarget as HTMLInputElement
  const quantity = Number(input.value)
  if (Number.isInteger(quantity) && quantity >= 1 && quantity <= 100) {
    await setQuantity(skuId, quantity)
    await nextTick()
    input.focus()
  }
}
async function requestQuote() {
  if (pending.value) return
  await createQuote(deliveryMethod.value)
  await nextTick()
  quoteButton.value?.focus()
}
async function applyPromotion(code = promotionInput.value) {
  promotionSaved.value = false
  await setPromotion(code.trim() ? code.trim().toUpperCase() : null)
  promotionSaved.value = !error.value
  promotionInput.value = cart.value?.promotionCode || ''
}
function formatExpiry(expiresAt: string) { return new Date(expiresAt).toLocaleString(locale.value) }
async function placeOrder() {
  if (member.session.value.state !== 'MemberAuthenticated') {
    await router.push({ path: localizedPath(locale.value, '/account'), query: { returnTo: `${route.path}?checkout=1` } })
    return
  }
  const contact = orderContact.value
  if (!confirmed.value || !contact) return
  const checkout = await createCheckout(contact, deliveryMethod.value, paymentMethod.value)
  if (checkout) {
    if (paymentMethod.value === 'wallet') await wallet.load()
    await router.push(localizedPath(locale.value, `/checkout/${encodeURIComponent(checkout.checkoutId)}`))
  } else {
    await nextTick()
    checkoutButton.value?.focus()
  }
}

onMounted(async () => {
  const [session] = await Promise.all([member.load(), load(), loadCatalog()])
  if (session.state === 'MemberAuthenticated') await wallet.load()
  if (session.state === 'MemberAuthenticated' && route.query.checkout === '1') {
    if (cart.value?.items.length) await requestQuote()
    await router.replace({ path: route.path })
  }
})
watch(() => cart.value?.promotionCode, code => { promotionInput.value = code || '' })
watch([locale, deliveryMethod], () => { quote.value = null; confirmed.value = false })
watch(() => quote.value?.id, () => { confirmed.value = false })
watch(locale, () => { void loadCatalog() })
useLocalizedPageHead({ title: computed(() => copy.value.title), path: computed(() => route.path), locale, publicPage: false })
</script>

<template>
  <main id="main-content" class="cart-page" tabindex="-1">
    <header>
      <h1>{{ copy.title }}</h1>
      <p class="notice">{{ copy.notReserved }}</p>
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.back }}</NuxtLink>
    </header>

    <p v-if="loading" role="status">{{ copy.loading }}</p>
    <section v-else-if="error && !cart" class="error-panel" role="alert">
      <p>{{ errorMessage() }}</p>
      <button type="button" @click="load">{{ copy.retry }}</button>
    </section>
    <section v-else-if="!cart?.items.length" class="empty" role="status">
      <p>{{ copy.empty }}</p>
    </section>

    <template v-else>
      <section :aria-label="checkoutCopy.items">
        <p v-if="catalogLoading" role="status">{{ copy.loading }}</p>
        <p v-if="catalogError" class="notice" role="status">{{ copy.catalogError }}</p>
        <ul class="cart-lines">
          <li v-for="item in cart.items" :key="item.skuId" class="cart-line">
            <div class="line-product">
              <h3><NuxtLink v-if="skuDetails(item.skuId)" :to="localizedPath(locale, `/products/${encodeURIComponent(skuDetails(item.skuId)!.slug)}`)">{{ productName(item.skuId) }}</NuxtLink><span v-else>{{ productName(item.skuId) }}</span></h3>
              <p>{{ copy.details }}: {{ localizedProductColor(skuDetails(item.skuId)?.colorId, locale) || '—' }} · {{ skuDetails(item.skuId)?.garmentSize || '—' }}</p>
              <p>{{ copy.productNumber }}：{{ formatProductNumber(item.skuId) }}</p>
              <p>{{ copy.unitPrice }}: TWD {{ item.unitPriceTwd }}</p>
              <p v-if="fallbackName(item.skuId)" class="muted">{{ copy.localeFallback }}</p>
              <p v-if="(!catalogLoading && !catalogError && !skuDetails(item.skuId)) || item.availability === 'Unavailable' || skuDetails(item.skuId)?.availability === 'Unavailable'" class="warning">{{ copy.skuUnavailable }}</p>
              <p v-else-if="item.availability === 'OutOfStock' || skuDetails(item.skuId)?.availability === 'OutOfStock'" class="warning">{{ copy.stockChanged }}</p>
              <p v-if="skuDetails(item.skuId) && skuDetails(item.skuId)!.priceTwd !== item.unitPriceTwd" class="warning">{{ copy.priceChanged }}</p>
            </div>
            <label>{{ copy.quantity }}
              <input :value="item.quantity" type="number" min="1" max="100" step="1" :disabled="pending" @change="onQuantityChange(item.skuId, $event)" />
            </label>
            <strong class="line-subtotal">{{ copy.subtotal }}: TWD {{ item.unitPriceTwd * item.quantity }}</strong>
            <button class="remove" type="button" :disabled="pending" @click="remove(item.skuId)">{{ copy.remove }}</button>
          </li>
        </ul>
        <div class="cart-options">
          <section class="promotion-option" :aria-label="copy.promotion">
            <p class="promotion-hint">{{ copy.promotionHint }}</p>
            <div class="promotion-controls">
              <label>{{ copy.promotion }}
                <input v-model="promotionInput" type="text" maxlength="32" autocomplete="off" :disabled="pending" />
              </label>
              <button type="button" :disabled="pending" @click="applyPromotion()">{{ copy.applyPromotion }}</button>
              <button v-if="cart.promotionCode" class="remove" type="button" :disabled="pending" @click="applyPromotion('')">{{ copy.clearPromotion }}</button>
            </div>
            <p v-if="promotionSaved && cart.promotionCode" class="promotion-status" role="status">{{ copy.promotionApplied }}: {{ cart.promotionCode }}</p>
          </section>
          <div class="delivery-option">
            <label class="delivery">{{ copy.delivery }}
              <select v-model="deliveryMethod" :disabled="pending">
                <option value="mock_home">{{ copy.home }}</option>
                <option value="mock_pickup">{{ copy.pickup }}</option>
              </select>
            </label>
            <button ref="quoteButton" class="quote-action" type="button" :disabled="pending || !cart.items.length" @click="requestQuote">{{ copy.quote }}</button>
          </div>
        </div>
        <p v-if="error && !quote" class="error-text" role="alert">{{ errorMessage() }}</p>
      </section>

      <section v-if="quote" class="quote" role="region" :aria-label="copy.summary">
        <h2>{{ copy.summary }}</h2>
        <dl>
          <dt>{{ copy.merchandise }}</dt><dd>TWD {{ quote.snapshot.merchandiseTwd }}</dd>
          <dt>{{ copy.discount }}</dt><dd>− TWD {{ quote.snapshot.discountTwd }}</dd>
          <dt>{{ copy.shipping }}</dt><dd>TWD {{ quote.snapshot.shippingTwd }}</dd>
          <dt class="total">{{ copy.total }} ({{ quote.snapshot.currency }})</dt><dd class="total">TWD {{ quote.snapshot.totalTwd }}</dd>
        </dl>
        <p>{{ copy.expires }}: <time :datetime="quote.expiresAt">{{ formatExpiry(quote.expiresAt) }}</time></p>
        <p class="notice">{{ copy.notReserved }}</p>
        <p>{{ copy.reservation }}</p>
        <section v-if="member.session.value.state !== 'MemberAuthenticated'" class="checkout-sign-in" aria-labelledby="checkout-sign-in-heading">
          <h3 id="checkout-sign-in-heading">{{ checkoutCopy.signInTitle }}</h3>
          <p>{{ checkoutCopy.signInHint }}</p>
          <div class="checkout-sign-in-actions">
            <NuxtLink class="sign-in-action" :to="{ path: localizedPath(locale, '/account'), query: { returnTo: `${route.path}?checkout=1` } }">{{ checkoutCopy.signIn }}</NuxtLink>
            <NuxtLink class="register-action" :to="{ path: localizedPath(locale, '/register'), query: { returnTo: `${route.path}?checkout=1` } }">{{ checkoutCopy.register }}</NuxtLink>
          </div>
        </section>
        <form v-else class="checkout-form" @submit.prevent="placeOrder">
          <p>{{ copy.accountEmail }}：<strong>{{ orderContact }}</strong></p>
          <fieldset class="payment-methods">
            <legend>{{ paymentCopy.title }}</legend>
            <label><input v-model="paymentMethod" type="radio" value="wallet" :disabled="pending || !walletCanPay" />{{ paymentCopy.wallet }} · {{ paymentCopy.balance }} TWD {{ wallet.snapshot.value?.availableTwd?.toLocaleString(locale) || 0 }}</label>
            <p v-if="!walletCanPay" class="muted">{{ paymentCopy.insufficient }} <NuxtLink :to="localizedPath(locale, '/account')">{{ locale === 'en' ? 'Open account' : locale === 'ja' ? '会員ページ' : '前往會員中心' }}</NuxtLink></p>
            <label><input v-model="paymentMethod" type="radio" value="credit_card" :disabled="pending" />{{ paymentCopy.creditCard }}</label>
            <p class="muted">{{ paymentCopy.cardNote }}</p>
          </fieldset>
          <label class="confirmation">
            <input v-model="confirmed" type="checkbox" :disabled="pending" />
            {{ copy.confirm }}
          </label>
          <button ref="checkoutButton" type="submit" :disabled="pending || !confirmed || !orderContact">{{ copy.placeOrder }}</button>
        </form>
        <p v-if="error && quote" class="error-text" role="alert">{{ errorMessage() }} <small v-if="error.traceId">{{ error.traceId }}</small></p>
      </section>
    </template>
  </main>
</template>

<style scoped>
.cart-page { box-sizing: border-box; width: min(100%, 74rem); margin: 2rem auto; padding: 1rem; color: var(--store-ink-strong); font-family: system-ui, sans-serif; }
.notice { border-left: 4px solid #8b3952; padding: .75rem; background: #fbf4f6; }
.cart-lines { list-style: none; padding: 0; }
.cart-line { display: grid; grid-template-columns: minmax(12rem, 1fr) minmax(7rem, 10rem) minmax(8rem, auto) auto; align-items: center; gap: 1rem; padding: 1rem 0; border-bottom: 1px solid #e8e2de; }
.line-product h3 { margin: 0 0 .4rem; }
.line-product p { margin: .25rem 0; overflow-wrap: anywhere; }
.line-product a { color: var(--store-accent-strong); }
.cart-line label, .delivery { display: grid; gap: .35rem; }
input:not([type="checkbox"]), select { min-height: 2.75rem; width: 100%; min-width: 0; max-width: 100%; box-sizing: border-box; padding: .6rem; font: inherit; }
.cart-options { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(15rem, .8fr); gap: 1.25rem; margin-top: 1.5rem; padding: 1.25rem; border: 1px solid #e8e2de; border-radius: .5rem; background: #fbf9f7; }
.promotion-option, .delivery-option, .promotion-controls, .promotion-controls label, .delivery { min-width: 0; }
.promotion-hint { margin: 0 0 .8rem; color: var(--store-muted-strong); line-height: 1.55; }
.promotion-controls { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: end; gap: .75rem; }
.promotion-controls label { display: grid; gap: .35rem; }
.promotion-controls button { white-space: nowrap; }
.promotion-status { margin: .75rem 0 0; color: #356447; }
.delivery-option { display: grid; align-content: start; gap: .75rem; }
.quote-action { width: 100%; }
button { min-height: 2.75rem; padding: .65rem 1rem; background: var(--store-accent-strong); color: #fff; border: 0; border-radius: .25rem; font: inherit; }
button:disabled { background: var(--store-muted-strong); color: #fff; opacity: 1; cursor: not-allowed; }
.remove { background: #4b5360; }
.warning, .error-text { color: var(--store-danger); }
.muted { color: var(--store-muted-strong); }
.quote { margin-top: 1rem; padding: 1.25rem; border: 1px solid var(--store-border-strong); border-radius: .75rem; }
.quote dl { display: grid; grid-template-columns: 1fr auto; gap: .6rem; }
.quote dd { margin: 0; text-align: right; }
.checkout-form { display: grid; gap: 1rem; max-width: 36rem; margin-top: 1.25rem; }
.payment-methods { display:grid; gap:.55rem; margin:0; padding:1rem; border:1px solid var(--store-border); border-radius:.4rem; }.payment-methods legend{font-weight:700}.payment-methods label{display:flex;align-items:center;gap:.5rem}.payment-methods input{width:1.15rem;height:1.15rem}.payment-methods p{margin:.15rem 0}
.checkout-sign-in { margin-top: 1.25rem; padding: 1rem; border-inline-start: 4px solid var(--store-accent-strong); background: #fbf4f6; }
.checkout-sign-in h3 { margin-top: 0; }
.checkout-sign-in-actions { display: flex; flex-wrap: wrap; gap: .75rem; }
.checkout-sign-in-actions a { display: inline-flex; min-height: 2.75rem; align-items: center; justify-content: center; padding: .55rem 1rem; border-radius: .25rem; font-weight: 600; }
.sign-in-action { background: var(--store-accent-strong); color: #fff; }
.register-action { border: 1px solid var(--store-accent-strong); }
.checkout-form > label:not(.confirmation) { display: grid; gap: .35rem; }
.confirmation { display: flex; align-items: start; gap: .65rem; line-height: 1.5; }
.confirmation input { width: 1.25rem; height: 1.25rem; flex: 0 0 auto; margin: .15rem 0 0; }
.total { border-top: 1px solid #aaa; padding-top: .6rem; font-weight: 700; font-size: 1.1rem; }
.error-panel { padding: 1rem; border-left: 4px solid var(--store-danger); background: var(--store-danger-surface); }
a { color: var(--store-accent-strong); }
:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
@media (max-width: 48rem) { .cart-options { grid-template-columns: minmax(0, 1fr); gap: 1rem; padding: 1rem; } .delivery-option { grid-template-columns: minmax(0, 1fr) auto; align-items: end; } .quote-action { width: auto; } }
@media (max-width: 42rem) { .cart-page { padding: .75rem; } .cart-line { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); align-items: start; } .line-product { grid-column: 1 / -1; } .line-subtotal { align-self: center; } .remove { grid-column: 1 / -1; } .promotion-controls { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); } .promotion-controls label { grid-column: 1 / -1; } .promotion-controls button { white-space: normal; } .delivery-option { grid-template-columns: minmax(0, 1fr); } .quote-action { width: 100%; } }
@media (max-width: 15rem) { .cart-page { padding: .5rem; } .cart-line { grid-template-columns: minmax(0, 1fr); gap: .65rem; } .line-product, .remove { grid-column: auto; } .cart-options { padding: .65rem; } .promotion-controls { grid-template-columns: minmax(0, 1fr); } .promotion-controls label { grid-column: auto; } .quote { padding: .75rem; } .quote dl { grid-template-columns: minmax(0, 1fr); } .quote dd { text-align: left; } }
</style>
