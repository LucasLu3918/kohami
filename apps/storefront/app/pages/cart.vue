<script setup lang="ts">
import LocaleSwitcher from '../components/LocaleSwitcher.vue'
import { localizedPath } from '../utils/storefront-locale'
import { useCommerceCart } from '../composables/useCommerceCart'

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
const catalogSkus = ref<CatalogSku[]>([])
const catalogLoading = ref(false)
const catalogError = ref(false)
const deliveryMethod = ref<'mock_home' | 'mock_pickup'>('mock_home')
const promotionInput = ref('')
const promotionSaved = ref(false)
const contactEmail = ref('')
const confirmed = ref(false)
const router = useRouter()

const copy = computed(() => locale.value === 'zh-TW'
  ? { title: '購物車明細', back: '← 繼續選購', empty: '購物車目前是空的。', loading: '載入購物車…', retry: '重新載入', quantity: '數量', remove: '移除商品', subtotal: '小計', details: '商品規格', unitPrice: '單價', stockChanged: '商品目前缺貨，購物車不會保留庫存；請移除或重新選擇。', notReserved: '購物車尚未保留庫存', priceChanged: '示意價格已更新；請重新試算並檢查新費用。', promotion: '優惠碼', promotionHint: '合成展示優惠 DEMO15 折抵商品金額 15%；不代表正式促銷條件。', applyPromotion: '套用優惠碼', clearPromotion: '清除優惠碼', promotionApplied: '優惠已套用', delivery: '模擬交付方式', home: '模擬宅配', pickup: '模擬自取', quote: '重新試算費用', summary: '費用明細', merchandise: '商品金額', discount: '優惠', shipping: '模擬運費', total: '應付總額', expires: '報價有效至', email: '聯絡 Email', confirm: '確認這份報價與交易條件；此為模擬訂單，不會收款或配送。', placeOrder: '建立模擬訂單並保留庫存', reservation: '成功建立後才會保留庫存，期限為 15 分鐘。', pendingPayment: '待付款', catalogError: '無法讀取商品規格；可重新載入購物車或繼續選購。', localeFallback: '商品名稱部分內容以繁體中文顯示。', errors: { VERSION_CHANGED: '購物車已更新，已重新載入最新內容。', STOCK_CHANGED: '可售庫存不足，請確認商品數量後重試。', PRODUCT_UNAVAILABLE: '商品目前缺貨或已停售；請移除或重新選擇。', QUOTE_CHANGED: '商品價格已更新，請重新試算並確認新金額。', QUOTE_EXPIRED: '報價已過期，請重新試算並確認金額。', FEE_UNAVAILABLE: '此交付方式目前無可用費用。', PRECONDITION_REQUIRED: '購物車版本資訊缺漏，請重新載入。', VALIDATION_ERROR: '資料不符合展示服務要求，請檢查 Email 和確認欄位。', KEY_CONFLICT: '送出內容與先前操作不同，請重新確認報價。', SERVICE_UNAVAILABLE: '目前無法連線至本機服務，請稍後重試。' } }
  : locale.value === 'ja'
    ? { title: 'カート明細', back: '← 商品を選び続ける', empty: 'カートは空です。', loading: 'カートを読み込み中…', retry: '再読み込み', quantity: '数量', remove: '商品を削除', subtotal: '小計', details: '商品情報', unitPrice: '単価', stockChanged: '在庫切れです。カートでは在庫を確保しません。削除するか選び直してください。', notReserved: 'カートでは在庫を確保しません', priceChanged: '表示価格が更新されました。再見積もりして金額を確認してください。', promotion: 'クーポンコード', promotionHint: '展示用コード DEMO15 は商品金額から 15% 割引します。正式な販促条件ではありません。', applyPromotion: 'コードを適用', clearPromotion: 'コードを削除', promotionApplied: '適用中のコード', delivery: '模擬受取方法', home: '模擬配送', pickup: '模擬店頭受取', quote: '費用を再計算', summary: '費用明細', merchandise: '商品金額', discount: '割引', shipping: '模擬送料', total: '合計', expires: '見積もり期限', email: '連絡先 Email', confirm: 'この見積もりと条件を確認します。模擬注文であり、決済・配送はありません。', placeOrder: '模擬注文を作成して在庫を確保', reservation: '注文成立後に 15 分間在庫を確保します。', pendingPayment: '支払い待ち', catalogError: '商品情報を読み込めません。再読み込みするか商品を選び続けてください。', localeFallback: '一部の商品名は繁体字中国語で表示されます。', errors: { VERSION_CHANGED: 'カートが更新されたため、最新内容を読み込みました。', STOCK_CHANGED: '販売可能在庫が不足しています。数量を確認してください。', PRODUCT_UNAVAILABLE: '商品は在庫切れ、または販売終了です。削除するか選び直してください。', QUOTE_CHANGED: '価格が更新されました。再見積もりして確認してください。', QUOTE_EXPIRED: '見積もりの期限が切れました。再見積もりして金額を確認してください。', FEE_UNAVAILABLE: 'この受取方法の費用は利用できません。', PRECONDITION_REQUIRED: 'カートのバージョンがありません。再読み込みしてください。', VALIDATION_ERROR: 'Email と確認欄を見直してください。', KEY_CONFLICT: '前回と異なる内容です。見積もりを確認してください。', SERVICE_UNAVAILABLE: 'ローカルサービスに接続できません。しばらくしてから再試行してください。' } }
    : { title: 'Cart details', back: '← Continue shopping', empty: 'Your cart is empty.', loading: 'Loading cart…', retry: 'Reload', quantity: 'Quantity', remove: 'Remove item', subtotal: 'Subtotal', details: 'Product details', unitPrice: 'Unit price', stockChanged: 'This item is out of stock. Cart does not reserve inventory; remove it or choose another option.', notReserved: 'Cart does not reserve inventory', priceChanged: 'The display price changed. Request a new quote and review the updated amount.', promotion: 'Promo code', promotionHint: 'Demo code DEMO15 takes 15% off merchandise. It is not a real promotion.', applyPromotion: 'Apply code', clearPromotion: 'Clear code', promotionApplied: 'Applied code', delivery: 'Mock delivery method', home: 'Mock home delivery', pickup: 'Mock pickup', quote: 'Recalculate fees', summary: 'Fee summary', merchandise: 'Merchandise', discount: 'Discount', shipping: 'Mock shipping', total: 'Total due', expires: 'Quote expires', email: 'Contact email', confirm: 'I confirm this quote and its terms. This mock order does not take payment or arrange delivery.', placeOrder: 'Create mock order and reserve stock', reservation: 'Stock is reserved for 15 minutes only after the order is created.', pendingPayment: 'Awaiting payment', catalogError: 'Product details could not be loaded. Reload or continue shopping.', localeFallback: 'Some product names are shown in Traditional Chinese.', errors: { VERSION_CHANGED: 'The cart changed. The latest contents have been loaded.', STOCK_CHANGED: 'Available stock is too low. Review the quantity.', PRODUCT_UNAVAILABLE: 'This item is out of stock or unavailable. Remove it or choose another option.', QUOTE_CHANGED: 'The price changed. Request a new quote and review it.', QUOTE_EXPIRED: 'The quote expired. Request a new quote and review the amount.', FEE_UNAVAILABLE: 'No fee is available for this delivery option.', PRECONDITION_REQUIRED: 'Cart version is missing. Reload the cart.', VALIDATION_ERROR: 'Check the email and confirmation fields.', KEY_CONFLICT: 'The retry differs from the original request. Review the quote.', SERVICE_UNAVAILABLE: 'The local service is unavailable. Try again shortly.' } })

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
    || cart.value?.items.find(item => item.skuId === skuId)?.skuId
    || skuId
}
function fallbackName(skuId: string) { const sku = skuDetails(skuId); return Boolean(sku && !sku.translations.some(item => item.locale === locale.value)) }
function errorMessage() { return copy.value.errors[error.value?.code as keyof typeof copy.value.errors] || copy.value.errors.SERVICE_UNAVAILABLE }
function onQuantityChange(skuId: string, event: Event) {
  const quantity = Number((event.target as HTMLInputElement).value)
  if (Number.isInteger(quantity) && quantity >= 1 && quantity <= 100) void setQuantity(skuId, quantity)
}
async function applyPromotion(code = promotionInput.value) {
  promotionSaved.value = false
  await setPromotion(code.trim() ? code.trim().toUpperCase() : null)
  promotionSaved.value = !error.value
  promotionInput.value = cart.value?.promotionCode || ''
}
function formatExpiry(expiresAt: string) { return new Date(expiresAt).toLocaleString(locale.value) }
async function placeOrder() {
  if (!confirmed.value || !contactEmail.value.trim()) return
  const checkout = await createCheckout(contactEmail.value, deliveryMethod.value)
  if (checkout) await router.push(localizedPath(locale.value, `/checkout/${encodeURIComponent(checkout.checkoutId)}`))
}

