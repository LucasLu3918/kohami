import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createDataSourceFetch } from '../app/utils/data-source-fetch.ts'

test('mock catalog, filtering and detail work without any backend request', async () => {
  let calls = 0
  const api = createDataSourceFetch({ source: 'mock', backendFetch: async () => { calls++; throw new Error('must not connect') } })
  let catalog = await api('/api/v1/products?locale=zh-TW&limit=100')
  const slugs = new Set(catalog.items.map(item => item.slug))
  while (catalog.nextCursor) {
    catalog = await api(`/api/v1/products?locale=zh-TW&limit=100&cursor=${encodeURIComponent(catalog.nextCursor)}`)
    catalog.items.forEach(item => slugs.add(item.slug))
  }
  assert.equal(slugs.size, 15)
  const filtered = await api('/api/v1/products?locale=en&q=linen&limit=1')
  assert.equal(filtered.items.length, 1)
  assert.ok(filtered.nextCursor)
  const next = await api(`/api/v1/products?locale=en&q=linen&limit=1&cursor=${filtered.nextCursor}`)
  assert.notEqual(next.items[0].skuId, filtered.items[0].skuId)
  const detail = await api(`/api/v1/products/${filtered.items[0].slug}`)
  assert.equal(detail.slug, filtered.items[0].slug)
  assert.equal(detail.translations.length, 3)
  assert.equal(calls, 0)
})

test('interactive mock stays available when no backend is configured', async () => {
  const api = createDataSourceFetch({ source: 'mock', backendFetch: async () => assert.fail('backend called') })
  assert.equal((await api('/api/v1/auth/session')).state, 'Anonymous')
  const csrf = await api('/api/v1/auth/csrf')
  const cart = await api('/api/v1/carts', { method: 'POST', body: {}, headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': 'public-mock-cart' } })
  assert.ok(cart.id)
  await assert.rejects(api('/api/v1/products/missing'), error => error.data.code === 'RESOURCE_NOT_FOUND')
})

test('API mode delegates unchanged and preserves backend failures without fabricating mock data', async () => {
  const seen = []
  const backendFetch = async (...args) => { seen.push(args); return { items: [{ slug: 'database-product' }] } }
  const api = createDataSourceFetch({ source: 'backend', backendFetch })
  assert.equal((await api('/api/v1/products')).items[0].slug, 'database-product')
  assert.equal(seen[0][0], '/api/v1/products')
  const unavailable = createDataSourceFetch({ source: 'backend', backendFetch: async (...args) => { seen.push(args); throw Object.assign(new Error('SERVICE_UNAVAILABLE'), { data: { code: 'SERVICE_UNAVAILABLE' } }) } })
  await assert.rejects(unavailable('/api/v1/products'), error => error.data.code === 'SERVICE_UNAVAILABLE')
  assert.equal(seen.length, 2)
  const failed = createDataSourceFetch({ source: 'backend', backendFetch: async () => { throw new Error('offline') } })
  await assert.rejects(failed('/api/v1/products'), /offline/)
})

test('public mock member can sign in, use a cart, pay a simulated order and query it', async () => {
  const values = new Map()
  const previousWindow = Object.getOwnPropertyDescriptor(globalThis, 'window')
  Object.defineProperty(globalThis, 'window', {
    configurable: true,
    value: { localStorage: {
      getItem: key => values.get(key) ?? null,
      setItem: (key, value) => values.set(key, value),
    } },
  })
  let backendCalls = 0
  const api = createDataSourceFetch({
    source: 'mock',
    backendFetch: async () => { backendCalls++; throw new Error('mock must not contact backend') },
  })
  const csrf = await api('/api/v1/auth/csrf')
  const csrfHeaders = { 'X-CSRF-TOKEN': csrf.requestToken }
  try {
    const session = await api('/api/v1/auth/sessions', {
      method: 'POST', body: { demoMode: 'member' }, headers: csrfHeaders,
    })
    assert.equal(session.state, 'MemberAuthenticated')

    const productPage = await api('/api/v1/products?locale=zh-TW&limit=100')
    const sku = productPage.items.find(item => item.availability === 'InStock')
    assert.ok(sku)
    const cart = await api('/api/v1/carts', {
      method: 'POST', body: {}, headers: { ...csrfHeaders, 'Idempotency-Key': 'mock-flow-create-cart' },
    })
    const updatedCart = await api(`/api/v1/carts/current/items/${encodeURIComponent(sku.skuId)}`, {
      method: 'PUT', body: { quantity: 1 }, headers: { ...csrfHeaders, 'If-Match': `"${cart.version}"`, 'Idempotency-Key': 'mock-flow-add-item' },
    })
    assert.equal(updatedCart.items[0].skuId, sku.skuId)

    const quote = await api('/api/v1/checkout-quotes', {
      method: 'POST', body: { cartVersion: updatedCart.version, deliveryMethod: 'mock_home', region: 'demo_supported' },
      headers: { ...csrfHeaders, 'If-Match': `"${updatedCart.version}"`, 'Idempotency-Key': 'mock-flow-quote' },
    })
    const checkout = await api('/api/v1/checkouts', {
      method: 'POST', body: {
        quoteId: quote.id, cartVersion: updatedCart.version, contact: {},
        delivery: { method: 'mock_home', region: 'demo_supported' }, confirmed: true, paymentMethod: 'credit_card',
      },
      headers: { ...csrfHeaders, 'If-Match': `"${updatedCart.version}"`, 'Idempotency-Key': 'mock-flow-checkout' },
    })
    assert.ok(checkout.checkoutId)
    assert.equal((await api(`/api/v1/checkouts/${checkout.checkoutId}/status`)).paymentState, 'Created')

    const status = await api(`/api/v1/checkouts/${checkout.checkoutId}/status`)
    const payment = await api(`/api/v1/checkouts/${checkout.checkoutId}/payment-attempts`, {
      method: 'POST', body: {
        expectedVersion: status.version, paymentMethod: 'credit_card', demoOutcome: 'success',
      },
      headers: { ...csrfHeaders, 'Idempotency-Key': 'mock-flow-payment' },
    })
    assert.equal(payment.state, 'Succeeded')
    const orders = await api('/api/v1/orders')
    assert.equal(orders.items[0].id, checkout.orderId)
    assert.equal(orders.items[0].orderState, 'Confirmed')
    const order = await api(`/api/v1/orders/${checkout.orderId}`)
    assert.equal(order.snapshot.lines[0].skuId, sku.skuId)
    assert.equal((await api(`/api/v1/orders/${checkout.orderId}/receipts`)).receipts[0].amountTwd, quote.snapshot.totalTwd)
    assert.equal(backendCalls, 0)
  } finally {
    if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow)
    else delete globalThis.window
  }
})
