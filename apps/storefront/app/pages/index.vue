<script setup lang="ts">
import ProductDiscovery from '../components/catalog/ProductDiscovery.vue'

type Locale = 'zh-TW' | 'ja' | 'en'
type CartItem = { skuId: string; quantity: number; unitPriceTwd: number; availability?: string; name?: { value?: string } }
type Product = { productId: string; slug: string; translations: Array<{ locale: Locale; name: string }>; media: Array<{ media: { url: string; alt: { value: string } } }>; skuId: string; colorId: string; garmentSize: string; priceTwd: number; availability: string; mode: 'mock' }
type Shipment = { id: string; state: string; version: string }
type ReturnLine = { orderLineId: string; requestedQuantity: number; acceptedQuantity: number; receivedUnitIds: string[]; restockedUnitIds: string[]; quarantinedUnitIds: string[] }
type ReturnSummary = { id: string; state: string; version: string; lines: ReturnLine[] }
type ReturnAction = { id: string; state: string; version: string; quantity: number; orderLineId: string; inspection?: { refundCaseId?: string; refundCaseVersion?: string; refundUnits?: Array<{ unitId: string; ordinal: number; paidTwd: number }> } }
type ReceiptView = { orderId: string; taxEffect: string; receipts: Array<{ id: string; state: string; amountTwd: number; displayNotice?: string }>; adjustments: Array<{ id: string; state: string; amountTwd: number; displayNotice?: string }> }
type OrderView = { id: string; version: string; snapshot?: { lines: Array<{ orderLineId: string; skuId: string; quantity: number; units?: Array<{ unitId: string; paidTwd: number }> }> }; shipmentSummary?: { state?: string; shipments?: Shipment[] }; returns?: ReturnSummary[] }
type Cart = { id: string; version: string; items: CartItem[] }
type PriceSnapshot = { currency: string; merchandiseTwd: number; discountTwd: number; shippingTwd: number; totalTwd: number }
type Quote = { id: string; mode: 'mock'; expiresAt: string; snapshot: PriceSnapshot }
type Checkout = { checkoutId: string; orderId: string; mode: 'mock'; version: string; expiresAt: string; snapshot: PriceSnapshot; statusUrl: string }
type CheckoutStatus = { checkoutId: string; mode: 'mock'; version: string; orderState: string; paymentState: string; compensationRequired: boolean; expiresAt: string; snapshot: PriceSnapshot }
type ApiProblem = { code?: string; traceId?: string; status?: number }
type ApiOptions = { method?: 'GET' | 'POST' | 'PUT' | 'DELETE'; body?: Record<string, unknown>; ifMatch?: string; idempotencyScope?: string }