onMounted(() => { void load(); void loadCatalog() })
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
      <LocaleSwitcher :locale="locale" />
      <p class="notice">{{ copy.notReserved }}</p>
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.back }}</NuxtLink>
    </header>

    <p v-if="loading" role="status">{{ copy.loading }}</p>
    <section v-else-if="error && !cart" class="error-panel" role="alert">
      <p>{{ errorMessage() }}</p>
      <button type="button" @click="load">{{ copy.retry }}</button>
    </section>
    <section v-else-if="!cart?.items.length" class="empty" aria-labelledby="cart-heading">
      <h2 id="cart-heading">{{ copy.title }}</h2>
      <p>{{ copy.empty }}</p>
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.back }}</NuxtLink>
    </section>

    <template v-else>
      <section aria-labelledby="cart-heading">
        <h2 id="cart-heading">{{ copy.title }}</h2>
        <p v-if="catalogLoading" role="status">{{ copy.loading }}</p>
        <p v-if="catalogError" class="notice" role="status">{{ copy.catalogError }}</p>
        <ul class="cart-lines">
          <li v-for="item in cart.items" :key="item.skuId" class="cart-line">
            <div class="line-product">
              <h3><NuxtLink v-if="skuDetails(item.skuId)" :to="localizedPath(locale, `/products/${encodeURIComponent(skuDetails(item.skuId)!.slug)}`)">{{ productName(item.skuId) }}</NuxtLink><span v-else>{{ productName(item.skuId) }}</span></h3>
              <p>{{ copy.details }}: {{ skuDetails(item.skuId)?.colorId || '—' }} · {{ skuDetails(item.skuId)?.garmentSize || '—' }} · {{ item.skuId }}</p>
              <p>{{ copy.unitPrice }}: TWD {{ item.unitPriceTwd }}</p>
              <p v-if="fallbackName(item.skuId)" class="muted">{{ copy.localeFallback }}</p>
              <p v-if="item.availability === 'OutOfStock' || skuDetails(item.skuId)?.availability === 'OutOfStock'" class="warning">{{ copy.stockChanged }}</p>
              <p v-if="skuDetails(item.skuId) && skuDetails(item.skuId)!.priceTwd !== item.unitPriceTwd" class="warning">{{ copy.priceChanged }}</p>
            </div>
            <label>{{ copy.quantity }}
              <input :value="item.quantity" type="number" min="1" max="100" step="1" :disabled="pending" @change="onQuantityChange(item.skuId, $event)" />
            </label>
            <strong class="line-subtotal">{{ copy.subtotal }}: TWD {{ item.unitPriceTwd * item.quantity }}</strong>
            <button class="remove" type="button" :disabled="pending" @click="remove(item.skuId)">{{ copy.remove }}</button>
          </li>
        </ul>
        <p v-if="error" class="error-text" role="alert">{{ errorMessage() }}</p>
        <p class="promotion-hint">{{ copy.promotionHint }}</p>
        <div class="promotion-controls">
          <label>{{ copy.promotion }}
            <input v-model="promotionInput" type="text" maxlength="32" autocomplete="off" :disabled="pending" />
          </label>
          <button type="button" :disabled="pending" @click="applyPromotion()">{{ copy.applyPromotion }}</button>
          <button v-if="cart.promotionCode" class="remove" type="button" :disabled="pending" @click="applyPromotion('')">{{ copy.clearPromotion }}</button>
        </div>
        <p v-if="promotionSaved && cart.promotionCode" role="status">{{ copy.promotionApplied }}: {{ cart.promotionCode }}</p>
        <label class="delivery">{{ copy.delivery }}
          <select v-model="deliveryMethod" :disabled="pending">
            <option value="mock_home">{{ copy.home }}</option>
            <option value="mock_pickup">{{ copy.pickup }}</option>
          </select>
        </label>
        <button type="button" :disabled="pending || !cart.items.length" @click="createQuote(deliveryMethod)">{{ copy.quote }}</button>
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
        <form class="checkout-form" @submit.prevent="placeOrder">
          <label>{{ copy.email }}
            <input v-model="contactEmail" type="email" maxlength="254" autocomplete="email" required :disabled="pending" />
          </label>
          <label class="confirmation">
            <input v-model="confirmed" type="checkbox" :disabled="pending" />
            {{ copy.confirm }}
          </label>
          <button type="submit" :disabled="pending || !confirmed || !contactEmail.trim()">{{ copy.placeOrder }}</button>
        </form>
        <p v-if="error" class="error-text" role="alert">{{ errorMessage() }} <small v-if="error.traceId">{{ error.traceId }}</small></p>
      </section>
    </template>
  </main>
