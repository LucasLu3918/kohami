// Repository-owned synthetic catalog. Never imported from a database or private fixture.
const seeds = [
  ['linen-shirt', '棉麻襯衫', 'リネンシャツ', 'Linen shirt', 980],
  ['cotton-tee', '純棉上衣', 'コットンTシャツ', 'Cotton tee', 680],
  ['poplin-blouse', '柔棉襯衫', 'ポプリンブラウス', 'Poplin blouse', 1080],
  ['textured-top', '織紋上衣', '表情素材トップス', 'Textured top', 880],
  ['knit-vest', '針織背心', 'ニットベスト', 'Knit vest', 980],
  ['rib-knit-cardigan', '羅紋針織外套', 'リブカーディガン', 'Rib knit cardigan', 1280],
  ['mock-neck-knit', '高領針織上衣', 'ハイネックニット', 'Mock neck knit', 1180],
  ['shirt-dress', '襯衫洋裝', 'シャツワンピース', 'Shirt dress', 1580],
  ['wrap-midi-dress', '繫帶洋裝', 'ラップワンピース', 'Wrap midi dress', 1680],
  ['pleated-midi-skirt', '百褶裙', 'プリーツスカート', 'Pleated midi skirt', 1380],
  ['wide-leg-trousers', '寬版長褲', 'ワイドパンツ', 'Wide leg trousers', 1280],
  ['linen-shorts', '棉麻短褲', 'リネンショートパンツ', 'Linen shorts', 880],
  ['straight-denim', '直筒丹寧褲', 'ストレートデニム', 'Straight denim', 1480],
  ['cropped-jacket', '短版外套', 'ショートジャケット', 'Cropped jacket', 1880],
  ['wool-coat', '毛呢大衣', 'ウールコート', 'Wool coat', 2880],
] as const

const products = seeds.map(([key, zh, ja, en, price]) => ({
  id: `synthetic-${key}`, slug: `demo-${key}`, mode: 'mock' as const,
  translations: [
    { locale: 'zh-TW', name: zh, description: '合成展示商品；結帳與付款僅在本機模擬。', material: '展示用合成材質說明' },
    { locale: 'ja', name: ja, description: '合成サンプル商品です。注文と支払いはローカルでのみ模擬します。', material: 'サンプル素材情報' },
    { locale: 'en', name: en, description: 'Synthetic demo item. Checkout and payment are simulated locally.', material: 'Sample material information' },
  ],
  media: [{ media: { url: `/demo-assets/products/demo-${key}.png`, alt: { value: zh } } }],
  measurements: ['S', 'M', 'L'].map((size, index) => ({ name: `${size}-body-length`, valueMm: 600 + index * 20, synthetic: true })),
  skus: ['S', 'M', 'L'].map(size => ({ skuId: `synthetic-${key}-${size}`, colorId: 'oatmeal', garmentSize: size, priceTwd: price, stock: 10, availability: 'InStock' })),
}))

export const demoProductSlugs = products.map(product => product.slug)

const STORE_KEY = 'kohami.frontend-demo.v1'
const TEST_EMAIL = 'demo@test.com'
const TEST_PASSWORD = 'Kohami-Member-Demo-2026!'
const TEST_CARD = '4111111111111111'
type MockLine = { skuId: string; productSlug: string; name: { value: string }; colorId: string; garmentSize: string; quantity: number; unitPriceTwd: number; paidTwd: number }
type MockOrder = {
  id: string; checkoutId: string; version: number; placedAt: string; expiresAt: string
  orderState: 'AwaitingPayment' | 'Confirmed' | 'Cancelled'; paymentState: 'Created' | 'Succeeded' | 'Failed'
  paymentMethod: 'wallet' | 'debit_card' | 'credit_card'; compensationRequired: boolean
  snapshot: { currency: 'TWD'; merchandiseTwd: number; discountTwd: number; shippingTwd: number; totalTwd: number; lines: MockLine[] }
}
type MockStore = {
  authenticated: boolean; adminAuthenticated: boolean; cart: { id: string; version: number; items: Array<{ skuId: string; quantity: number; unitPriceTwd: number; availability: string }> } | null
  quote: { id: string; cartVersion: number; deliveryMethod: string; promotionCode: string | null; expiresAt: string; snapshot: MockOrder['snapshot'] } | null
  orders: MockOrder[]; walletTwd: number; walletEntries: Array<{ id: string; type: string; amountTwd: number; currency: 'TWD'; sourceId: string; createdAt: string }>
  stock: Record<string, number>; idempotency: Record<string, { fingerprint: string; result: unknown }>
}