const route = useRoute()
const locale = computed<Locale>(() => ['zh-TW', 'ja', 'en'].includes(String(route.query.lang)) ? String(route.query.lang) as Locale : 'zh-TW')
const copy = computed(() => ({
  'zh-TW': {
    title: 'KOHAMI 本機展示', notice: '全流程模擬，沒有實際付款、退款、配送或有效發票。', login: '登入與身份驗證', email: '測試 Email', password: '密碼', totp: '驗證器代碼', result: '操作結果', identity: '目前身份', action: '送出', challenge: 'Challenge ID', nonce: 'Request nonce', checkoutReference: '結帳編號', viewCheckoutResult: '查看唯讀結帳摘要',
    catalog: '示意商品', catalogNote: '合成資料。送出後以伺服器回傳的價格、可購買性與庫存結果為準。', add: '加入購物車', cart: '購物車', empty: '購物車目前是空的。', quantity: '數量', subtotal: '小計', delivery: '模擬交付方式', home: '模擬宅配', pickup: '模擬自取', makeQuote: '重新試算', quote: '結帳報價', quoteExpires: '報價有效至', merchandise: '商品金額', discount: '優惠', shipping: '模擬運費', total: '應付總額', emailNote: '僅接受合成測試信箱，例如 buyer at example dot test。', confirm: '我確認以此報價建立模擬訂單，並了解這不會產生真實付款或配送。', placeOrder: '建立模擬訂單並保留庫存', checkout: '模擬結帳', reservationUntil: '庫存保留至', orderId: '訂單編號', startPayment: '啟動模擬付款', status: '查詢模擬付款狀態', paymentState: '付款狀態', orderState: '訂單狀態', refreshStatus: '重新查詢狀態', noRetry: '付款結果未知時請先查詢狀態，不可重複啟動付款。', mockMode: '模擬模式', loading: '載入中…', unavailable: '目前無法連線至本機服務。', errors: { AUTHENTICATION_REQUIRED: '請先建立或確認本機展示身份。', RESOURCE_NOT_FOUND: '找不到這筆資料；請重新整理購物車。', PRODUCT_UNAVAILABLE: '商品目前無法購買，請更新購物車。', QUOTE_CHANGED: '商品或報價已變更，請重新試算。', QUOTE_EXPIRED: '報價已過期，請重新試算。', STOCK_CHANGED: '庫存已變更，請重新整理購物車後再試。', FEE_UNAVAILABLE: '目前不支援此交付地區。', PAYMENT_UNKNOWN: '付款結果尚未確認；請查詢狀態，不要重複付款。', INVALID_TRANSITION: '目前狀態無法執行這項操作。', VERSION_CHANGED: '資料已更新，請重新載入後再操作。', PRECONDITION_REQUIRED: '版本資訊已過期，請重新載入後再操作。', VALIDATION_ERROR: '資料格式不符合展示服務的要求。', KEY_CONFLICT: '此操作的重送資料不一致，請重新載入後再操作。', SERVICE_UNAVAILABLE: '本機展示服務暫時無法使用。' }, stateNames: { Created: '尚未啟動', Processing: '處理中', Unknown: '結果待確認', Succeeded: '模擬成功', Declined: '模擬未通過', AwaitingPayment: '等待付款', Confirmed: '已確認', Cancelled: '已取消', Expired: '已到期' }
  },
  ja: {
    title: 'KOHAMI ローカルデモ', notice: '全処理はシミュレーションです。実際の支払・返金・配送・有効な請求書はありません。', login: 'ログインと本人確認', email: 'テストメール', password: 'パスワード', totp: '認証コード', result: '操作結果', identity: '現在の認証状態', action: '送信', challenge: 'Challenge ID', nonce: 'Request nonce', checkoutReference: 'チェックアウト ID', viewCheckoutResult: 'チェックアウト概要を表示',
    catalog: 'デモ商品', catalogNote: '合成データです。送信後、サーバーの価格・購入可否・在庫結果を使用します。', add: 'カートに追加', cart: 'カート', empty: 'カートは空です。', quantity: '数量', subtotal: '小計', delivery: '模擬受取方法', home: '模擬配送', pickup: '模擬店頭受取', makeQuote: '再見積もり', quote: 'チェックアウト見積もり', quoteExpires: '見積もり期限', merchandise: '商品金額', discount: '割引', shipping: '模擬送料', total: '合計', emailNote: '合成テスト用メールのみ利用できます（例: buyer at example dot test）。', confirm: 'この見積もりで模擬注文を作成し、実際の支払・配送がないことを理解しました。', placeOrder: '模擬注文を作成して在庫を確保', checkout: '模擬チェックアウト', reservationUntil: '在庫確保期限', orderId: '注文番号', startPayment: '模擬支払を開始', status: '模擬支払状態', paymentState: '支払状態', orderState: '注文状態', refreshStatus: '状態を再確認', noRetry: '結果不明の場合は状態を確認してください。支払を再開しないでください。', mockMode: '模擬モード', loading: '読み込み中…', unavailable: 'ローカルサービスに接続できません。', errors: { AUTHENTICATION_REQUIRED: 'ローカルデモの本人確認をしてください。', RESOURCE_NOT_FOUND: 'データが見つかりません。カートを再読み込みしてください。', PRODUCT_UNAVAILABLE: '商品を購入できません。カートを更新してください。', QUOTE_CHANGED: '商品または見積もりが変更されました。再見積もりしてください。', QUOTE_EXPIRED: '見積もりの期限が切れました。再見積もりしてください。', STOCK_CHANGED: '在庫が変更されました。カートを再読み込みしてください。', FEE_UNAVAILABLE: 'この地域の受取方法は利用できません。', PAYMENT_UNKNOWN: '支払結果は未確認です。状態を確認し、再実行しないでください。', INVALID_TRANSITION: '現在の状態では操作できません。', VERSION_CHANGED: 'データが更新されました。再読み込みしてください。', PRECONDITION_REQUIRED: 'バージョン情報が古くなりました。再読み込みしてください。', VALIDATION_ERROR: 'データ形式がデモサービスの要件を満たしていません。', KEY_CONFLICT: '再送データが一致しません。再読み込みしてください。', SERVICE_UNAVAILABLE: 'ローカルデモサービスを利用できません。' }, stateNames: { Created: '未開始', Processing: '処理中', Unknown: '確認待ち', Succeeded: '模擬成功', Declined: '模擬拒否', AwaitingPayment: '支払待ち', Confirmed: '確認済み', Cancelled: 'キャンセル済み', Expired: '期限切れ' }
  },
  en: {
    title: 'KOHAMI local demo', notice: 'All transactions are simulated. No real payments, refunds, deliveries or valid tax invoices.', login: 'Sign in and verification', email: 'Test email', password: 'Password', totp: 'Authenticator code', result: 'Operation result', identity: 'Current identity', action: 'Submit', challenge: 'Challenge ID', nonce: 'Request nonce', checkoutReference: 'Checkout ID', viewCheckoutResult: 'View checkout summary',
    catalog: 'Demo catalog', catalogNote: 'Synthetic data. The server response is authoritative for price, availability and stock.', add: 'Add to cart', cart: 'Cart', empty: 'Your cart is empty.', quantity: 'Quantity', subtotal: 'Subtotal', delivery: 'Mock delivery method', home: 'Mock home delivery', pickup: 'Mock pickup', makeQuote: 'Refresh quote', quote: 'Checkout quote', quoteExpires: 'Quote expires', merchandise: 'Merchandise', discount: 'Discount', shipping: 'Mock shipping', total: 'Total due', emailNote: 'Synthetic test email only, for example buyer at example dot test.', confirm: 'I confirm this quote for a mock order and understand that it creates no real payment or delivery.', placeOrder: 'Create mock order and reserve stock', checkout: 'Mock checkout', reservationUntil: 'Stock reserved until', orderId: 'Order ID', startPayment: 'Start mock payment', status: 'Mock payment status', paymentState: 'Payment state', orderState: 'Order state', refreshStatus: 'Refresh status', noRetry: 'If the payment result is unknown, check its status. Do not start payment again.', mockMode: 'Mock mode', loading: 'Loading…', unavailable: 'The local service is unavailable.', errors: { AUTHENTICATION_REQUIRED: 'Create or verify a local demo identity first.', RESOURCE_NOT_FOUND: 'This resource was not found. Reload the cart.', PRODUCT_UNAVAILABLE: 'This item is unavailable. Update your cart.', QUOTE_CHANGED: 'The item or quote changed. Request a new quote.', QUOTE_EXPIRED: 'The quote expired. Request a new quote.', STOCK_CHANGED: 'Stock changed. Reload the cart and try again.', FEE_UNAVAILABLE: 'This delivery region is not supported.', PAYMENT_UNKNOWN: 'The payment result is unconfirmed. Check status; do not retry payment.', INVALID_TRANSITION: 'This action is unavailable in the current state.', VERSION_CHANGED: 'The data changed. Reload before continuing.', PRECONDITION_REQUIRED: 'The version is stale. Reload before continuing.', VALIDATION_ERROR: 'The request does not meet the demo service requirements.', KEY_CONFLICT: 'The retry payload differs. Reload before continuing.', SERVICE_UNAVAILABLE: 'The local demo service is temporarily unavailable.' }, stateNames: { Created: 'Not started', Processing: 'Processing', Unknown: 'Unconfirmed', Succeeded: 'Mock success', Declined: 'Mock declined', AwaitingPayment: 'Awaiting payment', Confirmed: 'Confirmed', Cancelled: 'Cancelled', Expired: 'Expired' }
  }
}[locale.value]!))

