import { chromium } from '@playwright/test'
import { performance } from 'node:perf_hooks'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '30', 10)
if (!Number.isInteger(samples) || samples < 1) throw new Error('ISSUE24_SAMPLES must be a positive integer')
const fixedServerDelayMs = 120
const browser = await chromium.launch({ channel: 'chrome', headless: true })
const measurements = new Map()
const eventTiming = []
const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
const snapshot = { currency: 'TWD', merchandiseTwd: 1200, discountTwd: 0, shippingTwd: 60, totalTwd: 1260 }
const skuId = 'sku-issue24-latency-m'
const product = {
  productId: 'product-issue24-latency', slug: 'latency-demo-shirt',
  translations: [{ locale: 'zh-TW', name: '延遲驗收示意襯衫' }, { locale: 'ja', name: '遅延確認用シャツ' }, { locale: 'en', name: 'Latency demo shirt' }],
  media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖' } } }],
  skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1200, availability: 'InStock', mode: 'mock',
}
const orderView = () => ({
  id: 'issue24_latency_order', version: 'order-v1', orderState: 'Confirmed',
  snapshot: { lines: [{ orderLineId: 'issue24_latency_line', skuId, quantity: 1 }] },
  shipmentSummary: { state: 'Delivered', shipments: [] }, returns: [],
})