</template>

<style scoped>
.cart-page { max-width: 74rem; margin: 2rem auto; padding: 1rem; color: #25252c; font-family: system-ui, sans-serif; }
.notice { border-left: 4px solid #8b3952; padding: .75rem; background: #fbf4f6; }
.cart-lines { list-style: none; padding: 0; }
.cart-line { display: grid; grid-template-columns: minmax(12rem, 1fr) minmax(7rem, 10rem) minmax(8rem, auto) auto; align-items: center; gap: 1rem; padding: 1rem 0; border-bottom: 1px solid #e8e2de; }
.line-product h3 { margin: 0 0 .4rem; }
.line-product p { margin: .25rem 0; overflow-wrap: anywhere; }
.line-product a { color: #6d2942; }
.cart-line label, .delivery { display: grid; gap: .35rem; }
input:not([type="checkbox"]), select { min-height: 2.75rem; width: 100%; min-width: 0; max-width: 100%; box-sizing: border-box; padding: .6rem; font: inherit; }
.promotion-controls { display: flex; flex-wrap: wrap; align-items: end; gap: .75rem; margin-block: 1rem; }
.promotion-controls label { display: grid; gap: .35rem; min-width: min(100%, 16rem); }
.promotion-hint { color: #625c58; }
button { min-height: 2.75rem; padding: .65rem 1rem; background: #6d2942; color: #fff; border: 0; border-radius: .25rem; font: inherit; }
button:disabled { background: #625c58; color: #fff; opacity: 1; cursor: not-allowed; }
.remove { background: #4b5360; }
.warning, .error-text { color: #a11919; }
.muted { color: #625c58; }
.quote { margin-top: 1rem; padding: 1.25rem; border: 1px solid #8a817b; border-radius: .75rem; }
.quote dl { display: grid; grid-template-columns: 1fr auto; gap: .6rem; }
.quote dd { margin: 0; text-align: right; }
.checkout-form { display: grid; gap: 1rem; max-width: 36rem; margin-top: 1.25rem; }
.checkout-form > label:not(.confirmation) { display: grid; gap: .35rem; }
.confirmation { display: flex; align-items: start; gap: .65rem; line-height: 1.5; }
.confirmation input { width: 1.25rem; height: 1.25rem; flex: 0 0 auto; margin: .15rem 0 0; }
.total { border-top: 1px solid #aaa; padding-top: .6rem; font-weight: 700; font-size: 1.1rem; }
.error-panel { padding: 1rem; border-left: 4px solid #a11919; background: #fff3f3; }
a { color: #6d2942; }
:focus-visible { outline: 3px solid #235b9d; outline-offset: 3px; }
@media (max-width: 42rem) { .cart-page { padding: .75rem; } .cart-line { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); align-items: start; } .line-product { grid-column: 1 / -1; } .line-subtotal { align-self: center; } .remove { grid-column: 1 / -1; } }
@media (max-width: 15rem) { .cart-page { padding: .5rem; } .cart-line { grid-template-columns: minmax(0, 1fr); gap: .65rem; } .line-product, .remove { grid-column: auto; } .quote { padding: .75rem; } .quote dl { grid-template-columns: minmax(0, 1fr); } .quote dd { text-align: left; } }
</style>