useHead(() => ({ title: copy.value.title, htmlAttrs: { lang: locale.value } }))
const requestFetch = useRequestFetch()
const { data: session, refresh: refreshSession } = await useAsyncData('current-identity', () => requestFetch('/api/v1/auth/session').catch(() => ({ state: 'Unavailable', mode: 'mock' })))
const catalog = ref<Product[]>([])
const catalogLabels = computed(() => locale.value === 'zh-TW'
  ? { heading: '示意商品', note: '合成資料。送出後以伺服器回傳的價格、可購買性與庫存結果為準。', search: '搜尋名稱、SKU 或商品描述', searchAction: '搜尋商品', color: '顏色', size: '顏色與衣服尺寸', any: '不限', stock: '庫存', allStock: '全部狀態', inStock: '只看有庫存', reset: '清除篩選', view: '查看商品與尺寸表', add: '加入購物車', unavailable: '缺貨', noResults: '找不到符合條件的商品，請調整搜尋或篩選。', loadFailure: '目前無法載入商品。', loading: '正在載入商品…', more: '載入更多' }
  : locale.value === 'ja'
    ? { heading: 'デモ商品', note: '合成データです。価格・購入可否・在庫はサーバーの応答を使用します。', search: '商品名、SKU、説明を検索', searchAction: '商品を検索', color: '色', size: '色と衣服サイズ', any: '指定なし', stock: '在庫', allStock: 'すべて', inStock: '在庫ありのみ', reset: '条件をクリア', view: '商品とサイズ表を見る', add: 'カートに追加', unavailable: '在庫切れ', noResults: '該当する商品がありません。検索条件を変更してください。', loadFailure: '商品を読み込めません。', loading: '商品を読み込み中…', more: 'さらに表示' }
    : { heading: 'Demo catalog', note: 'Synthetic data. The server response is authoritative for price, availability and stock.', search: 'Search product name, SKU or description', searchAction: 'Search products', color: 'Color', size: 'Color and garment size', any: 'Any', stock: 'Stock', allStock: 'All items', inStock: 'In stock only', reset: 'Clear filters', view: 'View product and size chart', add: 'Add to cart', unavailable: 'Out of stock', noResults: 'No products match these filters. Try changing your search.', loadFailure: 'Products could not be loaded.', loading: 'Loading products…', more: 'Load more' })
const catalogLoading = ref(false), catalogError = ref(false), catalogNextCursor = ref<string | null>(null)
const catalogFilters = reactive({ q: '', colorId: '', garmentSize: '', availability: 'all' as 'all' | 'in_stock' })
let catalogRequestId = 0
const demoNames: Record<string, Record<Locale, string>> = { sku_demo_01: { 'zh-TW': '合成示意商品 A', ja: 'デモ商品 A', en: 'Demo item A' }, sku_demo_02: { 'zh-TW': '合成示意商品 B', ja: 'デモ商品 B', en: 'Demo item B' }, sku_demo_03: { 'zh-TW': '合成示意商品 C', ja: 'デモ商品 C', en: 'Demo item C' } }
const careLabels = computed(() => locale.value === 'zh-TW' ? { title: '訂單配送與售後（模擬）', load: '查詢自己的訂單', line: '訂單明細編號', reason: '原因', qty: '數量', request: '提出退貨申請', shipment: '配送編號', ret: '退貨編號', state: '下一狀態', create: '建立模擬配送', move: '更新配送狀態', accept: '受理退貨', inspect: '完成驗收', resellable: '可再售數量', receipts: 'DEMO 憑證／調整', receiptNotice: 'DEMO 憑證僅供展示，無稅務效果；不可作為有效發票。', reconciliation: '產生唯讀 mock 對帳', refund: '執行模擬退款' } : locale.value === 'ja' ? { title: '注文配送とアフターサービス（模擬）', load: '自分の注文を確認', line: '注文明細 ID', reason: '理由', qty: '数量', request: '返品を申請', shipment: '配送 ID', ret: '返品 ID', state: '次の状態', create: '模擬配送を作成', move: '配送状態を更新', accept: '返品を受理', inspect: '検品を完了', resellable: '再販可能数量', receipts: 'DEMO 証明書／調整', receiptNotice: 'DEMO 証明書は表示専用で、税務上の効力はありません。有効な請求書ではありません。', reconciliation: '読取専用 mock 照合を表示', refund: '模擬返金を実行' } : { title: 'Order delivery and aftercare (mock)', load: 'Load my order', line: 'Order line ID', reason: 'Reason', qty: 'Quantity', request: 'Request a return', shipment: 'Shipment ID', ret: 'Return ID', state: 'Next state', create: 'Create mock shipment', move: 'Update shipment state', accept: 'Accept return', inspect: 'Complete inspection', resellable: 'Resellable quantity', receipts: 'DEMO receipts/adjustments', receiptNotice: 'DEMO receipts are for display only and have no tax effect; they are not valid tax invoices.', reconciliation: 'Show read-only mock reconciliation', refund: 'Execute mock refund' })
const email = ref(''), password = ref(''), code = ref(''), challengeId = ref(''), requestNonce = ref('')
const cart = ref<Cart | null>(null), quote = ref<Quote | null>(null), checkout = ref<Checkout | null>(null), checkoutStatus = ref<CheckoutStatus | null>(null)
const deliveryMethod = ref<'mock_home' | 'mock_pickup'>('mock_home'), contactEmail = ref(''), confirmed = ref(false), checkoutId = ref('')
const result = ref<unknown>(null), reconciliationReport = ref<unknown>(null), pending = ref(false), cartLoading = ref(false), cartError = ref<ApiProblem | null>(null), checkoutError = ref<ApiProblem | null>(null)
const idempotencyKeys = new Map<string, { fingerprint: string; key: string }>()
const orderLookupId = ref(''), orderView = ref<OrderView | null>(null), receiptView = ref<ReceiptView | null>(null), orderLineId = ref(''), returnReason = ref(''), returnQuantity = ref(1)
const managedShipments = ref<Shipment[]>([])
const managedReturns = ref<ReturnAction[]>([])
const shipmentId = ref(''), returnId = ref(''), nextState = ref(''), operationReason = ref(''), resellableQuantity = ref(0), refundQuantity = ref(1)
const visibleShipments = computed(() => managedShipments.value.length ? managedShipments.value : orderView.value?.shipmentSummary?.shipments || [])
const visibleReturns = computed(() => {
  const values = new Map((orderView.value?.returns || []).map(item => [item.id, item]))
  for (const item of managedReturns.value) {
    values.set(item.id, { id: item.id, state: item.state, version: item.version, lines: [{ orderLineId: item.orderLineId, requestedQuantity: item.quantity, acceptedQuantity: item.state === 'Rejected' ? 0 : item.quantity, receivedUnitIds: [], restockedUnitIds: [], quarantinedUnitIds: [] }] })
  }
  return [...values.values()]
})