function percentile(values, p) {
  const sorted = [...values].sort((a, b) => a - b)
  return sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)]
}
function add(name, value) { const list = measurements.get(name) || []; list.push(value); measurements.set(name, list) }
async function latency(page, name, action, settled) {
  await page.evaluate(label => performance.mark(`${label}:start`), name)
  await action()
  await settled()
  const value = await page.evaluate(label => {
    performance.mark(`${label}:end`)
    return performance.measure(label, `${label}:start`, `${label}:end`).duration
  }, name)
  add(name, value)
}
async function delay() { await new Promise(resolve => setTimeout(resolve, fixedServerDelayMs)) }

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true })
  for (let sample = 0; sample < samples; sample += 1) {
    const page = await context.newPage()
    const cdp = await context.newCDPSession(page)
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
    await cdp.send('Network.enable')
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
    await page.addInitScript(() => {
      const entries = []
      Object.defineProperty(window, '__issue24EventTiming', { value: entries })
      try { new PerformanceObserver(list => entries.push(...list.getEntries().map(entry => ({ name: entry.name, duration: entry.duration, interactionId: entry.interactionId })))).observe({ type: 'event', buffered: true, durationThreshold: 16 }) } catch {}
    })

    const state = { cartVersion: 0, productReads: 0, cartWrites: 0, quoteWrites: 0, checkoutWrites: 0, orderReads: 0, returnWrites: 0 }
    const cart = () => ({ id: 'cart-issue24-latency', version: `cart-v${state.cartVersion}`, items: state.cartVersion ? [{ skuId, quantity: state.cartVersion > 1 ? 2 : 1, unitPriceTwd: 1200, availability: 'InStock' }] : [] })
    await page.route('**/api/v1/**', async route => {
      const request = route.request(); const url = new URL(request.url()); const path = url.pathname.replace('/api/v1', '');
      let status = 200; let body = {}
      if (path === '/auth/session') body = { state: 'Anonymous', mode: 'mock' }
      else if (path === '/auth/csrf') body = { requestToken: 'synthetic-issue24-latency' }
      else if (path === '/products') { state.productReads += 1; status = state.productReads <= 2 ? 503 : 200; body = status === 200 ? { items: [product], nextCursor: null } : { code: 'SERVICE_UNAVAILABLE' } }
      else if (path === '/carts/current' && request.method() === 'GET') body = cart()
      else if (path === '/carts' && request.method() === 'POST') body = { id: 'cart-issue24-latency', version: 'cart-v0', items: [] }
      else if (path === `/carts/current/items/${skuId}` && request.method() === 'PUT') { state.cartWrites += 1; if (state.cartWrites === 1 || state.cartWrites === 3) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } } else { state.cartVersion += 1; body = cart() } }
      else if (path === '/checkout-quotes' && request.method() === 'POST') { state.quoteWrites += 1; if (state.quoteWrites === 1) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } } else body = { id: 'quote-issue24-latency', mode: 'mock', expiresAt, snapshot } }
      else if (path === '/checkouts' && request.method() === 'POST') { state.checkoutWrites += 1; if (state.checkoutWrites === 1) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } } else body = { checkoutId: 'checkout-issue24-latency', orderId: 'order-issue24-latency', mode: 'mock', version: 'checkout-v1', expiresAt, snapshot, statusUrl: '/api/v1/checkouts/checkout-issue24-latency/status' } }
      else if (path === '/checkouts/checkout-issue24-latency/status') body = { checkoutId: 'checkout-issue24-latency', mode: 'mock', version: 'status-v1', orderState: 'AwaitingPayment', paymentState: 'Created', compensationRequired: false, expiresAt, snapshot: { ...snapshot, lines: [{ skuId, quantity: 1, unitPriceTwd: 1200 }] } }
      else if (path.startsWith('/orders/') && path.endsWith('/receipts')) body = { orderId: 'issue24_latency_order', taxEffect: 'none', receipts: [], adjustments: [] }
      else if (path.startsWith('/orders/') && path.endsWith('/returns') && request.method() === 'POST') { state.returnWrites += 1; if (state.returnWrites === 1) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } } else body = { id: 'issue24_latency_return', state: 'Requested', version: 'return-v1', quantity: 1, orderLineId: 'issue24_latency_line' } }
      else if (path === '/orders/hidden_order') { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
      else if (path === '/orders/service_offline') { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } }
      else if (path === '/orders/issue24_latency_order') { state.orderReads += 1; body = orderView() }
      else { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
      await delay()
      await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    })

    await page.goto('http://localhost:3000/zh-TW')
    await page.waitForFunction(() => Boolean(document.querySelector('#__nuxt')?.__vue_app__))

    const search = page.getByRole('searchbox', { name: '搜尋名稱、SKU 或商品描述' })
    await search.fill('延遲驗收')
    const searchButton = page.getByRole('button', { name: '搜尋商品' }).first()
    await latency(page, 'search-failure', () => searchButton.click(), () => page.locator('.discovery [role="alert"]').waitFor())
    const retrySearch = page.locator('.discovery [role="alert"]').getByRole('button', { name: '搜尋商品' })
    await latency(page, 'search-recovery', () => retrySearch.click(), () => page.getByRole('heading', { name: '延遲驗收示意襯衫' }).waitFor())

    await page.getByRole('combobox', { name: '顏色與衣服尺寸: 延遲驗收示意襯衫' }).selectOption(skuId)
    const addButton = page.getByRole('button', { name: '加入購物車' }).first()
    await addButton.waitFor({ state: 'visible' })
    if (!(await addButton.isEnabled())) throw new Error('add-to-cart control unexpectedly disabled')
    await latency(page, 'cart-add-failure', () => addButton.click(), () => page.locator('section[aria-labelledby="cart-heading"] [role="alert"]').first().waitFor())
    await latency(page, 'cart-add-recovery', () => addButton.click(), () => page.getByRole('spinbutton', { name: '數量' }).waitFor())
    const quantity = page.getByRole('spinbutton', { name: '數量' })
    await latency(page, 'cart-quantity-failure', async () => { await quantity.fill('2'); await quantity.dispatchEvent('change') }, () => page.locator('section[aria-labelledby="cart-heading"] [role="alert"]').first().waitFor())
    await latency(page, 'cart-quantity-recovery', async () => { await quantity.fill('2'); await quantity.dispatchEvent('change') }, async () => { await page.getByText('小計: TWD 2400').waitFor(); await page.waitForFunction(() => !document.querySelector('button[aria-busy="true"]')) })

    const quoteButton = page.getByRole('button', { name: '重新試算' })
    await latency(page, 'checkout-quote-failure', () => quoteButton.click(), () => page.getByRole('alert').filter({ hasText: '本機展示服務暫時無法使用' }).waitFor())
    await latency(page, 'checkout-quote-recovery', () => quoteButton.click(), () => page.getByRole('region', { name: '結帳報價' }).waitFor())
    const quote = page.getByRole('region', { name: '結帳報價' })
    const email = quote.getByRole('textbox', { name: '測試 Email' })
    await email.fill('buyer@example.test')
    const confirmation = quote.getByRole('checkbox')
    await confirmation.check()
    const createOrder = quote.getByRole('button', { name: '建立模擬訂單並保留庫存' })
    await latency(page, 'checkout-create-failure', () => createOrder.click(), () => quote.getByRole('alert').waitFor())
    await latency(page, 'checkout-create-recovery', () => createOrder.click(), () => page.getByText('checkout-issue24-latency').first().waitFor())

    const aftercare = page.getByRole('region', { name: '訂單配送與售後（模擬）' })
    const orderId = aftercare.getByRole('textbox', { name: '訂單編號' })
    const lookup = aftercare.getByRole('button', { name: '查詢自己的訂單' })
    await orderId.fill('hidden_order')
    await latency(page, 'aftercare-not-found', () => lookup.click(), () => aftercare.getByRole('alert').waitFor())
    await orderId.fill('service_offline')
    await latency(page, 'aftercare-service-error', () => lookup.click(), () => aftercare.getByRole('alert').waitFor())
    await orderId.fill('issue24_latency_order')
    await latency(page, 'aftercare-lookup-success', () => lookup.click(), () => aftercare.getByText('issue24_latency_order').waitFor())
    await aftercare.getByRole('textbox', { name: '訂單明細編號' }).fill('issue24_latency_line')
    await aftercare.getByRole('textbox', { name: '原因', exact: true }).first().fill('synthetic latency retry')
    const returnButton = aftercare.getByRole('button', { name: '提出退貨申請' })
    await latency(page, 'aftercare-return-failure', () => returnButton.click(), () => aftercare.getByRole('alert').waitFor())
    await latency(page, 'aftercare-return-recovery', () => returnButton.click(), () => aftercare.locator('p').filter({ hasText: 'issue24_latency_return' }).waitFor())

    eventTiming.push(...await page.evaluate(() => window.__issue24EventTiming || []))
    await page.close()
    if ((sample + 1) % 5 === 0) console.log(`completed ${sample + 1}/${samples} journeys`)
  }

  const output = Object.fromEntries([...measurements.entries()].map(([name, values]) => [name, { samples: values.length, p50Ms: +percentile(values, 0.5).toFixed(1), p75Ms: +percentile(values, 0.75).toFixed(1), maxMs: +Math.max(...values).toFixed(1) }]))
  const eventDurations = eventTiming.filter(item => item.interactionId > 0).map(item => item.duration)
  console.log(JSON.stringify({ profile: { browser: `Chrome ${browser.version()}`, platform: `${process.platform}/${process.arch}`, viewport: '1440x900', cpuSlowdown: '4x', network: '150ms network latency, 1.6 Mbps down, 750 Kbps up', mockApiResponseDelayMs: fixedServerDelayMs, samplesPerJourney: samples }, actionToSettledUI: output, eventTiming: { thresholdMs: 16, observedEntries: eventDurations.length, p50Ms: eventDurations.length ? +percentile(eventDurations, 0.5).toFixed(1) : null, p75Ms: eventDurations.length ? +percentile(eventDurations, 0.75).toFixed(1) : null, maxMs: eventDurations.length ? +Math.max(...eventDurations).toFixed(1) : null }, limitation: 'Synthetic fixture; Event Timing omits interactions below 16ms and is not field INP.' }, null, 2))
} finally {
  await browser.close()
}