function emptyStore(): MockStore {
  return { authenticated: false, adminAuthenticated: false, cart: null, quote: null, orders: [], walletTwd: 240, walletEntries: [], stock: {}, idempotency: {} }
}
function readStore(): MockStore {
  if (typeof window === 'undefined') return emptyStore()
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORE_KEY) || 'null') as Partial<MockStore> | null
    return { ...emptyStore(), ...parsed, stock: parsed?.stock || {}, idempotency: parsed?.idempotency || {} }
  } catch { return emptyStore() }
}
function saveStore(store: MockStore) {
  if (typeof window !== 'undefined') window.localStorage.setItem(STORE_KEY, JSON.stringify(store))
}
function id(prefix: string) { return `${prefix}-${globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(36).slice(2)}`}` }
function problem(status: number, code: string) { return Response.json({ type: 'about:blank', title: '展示模式無法完成操作', status, code }, { status }) }
export function demoProblem(status: number, code: string) { return problem(status, code) }
function requestBody(body?: BodyInit | null): Record<string, any> {
  if (typeof body !== 'string') return {}
  try { return JSON.parse(body) as Record<string, any> } catch { return {} }
}
function header(headers: HeadersInit | undefined, name: string) {
  if (!headers) return ''
  if (headers instanceof Headers) return headers.get(name) || ''
  if (Array.isArray(headers)) return headers.find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] || ''
  return Object.entries(headers).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] || ''
}
function version(value: number) { return `cart-v${value}` }
function cartResponse(store: MockStore) { return { ...store.cart!, version: version(store.cart!.version), promotionCode: (store.cart as any).promotionCode || null, items: store.cart!.items.map(item => ({ ...item })) } }
function stockOf(store: MockStore, skuId: string) {
  const fixture = products.flatMap(product => product.skus).find(sku => sku.skuId === skuId)
  return fixture ? (store.stock[skuId] ?? fixture.stock) : 0
}
function skuInfo(skuId: string) {
  for (const product of products) {
    const sku = product.skus.find(item => item.skuId === skuId)
    if (sku) return { product, sku }
  }
  return null
}
function storeOrder(order: MockOrder) {
  return { id: order.id, checkoutId: order.checkoutId, mode: 'mock', version: `order-v${order.version}`, placedAt: order.placedAt, orderState: order.orderState, snapshot: order.snapshot }
}
function checkoutResponse(order: MockOrder) {
  return { checkoutId: order.checkoutId, orderId: order.id, mode: 'mock', expiresAt: order.expiresAt, snapshot: order.snapshot }
}
function checkoutStatus(order: MockOrder) {
  return { checkoutId: order.checkoutId, version: `checkout-v${order.version}`, orderState: order.orderState, paymentState: order.paymentState, paymentMethod: order.paymentMethod, compensationRequired: order.compensationRequired, expiresAt: order.expiresAt, snapshot: order.snapshot }
}
function memberOrderDetail(order: MockOrder) {
  return {
    id: order.id, checkoutId: order.checkoutId, version: `order-v${order.version}`, orderState: order.orderState,
    snapshot: { currency: 'TWD', lines: order.snapshot.lines.map((line, index) => ({ ...line, orderLineId: `${order.id}-line-${index + 1}` })) },
    actions: [{ action: 'Cancel', allowed: order.orderState === 'AwaitingPayment', reasonCodes: [] }, { action: 'RequestReturn', allowed: false, reasonCodes: ['NOT_DELIVERED'] }],
    paymentSummary: { state: order.paymentState, confirmedAt: order.paymentState === 'Succeeded' ? order.placedAt : null, confirmedCollectedTwd: order.paymentState === 'Succeeded' ? order.snapshot.totalTwd : 0, compensationRequired: order.compensationRequired },
    shipmentSummary: { state: 'NotShipped', shipments: [] },
    refundSummary: { requestedCount: 0, processingCount: 0, unknownCount: 0, succeededCount: 0, failedCount: 0, succeededTwd: 0, pendingTwd: 0 },
    receiptSummary: { state: order.paymentState === 'Succeeded' ? 'Completed' : 'NotIssued', pendingAdjustmentCount: 0 },
    returns: [],
  }
}

function productsResponse(url: URL, store: MockStore) {
  const requested = url.searchParams.get('locale') || 'zh-TW'
  const locale = ['zh-TW', 'ja', 'en'].includes(requested) ? requested : 'zh-TW'
  const query = (url.searchParams.get('q') || '').toLocaleLowerCase()
  const rows = products.flatMap(product => product.skus.map(sku => ({
    ...sku, stock: stockOf(store, sku.skuId), availability: stockOf(store, sku.skuId) > 0 ? 'InStock' : 'OutOfStock',
    productId: product.id, slug: product.slug, mode: product.mode, translations: product.translations, media: product.media,
  }))).filter(row => {
    const translation = row.translations.find(item => item.locale === locale) || row.translations[0]!
    return `${translation.name} ${translation.description}`.toLocaleLowerCase().includes(query)
      && (!url.searchParams.get('colorId') || row.colorId === url.searchParams.get('colorId'))
      && (!url.searchParams.get('garmentSize') || row.garmentSize === url.searchParams.get('garmentSize'))
      && (url.searchParams.get('availability') !== 'in_stock' || row.availability === 'InStock')
  }).sort((a, b) => a.translations.find(item => item.locale === locale)!.name.localeCompare(b.translations.find(item => item.locale === locale)!.name, locale))
  const limit = Math.max(1, Math.min(100, Number(url.searchParams.get('limit')) || 20))
  const offset = Math.max(0, Number(url.searchParams.get('cursor')) || 0)
  return { items: rows.slice(offset, offset + limit), nextCursor: offset + limit < rows.length ? String(offset + limit) : null }
}

export function mockApiResponse(request: string, method = 'GET', rawBody?: BodyInit | null, headers?: HeadersInit, readOnly = false): Response {
  const url = new URL(request, 'https://frontend.invalid')
  const path = url.pathname
  const body = requestBody(rawBody)
  const store = readStore()
  const publicRead = method === 'GET' && (path === '/api/v1/auth/session' || path === '/api/v1/auth/csrf' || path === '/api/v1/products' || path.startsWith('/api/v1/products/'))
  if (readOnly && !publicRead) return problem(403, 'DEMO_READ_ONLY')
  const idempotencyKey = header(headers, 'Idempotency-Key')
  const idempotent = <T>(scope: string, payload: unknown, run: () => T): T => {
    const fingerprint = JSON.stringify(payload)
    const previous = store.idempotency[scope]
    if (previous) {
      if (previous.fingerprint !== fingerprint) throw Object.assign(new Error('KEY_CONFLICT'), { status: 409, code: 'KEY_CONFLICT' })
      return previous.result as T
    }
    const result = run()
    store.idempotency[scope] = { fingerprint, result }
    return result
  }
  try {
    if (path === '/api/v1/auth/csrf' && method === 'GET') return Response.json({ requestToken: 'frontend-demo-csrf' })
    if (path === '/api/v1/auth/session' && method === 'GET') return Response.json({ state: store.adminAuthenticated ? 'AdminAuthenticated' : store.authenticated ? 'MemberAuthenticated' : 'Anonymous', mode: 'mock', userId: store.adminAuthenticated ? 'demo-admin' : store.authenticated ? 'demo-member' : null, email: store.adminAuthenticated ? 'demo-admin@test.com' : store.authenticated ? TEST_EMAIL : null, permissions: store.adminAuthenticated ? ['catalog.read', 'reports.read', 'user.manage'] : [], expiresAt: null })
    if (path === '/api/v1/auth/session' && method === 'DELETE') { store.authenticated = false; store.adminAuthenticated = false; saveStore(store); return Response.json({ state: 'Anonymous', mode: 'mock' }) }
    if (path === '/api/v1/auth/sessions' && method === 'POST') {
      const email = body.email?.trim().toLowerCase()
      if (email === 'demo-admin@test.com' && body.password === 'Kohami-Admin-Demo-2026!') {
        store.authenticated = false; store.adminAuthenticated = true; saveStore(store)
        return Response.json({ state: 'AdminAuthenticated', mode: 'mock', userId: 'demo-admin', email, permissions: ['catalog.read', 'reports.read', 'user.manage'] })
      }
      if (email !== TEST_EMAIL || body.password !== TEST_PASSWORD) return problem(401, 'INVALID_CREDENTIALS')
      store.authenticated = true; store.adminAuthenticated = false; saveStore(store)
      return Response.json({ state: 'MemberAuthenticated', mode: 'mock', userId: 'demo-member', email: TEST_EMAIL, permissions: [] })
    }
    if (path === '/api/v1/auth/registrations' && method === 'POST') return problem(403, 'DEMO_REGISTRATION_DISABLED_USE_TEST_ACCOUNT')
    if (path === '/api/v1/products' && method === 'GET') return Response.json(productsResponse(url, store))
    if (path.startsWith('/api/v1/products/') && method === 'GET') {
      const product = products.find(item => item.slug === decodeURIComponent(path.slice('/api/v1/products/'.length)))
      return product ? Response.json({ ...product, skus: product.skus.map(sku => ({ ...sku, stock: stockOf(store, sku.skuId), availability: stockOf(store, sku.skuId) > 0 ? 'InStock' : 'OutOfStock' })) }) : problem(404, 'RESOURCE_NOT_FOUND')
    }
    if (path === '/api/v1/admin/products' && method === 'GET') {
      if (!store.adminAuthenticated) return problem(403, 'FORBIDDEN')
      const items = products.map(product => ({ id: product.id, slug: product.slug, state: 'Published', translations: product.translations, media: product.media, skus: product.skus.map(sku => ({ ...sku, stock: stockOf(store, sku.skuId), availability: stockOf(store, sku.skuId) > 0 ? 'InStock' : 'OutOfStock' })) }))
      return Response.json({ mode: 'mock', items })
    }
    const adminProduct = path.match(/^\/api\/v1\/admin\/products\/([^/]+)$/)
    if (adminProduct && method === 'GET') {
      if (!store.adminAuthenticated) return problem(403, 'FORBIDDEN')
      const product = products.find(item => item.id === decodeURIComponent(adminProduct[1]!))
      return product ? Response.json({ id: product.id, slug: product.slug, state: 'Published', translations: product.translations, media: product.media, skus: product.skus.map(sku => ({ ...sku, stock: stockOf(store, sku.skuId), availability: stockOf(store, sku.skuId) > 0 ? 'InStock' : 'OutOfStock' })) }) : problem(404, 'RESOURCE_NOT_FOUND')
    }
    if (path === '/api/v1/admin/reports' && method === 'GET') {
      if (!store.adminAuthenticated) return problem(403, 'FORBIDDEN')
      const from = url.searchParams.get('from') || ''
      const to = url.searchParams.get('to') || ''
      const rows = store.orders.filter(order => order.paymentState === 'Succeeded').map(order => ({
        date: order.placedAt.slice(0, 10), sourceKind: 'Primary', orderCount: 1, collectedTwd: order.snapshot.totalTwd,
        succeededRefundTwd: 0, pendingTwd: 0, availableTwd: order.snapshot.totalTwd, netConfirmedTwd: order.snapshot.totalTwd, discrepancyCount: 0,
      })).filter(row => row.date >= from && row.date <= to)
      return Response.json({ mode: 'mock', cutoff: new Date().toISOString(), rows, notice: '合成交易摘要，不是正式財務報表或銀行對帳結果。' })
    }
    if (path === '/api/v1/admin/wallet-credits' && method === 'POST') {
      if (!store.adminAuthenticated) return problem(403, 'FORBIDDEN')
      const amount = Number(body.amountTwd)
      if (body.memberUserId !== 'demo-member' || !Number.isInteger(amount) || amount < 1 || amount > 50_000 || typeof body.reason !== 'string' || !body.reason.trim()) return problem(422, 'VALIDATION_ERROR')
      const result = idempotent(`admin-credit:${idempotencyKey || body.memberUserId}`, { memberUserId: body.memberUserId, amount, reason: body.reason.trim() }, () => {
        store.walletTwd += amount
        store.walletEntries.unshift({ id: id('wallet-entry'), type: 'AdminCredit', amountTwd: amount, currency: 'TWD', sourceId: 'demo-admin-credit', createdAt: new Date().toISOString() })
        return { amountTwd: amount, availableTwd: store.walletTwd, mode: 'mock' }
      })
      saveStore(store); return Response.json(result, { status: 201 })
    }
    if (path === '/api/v1/carts/current' && method === 'GET') return store.cart ? Response.json(cartResponse(store)) : problem(404, 'RESOURCE_NOT_FOUND')
    if (path === '/api/v1/carts' && method === 'POST') {
      if (!store.cart) store.cart = { id: id('cart'), version: 1, items: [] }
      saveStore(store); return Response.json(cartResponse(store), { status: 201 })
    }
    const cartItem = path.match(/^\/api\/v1\/carts\/current\/items\/([^/]+)$/)
    if (cartItem && ['PUT', 'DELETE'].includes(method)) {
      if (!store.cart) return problem(404, 'RESOURCE_NOT_FOUND')
      const skuId = decodeURIComponent(cartItem[1]!)
      const currentVersion = Number(String(header(headers, 'If-Match')).replace(/["\\]/g, '').replace(/^cart-v/, ''))
      if (currentVersion !== store.cart.version) return problem(409, 'VERSION_CHANGED')
      const info = skuInfo(skuId)
      if (!info || stockOf(store, skuId) < 1) return problem(409, 'PRODUCT_UNAVAILABLE')
      if (method === 'DELETE') store.cart.items = store.cart.items.filter(item => item.skuId !== skuId)
      else {
        const quantity = Number(body.quantity)
        if (!Number.isInteger(quantity) || quantity < 1 || quantity > Math.min(100, stockOf(store, skuId))) return problem(409, 'STOCK_CHANGED')
        const existing = store.cart.items.find(item => item.skuId === skuId)
        if (existing) existing.quantity = quantity
        else store.cart.items.push({ skuId, quantity, unitPriceTwd: info.sku.priceTwd, availability: 'InStock' })
      }
      store.cart.version += 1; store.quote = null; saveStore(store)
      return Response.json(cartResponse(store))
    }
    if (path === '/api/v1/carts/current/promotion' && method === 'PUT') {
      if (!store.cart) return problem(404, 'RESOURCE_NOT_FOUND')
      const currentVersion = Number(String(header(headers, 'If-Match')).replace(/["\\]/g, '').replace(/^cart-v/, ''))
      if (currentVersion !== store.cart.version) return problem(409, 'VERSION_CHANGED')
      const code = body.promotionCode ? String(body.promotionCode).toUpperCase() : null
      if (code && code !== 'DEMO15') return problem(422, 'PROMOTION_INVALID')
      ;(store.cart as any).promotionCode = code; store.cart.version += 1; store.quote = null; saveStore(store)
      return Response.json(cartResponse(store))
    }
    if (path === '/api/v1/checkout-quotes' && method === 'POST') {
      if (!store.cart?.items.length) return problem(409, 'CART_EMPTY')
      const cartVersion = Number(String(body.cartVersion || header(headers, 'If-Match')).replace(/["\\]/g, '').replace(/^cart-v/, ''))
      if (cartVersion !== store.cart.version) return problem(409, 'VERSION_CHANGED')
      if (store.cart.items.some(item => stockOf(store, item.skuId) < item.quantity)) return problem(409, 'STOCK_CHANGED')
      const merchandiseTwd = store.cart.items.reduce((sum, item) => sum + item.unitPriceTwd * item.quantity, 0)
      const discountTwd = (store.cart as any).promotionCode === 'DEMO15' ? Math.floor(merchandiseTwd * 0.15) : 0
      const shippingTwd = body.deliveryMethod === 'mock_pickup' ? 0 : 60
      store.quote = { id: id('quote'), cartVersion: store.cart.version, deliveryMethod: body.deliveryMethod || 'mock_home', promotionCode: (store.cart as any).promotionCode || null, expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(), snapshot: { currency: 'TWD', merchandiseTwd, discountTwd, shippingTwd, totalTwd: merchandiseTwd - discountTwd + shippingTwd, lines: [] } }
      saveStore(store); return Response.json({ id: store.quote.id, mode: 'mock', expiresAt: store.quote.expiresAt, snapshot: store.quote.snapshot }, { status: 201 })
    }
    if (path === '/api/v1/checkouts' && method === 'POST') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const fingerprint = JSON.stringify(body)
      const result = idempotent(`checkout:${idempotencyKey || body.quoteId}`, fingerprint, () => {
        if (!store.cart || !store.quote || store.quote.id !== body.quoteId || store.cart.version !== store.quote.cartVersion) throw Object.assign(new Error('QUOTE_CHANGED'), { status: 409, code: 'QUOTE_CHANGED' })
        if (Date.parse(store.quote.expiresAt) <= Date.now()) throw Object.assign(new Error('QUOTE_EXPIRED'), { status: 409, code: 'QUOTE_EXPIRED' })
        if (store.cart.items.some(item => stockOf(store, item.skuId) < item.quantity)) throw Object.assign(new Error('STOCK_CHANGED'), { status: 409, code: 'STOCK_CHANGED' })
        const orderId = id('demo-order')
        const lines = store.cart.items.map(item => {
          const info = skuInfo(item.skuId)!
          const translated = info.product.translations.find(t => t.locale === 'zh-TW')!
          return { skuId: item.skuId, productSlug: info.product.slug, name: { value: translated.name }, colorId: info.sku.colorId, garmentSize: info.sku.garmentSize, quantity: item.quantity, unitPriceTwd: item.unitPriceTwd, paidTwd: item.unitPriceTwd * item.quantity }
        })
        for (const item of store.cart.items) store.stock[item.skuId] = stockOf(store, item.skuId) - item.quantity
        const order: MockOrder = { id: orderId, checkoutId: orderId, version: 1, placedAt: new Date().toISOString(), expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(), orderState: 'AwaitingPayment', paymentState: 'Created', paymentMethod: body.paymentMethod || 'credit_card', compensationRequired: false, snapshot: { ...store.quote.snapshot, lines } }
        if (order.paymentMethod === 'wallet') {
          if (store.walletTwd < order.snapshot.totalTwd) throw Object.assign(new Error('WALLET_INSUFFICIENT_BALANCE'), { status: 409, code: 'WALLET_INSUFFICIENT_BALANCE' })
          store.walletTwd -= order.snapshot.totalTwd; order.paymentState = 'Succeeded'; order.orderState = 'Confirmed'
          store.walletEntries.unshift({ id: id('wallet-entry'), type: 'OrderPayment', amountTwd: -order.snapshot.totalTwd, currency: 'TWD', sourceId: order.id, createdAt: new Date().toISOString() })
        }
        store.orders.unshift(order); store.cart = { ...store.cart, version: store.cart.version + 1, items: [] }; store.quote = null
        return checkoutResponse(order)
      })
      saveStore(store); return Response.json(result, { status: 201 })
    }
    const checkoutStatusMatch = path.match(/^\/api\/v1\/checkouts\/([^/]+)\/status$/)
    if (checkoutStatusMatch && method === 'GET') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const order = store.orders.find(item => item.checkoutId === decodeURIComponent(checkoutStatusMatch[1]!))
      return order ? Response.json(checkoutStatus(order)) : problem(404, 'RESOURCE_NOT_FOUND')
    }
    const paymentMatch = path.match(/^\/api\/v1\/checkouts\/([^/]+)\/payment-attempts$/)
    if (paymentMatch && method === 'POST') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const order = store.orders.find(item => item.checkoutId === decodeURIComponent(paymentMatch[1]!))
      if (!order) return problem(404, 'RESOURCE_NOT_FOUND')
      const result = idempotent(`payment:${order.id}:${idempotencyKey || body.expectedVersion}`, body, () => {
        if (order.orderState !== 'AwaitingPayment' || order.version !== Number(String(body.expectedVersion).replace(/^checkout-v/, ''))) throw Object.assign(new Error('VERSION_CHANGED'), { status: 409, code: 'VERSION_CHANGED' })
        if (String(body.cardNumber || '').replace(/\D/g, '') !== TEST_CARD) throw Object.assign(new Error('CARD_INVALID'), { status: 422, code: 'CARD_INVALID' })
        if (!/^\d{2}\/\d{2}$/.test(String(body.expiryDate || '')) || !/^\d{3,4}$/.test(String(body.securityCode || ''))) throw Object.assign(new Error('CARD_INVALID'), { status: 422, code: 'CARD_INVALID' })
        order.paymentMethod = body.paymentMethod === 'debit_card' ? 'debit_card' : 'credit_card'; order.paymentState = 'Succeeded'; order.orderState = 'Confirmed'; order.version += 1
        return { id: id('payment-attempt'), mode: 'mock', state: 'Succeeded', orderId: order.id }
      })
      saveStore(store); return Response.json(result, { status: 201 })
    }
    if (path === '/api/v1/orders' && method === 'GET') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      return Response.json({ items: store.orders.map(storeOrder), nextCursor: null })
    }
    const orderPath = path.match(/^\/api\/v1\/orders\/([^/]+)(?:\/(.+))?$/)
    if (orderPath) {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const order = store.orders.find(item => item.id === decodeURIComponent(orderPath[1]!))
      if (!order) return problem(404, 'RESOURCE_NOT_FOUND')
      const suffix = orderPath[2]
      if (!suffix && method === 'GET') return Response.json(memberOrderDetail(order))
      if (suffix === 'receipts' && method === 'GET') return Response.json({ receipts: order.paymentState === 'Succeeded' ? [{ id: `DEMO-${order.id}`, state: 'Completed', amountTwd: order.snapshot.totalTwd, displayNotice: '展示憑證，無稅務效力', createdAt: order.placedAt }] : [], adjustments: [], taxEffect: 'none' })
      if (suffix === 'cancellations' && method === 'POST') {
        const result = idempotent(`cancel:${order.id}:${idempotencyKey || body.expectedVersion}`, body, () => {
          if (order.orderState !== 'AwaitingPayment') throw Object.assign(new Error('INVALID_TRANSITION'), { status: 409, code: 'INVALID_TRANSITION' })
          order.orderState = 'Cancelled'; order.version += 1
          for (const line of order.snapshot.lines) store.stock[line.skuId] = stockOf(store, line.skuId) + line.quantity
          return { id: order.id, orderState: order.orderState, mode: 'mock' }
        })
        saveStore(store); return Response.json(result)
      }
      if (suffix === 'returns' && method === 'POST') return problem(409, 'RETURN_NOT_AVAILABLE_IN_DEMO')
    }
    if (path === '/api/v1/wallet/current' && method === 'GET') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const cursor = Number(url.searchParams.get('cursor') || 0), limit = Math.max(1, Math.min(100, Number(url.searchParams.get('limit')) || 20))
      const entries = store.walletEntries.slice(cursor, cursor + limit)
      return Response.json({ mode: 'mock', currency: 'TWD', availableTwd: store.walletTwd, version: store.walletEntries.length + 1, entries, nextCursor: cursor + limit < store.walletEntries.length ? String(cursor + limit) : null })
    }
    if (path === '/api/v1/wallet/top-ups' && method === 'POST') {
      if (!store.authenticated) return problem(401, 'AUTHENTICATION_REQUIRED')
      const amount = Number(body.amountTwd)
      if (!Number.isInteger(amount) || amount < 1 || amount > 50_000) return problem(422, 'VALIDATION_ERROR')
      if (String(body.cardNumber || '').replace(/\D/g, '') !== TEST_CARD) return problem(422, 'CARD_INVALID')
      const result = idempotent(`top-up:${idempotencyKey || amount}`, { amount }, () => {
        store.walletTwd += amount
        const entry = { id: id('wallet-entry'), type: 'MemberTopUp', amountTwd: amount, currency: 'TWD' as const, sourceId: id('wallet-top-up'), createdAt: new Date().toISOString() }
        store.walletEntries.unshift(entry)
        return { entryId: entry.id, mode: 'mock', amountTwd: amount, availableTwd: store.walletTwd }
      })
      saveStore(store); return Response.json(result, { status: 201 })
    }
    if (path.startsWith('/api/v1/')) return problem(404, 'MOCK_ENDPOINT_NOT_IMPLEMENTED')
    return problem(404, 'RESOURCE_NOT_FOUND')
  } catch (cause: any) {
    return problem(Number(cause?.status) || 409, String(cause?.code || 'MOCK_OPERATION_FAILED'))
  }
}