function problemOf(error: any): ApiProblem { return error?.data && typeof error.data === 'object' ? error.data as ApiProblem : { code: 'SERVICE_UNAVAILABLE' } }
function messageFor(problem: ApiProblem | null) { if (!problem) return ''; const errors = copy.value.errors as Record<string, string>; return errors[problem.code || ''] || errors.SERVICE_UNAVAILABLE }
const fulfillmentStateNames: Record<Locale, Record<string, string>> = {
  'zh-TW': { Pending: '待出貨', Shipped: '已出貨', InTransit: '運送中', Delivered: '已送達', Exception: '配送異常', Returned: '已退回', Accepted: '已受理', Rejected: '已拒絕', Received: '已收件', Inspected: '已完成驗收' },
  ja: { Pending: '出荷待ち', Shipped: '出荷済み', InTransit: '配送中', Delivered: '配達済み', Exception: '配送例外', Returned: '返送済み', Accepted: '受理済み', Rejected: '却下', Received: '受領済み', Inspected: '検品完了' },
  en: { Pending: 'Pending shipment', Shipped: 'Shipped', InTransit: 'In transit', Delivered: 'Delivered', Exception: 'Delivery exception', Returned: 'Returned', Accepted: 'Accepted', Rejected: 'Rejected', Received: 'Received', Inspected: 'Inspected' },
}
function stateName(state: string | undefined) { if (!state) return ''; const names = { ...(copy.value.stateNames as Record<string, string>), ...fulfillmentStateNames[locale.value] }; return names[state] || state }
function idempotencyKey(scope: string, body: unknown, ifMatch?: string) {
  const fingerprint = JSON.stringify([body ?? null, ifMatch ?? null])
  const existing = idempotencyKeys.get(scope)
  if (existing?.fingerprint === fingerprint) return existing.key
  const key = `storefront-${crypto.randomUUID()}`
  idempotencyKeys.set(scope, { fingerprint, key })
  return key
}
async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const method = options.method || (options.body ? 'POST' : 'GET')
  const headers: Record<string, string> = {}
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = (await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')).requestToken
  if (options.ifMatch) headers['If-Match'] = `"${options.ifMatch}"`
  if (options.idempotencyScope) headers['Idempotency-Key'] = idempotencyKey(options.idempotencyScope, options.body, options.ifMatch)
  const response = await $fetch<unknown>(`/api/v1${path}`, { method, body: options.body, headers }) as T
  if (options.idempotencyScope) idempotencyKeys.delete(options.idempotencyScope)
  return response
}
async function loadCart() {
  cartLoading.value = true; cartError.value = null
  try { cart.value = await api<Cart>('/carts/current') }
  catch (error: any) {
    const problem = problemOf(error)
    if (problem.code === 'RESOURCE_NOT_FOUND') {
      try { cart.value = await api<Cart>('/carts', { method: 'POST', body: {}, idempotencyScope: 'create-cart' }) }
      catch (createError: any) { cartError.value = problemOf(createError) }
    } else cartError.value = problem
  } finally { cartLoading.value = false }
}
async function loadCatalog(append = false) {
  const requestId = ++catalogRequestId
  catalogLoading.value = true; catalogError.value = false
  const params = new URLSearchParams({ locale: locale.value, limit: '20', sort: 'name' })
  if (catalogFilters.q) params.set('q', catalogFilters.q)
  if (catalogFilters.colorId) params.set('colorId', catalogFilters.colorId)
  if (catalogFilters.garmentSize) params.set('garmentSize', catalogFilters.garmentSize)
  if (catalogFilters.availability !== 'all') params.set('availability', catalogFilters.availability)
  if (append && catalogNextCursor.value) params.set('cursor', catalogNextCursor.value)
  try {
    const response = await api<{ items: Product[]; nextCursor: string | null }>(`/products?${params.toString()}`)
    if (requestId === catalogRequestId) {
      catalog.value = append ? [...catalog.value, ...response.items] : response.items
      catalogNextCursor.value = response.nextCursor
    }
  } catch {
    if (requestId === catalogRequestId) catalogError.value = true
  } finally {
    if (requestId === catalogRequestId) catalogLoading.value = false
  }
}
function searchCatalog(filters: { q: string; colorId: string; garmentSize: string; availability: 'all' | 'in_stock' }) {
  Object.assign(catalogFilters, filters)
  catalogNextCursor.value = null
  void loadCatalog()
}
function addCatalogSku(skuId: string) { void setCartQuantity(skuId, itemQuantity(skuId) + 1) }
function productName(product: Product | { translations: Product['translations']; skuId?: string; slug?: string } | string) {
  if (typeof product === 'string') return demoNames[product]?.[locale.value] || product
  return product.translations.find(item => item.locale === locale.value)?.name || product.translations.find(item => item.locale === 'zh-TW')?.name || product.skuId
}
function itemQuantity(skuId: string) { return cart.value?.items.find(item => item.skuId === skuId)?.quantity || 0 }
async function setCartQuantity(skuId: string, quantity: number) {
  if (!cart.value || !Number.isInteger(quantity) || quantity < 1 || quantity > 100) return
  pending.value = true; cartError.value = null; quote.value = null; checkout.value = null; checkoutStatus.value = null; confirmed.value = false
  try {
    cart.value = await api<Cart>(`/carts/current/items/${encodeURIComponent(skuId)}`, { method: 'PUT', body: { quantity }, ifMatch: cart.value.version, idempotencyScope: `cart-item-${skuId}` })
  } catch (error: any) { cartError.value = problemOf(error); if (cartError.value.code === 'VERSION_CHANGED' || cartError.value.code === 'STOCK_CHANGED') await loadCart() }
  finally { pending.value = false }
}
async function createQuote() {
  if (!cart.value?.items.length) return
  pending.value = true; checkoutError.value = null; checkout.value = null; checkoutStatus.value = null; confirmed.value = false
  try {
    quote.value = await api<Quote>('/checkout-quotes', { method: 'POST', body: { cartVersion: cart.value.version, deliveryMethod: deliveryMethod.value, region: 'demo_supported' }, ifMatch: cart.value.version, idempotencyScope: 'checkout-quote' })
  } catch (error: any) { checkoutError.value = problemOf(error); quote.value = null; if (['VERSION_CHANGED', 'QUOTE_CHANGED', 'STOCK_CHANGED'].includes(checkoutError.value.code || '')) await loadCart() }
  finally { pending.value = false }
}
async function placeOrder() {
  if (!cart.value || !quote.value || !confirmed.value || !contactEmail.value) return
  pending.value = true; checkoutError.value = null
  const body = { quoteId: quote.value.id, cartVersion: cart.value.version, contact: { email: contactEmail.value }, delivery: { method: deliveryMethod.value, region: 'demo_supported' }, confirmed: true }
  try {
    checkout.value = await api<Checkout>('/checkouts', { method: 'POST', body, ifMatch: cart.value.version, idempotencyScope: `checkout-${cart.value.id}` })
    checkoutId.value = checkout.value.checkoutId; quote.value = null; confirmed.value = false
    await loadCart()
    await loadCheckoutStatus()
  } catch (error: any) { checkoutError.value = problemOf(error); if (['VERSION_CHANGED', 'QUOTE_CHANGED', 'QUOTE_EXPIRED', 'STOCK_CHANGED'].includes(checkoutError.value.code || '')) { quote.value = null; await loadCart() } }
  finally { pending.value = false }
}
async function loadCheckoutStatus() {
  const id = checkout.value?.checkoutId || checkoutId.value.trim()
  if (!id) return
  checkoutError.value = null; pending.value = true
  try { checkoutStatus.value = await api<CheckoutStatus>(`/checkouts/${encodeURIComponent(id)}/status`); checkoutId.value = id }
  catch (error: any) { checkoutError.value = problemOf(error); checkoutStatus.value = null }
  finally { pending.value = false }
}
async function startMockPayment() {
  const current = checkoutStatus.value
  if (!current || current.paymentState === 'Unknown' || current.paymentState === 'Processing' || current.paymentState === 'Succeeded' || current.orderState !== 'AwaitingPayment') return
  pending.value = true; checkoutError.value = null
  const body = { expectedVersion: current.version }
  try {
    await api(`/checkouts/${encodeURIComponent(current.checkoutId)}/payment-attempts`, { method: 'POST', body, ifMatch: current.version, idempotencyScope: `payment-${current.checkoutId}` })
    await loadCheckoutStatus()
  } catch (error: any) { checkoutError.value = problemOf(error); if (checkoutError.value.code === 'VERSION_CHANGED') await loadCheckoutStatus() }
  finally { pending.value = false }
}
async function loadOrder() { if (!orderLookupId.value.trim()) return; pending.value = true; try { const loaded = await api<OrderView>(`/orders/${encodeURIComponent(orderLookupId.value.trim())}`); if (orderView.value?.id !== loaded.id) { managedShipments.value = []; managedReturns.value = []; shipmentId.value = ''; returnId.value = '' }; orderView.value = loaded; try { receiptView.value = await api<ReceiptView>(`/orders/${encodeURIComponent(loaded.id)}/receipts`) } catch { receiptView.value = null } } catch (error: any) { result.value = problemOf(error); orderView.value = null; receiptView.value = null; managedShipments.value = []; managedReturns.value = [] } finally { pending.value = false } }
async function loadReconciliationReport() { pending.value = true; try { reconciliationReport.value = await api('/admin/reconciliation-report') } catch (error: any) { reconciliationReport.value = problemOf(error) } finally { pending.value = false } }
function returnFor(id: string): ReturnAction | undefined { const managed = managedReturns.value.find(item => item.id === id); if (managed) return managed; const summary = orderView.value?.returns?.find(item => item.id === id), line = summary?.lines[0]; return summary && line ? { id: summary.id, state: summary.state, version: summary.version, quantity: line.requestedQuantity, orderLineId: line.orderLineId } : undefined }
async function requestReturn() { if (!orderView.value) return; const body = { orderLineId: orderLineId.value.trim(), quantity: returnQuantity.value, reason: returnReason.value }; pending.value = true; try { const created = await api<ReturnAction>(`/orders/${encodeURIComponent(orderView.value.id)}/returns`, { method: 'POST', body, idempotencyScope: `return-${orderView.value.id}-${body.orderLineId}` }); managedReturns.value = [...managedReturns.value.filter(item => item.id !== created.id), created]; returnId.value = created.id; result.value = created; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }
async function createShipment() { if (!orderView.value) return; const body = { expectedVersion: orderView.value.version }; pending.value = true; try { const shipment = await api<Shipment>(`/admin/orders/${encodeURIComponent(orderView.value.id)}/shipments`, { method: 'POST', body, ifMatch: orderView.value.version, idempotencyScope: `shipment-${orderView.value.id}` }); managedShipments.value = [...managedShipments.value.filter(item => item.id !== shipment.id), shipment]; shipmentId.value = shipment.id; result.value = shipment; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }
async function transitionShipment() { const id = shipmentId.value.trim(); const shipment = managedShipments.value.find(x => x.id === id) || orderView.value?.shipmentSummary?.shipments?.find(x => x.id === id); if (!shipment) return; const body = { state: nextState.value, eventId: crypto.randomUUID(), expectedVersion: shipment.version, reason: operationReason.value }; pending.value = true; try { const updated = await api<Shipment>(`/admin/shipments/${encodeURIComponent(shipment.id)}/transitions`, { method: 'POST', body, ifMatch: shipment.version, idempotencyScope: `shipment-state-${shipment.id}-${body.eventId}` }); managedShipments.value = [...managedShipments.value.filter(item => item.id !== updated.id), updated]; result.value = updated; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }
async function transitionReturn(state: 'Accepted' | 'Rejected' | 'Received') { const ret = returnFor(returnId.value.trim()); if (!ret) return; const body = { state, expectedVersion: ret.version, reason: operationReason.value }; pending.value = true; try { const updated = await api<ReturnAction>(`/admin/returns/${encodeURIComponent(ret.id)}/transitions`, { method: 'POST', body, ifMatch: ret.version, idempotencyScope: `return-state-${ret.id}-${body.state}` }); managedReturns.value = [...managedReturns.value.filter(item => item.id !== updated.id), { ...ret, ...updated }]; result.value = updated; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }
async function inspectReturn() { const ret = returnFor(returnId.value.trim()); if (!ret) return; const body = { resellableQuantity: resellableQuantity.value, reason: operationReason.value, expectedVersion: ret.version }; pending.value = true; try { const inspected = await api<ReturnAction>(`/admin/returns/${encodeURIComponent(ret.id)}/inspections`, { method: 'POST', body, ifMatch: ret.version, idempotencyScope: `return-inspection-${ret.id}` }); managedReturns.value = [...managedReturns.value.filter(item => item.id !== inspected.id), { ...ret, ...inspected }]; result.value = inspected; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }
async function requestRefund() { const ret = returnFor(returnId.value.trim()), info = managedReturns.value.find(item => item.id === returnId.value.trim())?.inspection; if (!ret || !info?.refundCaseId || !info.refundCaseVersion || !code.value) return; const line = orderView.value?.snapshot?.lines.find(x => x.orderLineId === ret.orderLineId); if (!line || refundQuantity.value < 1 || refundQuantity.value > ret.quantity) return; const refundUnits = (info.refundUnits || []).slice(0, refundQuantity.value); if (refundUnits.length !== refundQuantity.value) return; const amountTwd = refundUnits.reduce((sum, unit) => sum + unit.paidTwd, 0); pending.value = true; try { const context = await $fetch<{ requestNonce: string }>('/api/v1/auth/account-context'); const challenge = await api<{ challengeId: string; requestNonce: string }>('/auth/reauthentication-challenges', { method: 'POST', body: { purpose: 'refund.execute', resourceKind: 'RefundCase', resourceId: info.refundCaseId, requestNonce: context.requestNonce } }); await api('/auth/reauthentications', { method: 'POST', body: { challengeId: challenge.challengeId, code: code.value, requestNonce: challenge.requestNonce } }); const body = { lines: [{ orderLineId: ret.orderLineId, quantity: refundQuantity.value }], refundShipping: false, amountTwd, reason: operationReason.value || returnReason.value, expectedVersion: info.refundCaseVersion }; result.value = await api(`/admin/refund-cases/${encodeURIComponent(info.refundCaseId)}/operations`, { method: 'POST', body, ifMatch: info.refundCaseVersion, idempotencyScope: `refund-${info.refundCaseId}` }); code.value = ''; await loadOrder() } catch (error: any) { result.value = problemOf(error) } finally { pending.value = false } }

async function invoke(path: string, body?: Record<string, unknown>, method?: 'DELETE') {
  pending.value = true
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    const response = await $fetch<{ mfaChallengeId?: string | null }>(`/api/v1${path}`, { method: method || (body ? 'POST' : 'GET'), body, headers: body || method ? { 'X-CSRF-TOKEN': csrf.requestToken } : undefined })
    result.value = response
    if (path === '/auth/sessions') challengeId.value = response.mfaChallengeId || ''
    password.value = ''; code.value = ''; await refreshSession()
    if (path === '/auth/sessions') { const context = await $fetch<{ requestNonce?: string }>('/api/v1/auth/account-context'); requestNonce.value = context.requestNonce || ''; await loadCart() }
    if (method === 'DELETE') { result.value = null; requestNonce.value = ''; challengeId.value = ''; checkoutId.value = ''; cart.value = null; quote.value = null; checkout.value = null; checkoutStatus.value = null }
  } catch (error: any) { result.value = error.data || { code: 'SERVICE_UNAVAILABLE' } }
  finally { pending.value = false }
}
function clearPrivateView() { result.value = null; password.value = ''; code.value = ''; void refreshSession() }
onMounted(() => { window.addEventListener('pageshow', clearPrivateView); void loadCatalog(); void loadCart() })
onBeforeUnmount(() => window.removeEventListener('pageshow', clearPrivateView))
watch(locale, () => { result.value = null; void loadCatalog() })
watch(deliveryMethod, () => { quote.value = null; confirmed.value = false })
</script>

<template>
  <main>
    <header>
      <h1>{{ copy.title }}</h1>
      <p class="notice">{{ copy.notice }}</p>
      <nav aria-label="Language"><NuxtLink v-for="lang in ['zh-TW', 'ja', 'en']" :key="lang" :to="{ query: { ...route.query, lang } }">{{ lang }}</NuxtLink></nav>
      <p><NuxtLink to="/admin/catalog">商品目錄管理</NuxtLink></p>
    </header>

    <ProductDiscovery :items="catalog" :locale="locale" :loading="catalogLoading" :error="catalogError" :next-cursor="catalogNextCursor" :cart-ready="Boolean(cart)" :pending="pending" :labels="catalogLabels" @search="searchCatalog" @retry="loadCatalog()" @load-more="loadCatalog(true)" @add-to-cart="addCatalogSku" />

    <section aria-labelledby="cart-heading">
      <h2 id="cart-heading">{{ copy.cart }}</h2>
      <p v-if="cartLoading" role="status">{{ copy.loading }}</p>
      <p v-else-if="cartError" class="error" role="alert">{{ messageFor(cartError) }}</p>
      <p v-else-if="!cart?.items.length">{{ copy.empty }}</p>
      <template v-if="cart?.items.length">
        <ul class="cart-list">
          <li v-for="item in cart.items" :key="item.skuId">
            <div><strong>{{ productName(item.skuId) || item.name?.value || item.skuId }}</strong><small>{{ item.skuId }} · TWD {{ item.unitPriceTwd }} {{ item.availability === 'OutOfStock' ? '· Out of stock' : '' }}</small></div>
            <label>{{ copy.quantity }}<input :value="item.quantity" type="number" min="1" max="100" :disabled="pending" @change="setCartQuantity(item.skuId, Number(($event.target as HTMLInputElement).value))" /></label>
            <span>{{ copy.subtotal }}: TWD {{ item.unitPriceTwd * item.quantity }}</span>
          </li>
        </ul>
        <label>{{ copy.delivery }}
          <select v-model="deliveryMethod" :disabled="pending"><option value="mock_home">{{ copy.home }}</option><option value="mock_pickup">{{ copy.pickup }}</option></select>
        </label>
        <button :disabled="pending || !cart.items.length" @click="createQuote">{{ copy.makeQuote }}</button>
        <p v-if="cartError" class="error" role="alert">{{ messageFor(cartError) }}</p>
      </template>
    </section>

    <section v-if="quote" aria-labelledby="quote-heading">
      <h2 id="quote-heading">{{ copy.quote }}</h2>
      <dl class="totals">
        <dt>{{ copy.merchandise }}</dt><dd>TWD {{ quote.snapshot.merchandiseTwd }}</dd>
        <dt>{{ copy.discount }}</dt><dd>− TWD {{ quote.snapshot.discountTwd }}</dd>
        <dt>{{ copy.shipping }}</dt><dd>TWD {{ quote.snapshot.shippingTwd }}</dd>
        <dt class="grand-total">{{ copy.total }} ({{ quote.snapshot.currency }})</dt><dd class="grand-total">TWD {{ quote.snapshot.totalTwd }}</dd>
      </dl>
      <p>{{ copy.quoteExpires }}: <time :datetime="quote.expiresAt">{{ new Date(quote.expiresAt).toLocaleString(locale) }}</time></p>
      <label>{{ copy.email }}<input v-model="contactEmail" type="email" maxlength="254" autocomplete="email" required /></label>
      <small>{{ copy.emailNote }}</small>
      <label class="check"><input v-model="confirmed" type="checkbox" /> {{ copy.confirm }}</label>
      <button :disabled="pending || !confirmed || !contactEmail" @click="placeOrder">{{ copy.placeOrder }}</button>
      <p v-if="checkoutError" class="error" role="alert">{{ messageFor(checkoutError) }}</p>
    </section>

    <section aria-labelledby="checkout-heading">
      <h2 id="checkout-heading">{{ copy.checkout }}</h2>
      <template v-if="checkoutStatus">
        <p><strong>{{ copy.mockMode }}:</strong> mock</p>
        <p v-if="checkout">{{ copy.orderId }}: <code>{{ checkout.orderId }}</code></p>
        <p>{{ copy.checkoutReference }}: <code>{{ checkoutStatus.checkoutId }}</code></p>
        <p>{{ copy.orderState }}: {{ stateName(checkoutStatus.orderState) }} · {{ copy.paymentState }}: {{ stateName(checkoutStatus.paymentState) }}</p>
        <dl class="totals"><dt>{{ copy.total }}</dt><dd>TWD {{ checkoutStatus.snapshot.totalTwd }}</dd></dl>
        <NuxtLink :to="{ path: `/checkout/${checkoutStatus.checkoutId}`, query: { lang: locale } }">{{ copy.viewCheckoutResult }}</NuxtLink>
        <p>{{ copy.reservationUntil }}: <time :datetime="checkoutStatus.expiresAt">{{ new Date(checkoutStatus.expiresAt).toLocaleString(locale) }}</time></p>
        <p v-if="checkoutStatus.paymentState === 'Unknown'" class="notice">{{ copy.noRetry }}</p>
        <div class="actions">
          <button v-if="checkoutStatus.orderState === 'AwaitingPayment' && !['Processing', 'Unknown', 'Succeeded'].includes(checkoutStatus.paymentState)" :disabled="pending" @click="startMockPayment">{{ copy.startPayment }}</button>
          <button class="secondary" :disabled="pending" @click="loadCheckoutStatus">{{ copy.refreshStatus }}</button>
        </div>
      </template>
      <form v-else @submit.prevent="loadCheckoutStatus">
        <label>{{ copy.checkout }} ID<input v-model="checkoutId" required /></label>
        <button :disabled="pending">{{ copy.status }}</button>
      </form>
      <p v-if="checkoutError" class="error" role="alert">{{ messageFor(checkoutError) }} <small v-if="checkoutError.traceId">{{ checkoutError.traceId }}</small></p>
    </section>

    <section aria-labelledby="care-heading">
      <h2 id="care-heading">{{ careLabels.title }}</h2>
      <form @submit.prevent="loadOrder"><label>{{ copy.orderId }}<input v-model="orderLookupId" required /></label><button :disabled="pending">{{ careLabels.load }}</button></form>
      <template v-if="orderView">
        <p>{{ copy.orderState }}: {{ orderView.id }} · {{ stateName(orderView.shipmentSummary?.state || visibleShipments.at(-1)?.state || 'NotCreated') }}</p>
        <p>{{ careLabels.line }}: {{ orderView.snapshot?.lines?.map(line => `${line.orderLineId} (${line.skuId} × ${line.quantity})`).join(' · ') }}</p>
        <p>{{ careLabels.receipts }}</p>
        <p v-if="receiptView?.taxEffect === 'none'" class="notice">{{ careLabels.receiptNotice }}</p>
        <ul v-if="receiptView?.receipts?.length || receiptView?.adjustments?.length">
          <li v-for="receipt in receiptView?.receipts || []" :key="receipt.id">{{ receipt.id }} · {{ stateName(receipt.state) }} · TWD {{ receipt.amountTwd }}</li>
          <li v-for="adjustment in receiptView?.adjustments || []" :key="adjustment.id">{{ adjustment.id }} · {{ stateName(adjustment.state) }} · TWD {{ adjustment.amountTwd }}</li>
        </ul>
        <div class="actions"><button :disabled="pending" @click="loadReconciliationReport">{{ careLabels.reconciliation }}</button></div>
        <pre v-if="reconciliationReport">{{ JSON.stringify(reconciliationReport, null, 2) }}</pre>
        <form @submit.prevent="requestReturn"><label>{{ careLabels.line }}<input v-model="orderLineId" required /></label><label>{{ careLabels.qty }}<input v-model.number="returnQuantity" type="number" min="1" max="100" required /></label><label>{{ careLabels.reason }}<input v-model="returnReason" maxlength="500" required /></label><button :disabled="pending">{{ careLabels.request }}</button></form>
        <p>{{ visibleReturns.map(item => `${item.id}: ${stateName(item.state)}`).join(' · ') }}</p>
        <p>{{ visibleShipments.map(item => `${item.id}: ${stateName(item.state)}`).join(' · ') }}</p>
        <div class="actions"><button :disabled="pending" @click="createShipment">{{ careLabels.create }}</button></div>
        <label>{{ careLabels.shipment }}<input v-model="shipmentId" /></label>
        <label>{{ careLabels.state }}<select v-model="nextState"><option v-for="state in ['Shipped', 'InTransit', 'Delivered', 'Exception', 'Returned', 'Accepted', 'Rejected', 'Received']" :key="state" :value="state">{{ stateName(state) }}</option></select></label>
        <label>{{ careLabels.reason }}<input v-model="operationReason" maxlength="500" /></label>
        <div class="actions"><button :disabled="pending || !shipmentId" @click="transitionShipment">{{ careLabels.move }}</button></div>
        <label>{{ careLabels.ret }}<input v-model="returnId" /></label>
        <div class="actions"><button :disabled="pending || !returnId" @click="transitionReturn('Accepted')">{{ careLabels.accept }}</button><button :disabled="pending || !returnId" @click="transitionReturn('Received')">{{ stateName('Received') }}</button><button :disabled="pending || !returnId" @click="transitionReturn('Rejected')">{{ stateName('Rejected') }}</button><button :disabled="pending || !returnId" @click="inspectReturn">{{ careLabels.inspect }}</button></div>
        <label>{{ careLabels.resellable }}<input v-model.number="resellableQuantity" type="number" min="0" max="100" /></label>
        <label>{{ copy.totp }}<input v-model="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" /></label>
        <label>{{ careLabels.qty }}<input v-model.number="refundQuantity" type="number" min="1" max="100" /></label>
        <div class="actions"><button :disabled="pending || !returnId || !code" @click="requestRefund">{{ careLabels.refund }}</button></div>
      </template>
      <hr />
    </section>

    <section aria-labelledby="identity-heading">
      <h2 id="identity-heading">{{ copy.identity }}</h2>
      <pre>{{ session }}</pre>
      <button class="secondary" :disabled="pending" @click="invoke('/auth/session', undefined, 'DELETE')">{{ locale === 'zh-TW' ? '登出' : locale === 'ja' ? 'ログアウト' : 'Sign out' }}</button>
    </section>

    <section aria-labelledby="login-heading">
      <h2 id="login-heading">{{ copy.login }}</h2>
      <form @submit.prevent="invoke('/auth/sessions', { email, password })"><label>{{ copy.email }}<input v-model="email" type="email" autocomplete="username" required /></label><label>{{ copy.password }}<input v-model="password" type="password" autocomplete="current-password" required /></label><button :disabled="pending">{{ copy.action }}</button></form>
      <form @submit.prevent="invoke('/auth/mfa-verifications', { challengeId, code, requestNonce })"><label>{{ copy.challenge }}<input v-model="challengeId" required /></label><label>{{ copy.nonce }}<input v-model="requestNonce" required /></label><label>{{ copy.totp }}<input v-model="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" required /></label><button :disabled="pending">{{ copy.action }}</button></form>
    </section>

    <section aria-labelledby="result-heading"><h2 id="result-heading">{{ copy.result }}</h2><pre role="status" aria-live="polite">{{ result }}</pre></section>
  </main>
</template>

<style>
:root{font-family:system-ui,sans-serif;color:#25252c;background:#faf8f5}main{max-width:74rem;margin:auto;padding:1.5rem}section{background:white;padding:1.25rem;margin:1rem 0;border:1px solid #b0a7a0;border-radius:.5rem}nav{display:flex;gap:1rem}.notice{border-left:4px solid #8b3952;padding:.75rem;background:#fbf4f6}.error{border-left:4px solid #a11919;padding:.75rem;background:#fff3f3;color:#711010}.cart-list{list-style:none;padding:0}.cart-list li{display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:.75rem 0;border-bottom:1px solid #e8e2de}.cart-list li>div{display:grid;gap:.25rem;flex:1}.cart-list small{color:#625c58}.product-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(14rem,1fr));gap:1rem}.product-card{border:1px solid #e8e2de;border-radius:.5rem;padding:.75rem;background:#fff}.product-card h3{margin:.75rem 0 .25rem;font-size:1rem}.product-image-link{display:grid;gap:.4rem;color:#8b3952}.product-image-link img{width:100%;aspect-ratio:1;object-fit:cover;background:#f4f2ed;border-radius:.35rem}.product-price{font-weight:700}.totals{display:grid;grid-template-columns:1fr auto;gap:.5rem}.totals dd{margin:0;text-align:right}.grand-total{font-weight:700;font-size:1.1rem;border-top:1px solid #aaa;padding-top:.6rem}.actions{display:flex;gap:.75rem;flex-wrap:wrap}label{display:block;margin:.75rem 0}input,select{display:block;width:100%;max-width:28rem;box-sizing:border-box;padding:.65rem;font:inherit}input[type=checkbox]{display:inline;width:auto}.check{display:flex;align-items:flex-start;gap:.5rem;max-width:42rem}button{padding:.7rem 1.2rem;background:#6d2942;color:white;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.secondary{background:#4b5360}pre{white-space:pre-wrap;overflow-wrap:anywhere}:focus-visible{outline:3px solid #235b9d;outline-offset:3px}@media(max-width:38rem){main{padding:.75rem}.cart-list li{align-items:flex-start;flex-direction:column}.cart-list input{max-width:8rem}.product-grid{grid-template-columns:repeat(auto-fill,minmax(10rem,1fr))}}
</style>
