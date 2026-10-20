import { chromium } from 'file:///Users/lucas/Coding/main/kohami/apps/storefront/node_modules/@playwright/test/index.mjs'
import { performance } from 'node:perf_hooks'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '30', 10)
if (!Number.isInteger(samples) || samples < 1) throw new Error('ISSUE24_SAMPLES must be a positive integer')
const locales = (process.env.ISSUE24_LOCALES || 'zh-TW,ja,en').split(',').map(value => value.trim())
if (locales.length === 0 || locales.some(locale => !['zh-TW', 'ja', 'en'].includes(locale))) throw new Error('ISSUE24_LOCALES must contain zh-TW, ja, and/or en')
const fixedServerDelayMs = 120
const syntheticEmail = ['buyer', 'example.test'].join('@')
const browser = await chromium.launch({ headless: true, executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' })
const measurements = new Map()
const eventTiming = new Map()
const frameTiming = new Map()
const rawFrameEvents = new Map()
let currentLocale = ''
let currentSample = 0
const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
const snapshot = { currency: 'TWD', merchandiseTwd: 1200, discountTwd: 0, shippingTwd: 60, totalTwd: 1260 }
const skuId = 'sku-issue24-latency-m'
const product = {
  productId: 'product-issue24-latency', slug: 'latency-demo-shirt',
  translations: [{ locale: 'zh-TW', name: '延遲驗收示意襯衫' }, { locale: 'ja', name: '遅延確認用シャツ' }, { locale: 'en', name: 'Latency demo shirt' }],
  media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖' } } }],
  skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1200, availability: 'InStock', mode: 'mock',
}
const localized = {
  'zh-TW': { search: '搜尋名稱、SKU 或商品描述', searchAction: '搜尋商品', productName: '延遲驗收示意襯衫', productSearch: '延遲驗收', size: '顏色與衣服尺寸', add: '加入購物車', quantity: '數量', subtotal: '小計', quoteAction: '重新試算', serviceError: '本機展示服務暫時無法使用', quote: '結帳報價', email: '測試 Email', confirm: '我確認以此報價建立模擬訂單', placeOrder: '建立模擬訂單並保留庫存', aftercare: '訂單配送與售後（模擬）', orderId: '訂單編號', lookup: '查詢自己的訂單', lineId: '訂單明細編號', reason: '原因', requestReturn: '提出退貨申請' },
  ja: { search: '商品名、SKU、説明を検索', searchAction: '商品を検索', productName: '遅延確認用シャツ', productSearch: '遅延確認', size: '色と衣服サイズ', add: 'カートに追加', quantity: '数量', subtotal: '小計', quoteAction: '再見積もり', serviceError: 'ローカルデモサービスを利用できません', quote: 'チェックアウト見積もり', email: 'テストメール', confirm: 'この見積もりで模擬注文を作成', placeOrder: '模擬注文を作成して在庫を確保', aftercare: '注文配送とアフターサービス（模擬）', orderId: '注文番号', lookup: '自分の注文を確認', lineId: '注文明細 ID', reason: '理由', requestReturn: '返品を申請' },
  en: { search: 'Search product name, SKU or description', searchAction: 'Search products', productName: 'Latency demo shirt', productSearch: 'Latency demo', size: 'Color and garment size', add: 'Add to cart', quantity: 'Quantity', subtotal: 'Subtotal', quoteAction: 'Refresh quote', serviceError: 'The local demo service is temporarily unavailable', quote: 'Checkout quote', email: 'Test email', confirm: 'I confirm this quote for a mock order', placeOrder: 'Create mock order and reserve stock', aftercare: 'Order delivery and aftercare (mock)', orderId: 'Order ID', lookup: 'Load my order', lineId: 'Order line ID', reason: 'Reason', requestReturn: 'Request a return' },
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
function add(name, value) { const key = `${currentLocale}/${name}`; const list = measurements.get(key) || []; list.push(value); measurements.set(key, list) }
async function latency(page, name, action, settled) {
  await page.evaluate(label => {
    performance.mark(`${label}:start`)
    window.__issue24ActiveAction = label
  window.__issue24ActionStartTime = performance.now()
  }, name)
  await action()
  await settled()
  const result = await page.evaluate(async label => {
    await new Promise(resolve => requestAnimationFrame(() => resolve()))
    performance.mark(`${label}:end`)
    window.__issue24ActiveAction = ''
    return {
      duration: performance.measure(label, `${label}:start`, `${label}:end`).duration,
      frameEvents: window.__issue24NextFrameEvents.filter(entry => entry.action === label),
      startTime: window.__issue24ActionStartTime,
      endTime: performance.now(),
    }
  }, name)
  await page.evaluate(({ label, startTime, endTime }) => { window.__issue24ActionWindows.push({ label, startTime, endTime }) }, { label: name, startTime: result.startTime, endTime: result.endTime })
  add(name, result.duration)
  for (const entry of result.frameEvents) {
    const key = `${currentLocale}/${name}`
    const values = frameTiming.get(key) || []
    values.push(entry.eventToFirstFrameMs)
    frameTiming.set(key, values)
    const raw = rawFrameEvents.get(currentLocale) || []
    raw.push({ ...entry, sample: currentSample, action: name })
    rawFrameEvents.set(currentLocale, raw)
  }
}
async function delay() { await new Promise(resolve => setTimeout(resolve, fixedServerDelayMs)) }

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true })
  for (const locale of locales) {
  currentLocale = locale
  const labels = localized[locale]
  for (let sample = 0; sample < samples; sample += 1) {
    currentSample = sample + 1
    const page = await context.newPage()
    const cdp = await context.newCDPSession(page)
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
    await cdp.send('Network.enable')
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
    await page.addInitScript(() => {
      const entries = []
      const frameEntries = []
      Object.defineProperty(window, '__issue24EventTiming', { value: entries })
      Object.defineProperty(window, '__issue24NextFrameEvents', { value: frameEntries })
      Object.defineProperty(window, '__issue24ActiveAction', { value: '', writable: true })
      Object.defineProperty(window, '__issue24ActionWindows', { value: [] })
      for (const type of ['click', 'keydown', 'input', 'change']) {
        document.addEventListener(type, event => {
          const action = window.__issue24ActiveAction
          if (!action) return
          const eventTimestamp = event.timeStamp
          requestAnimationFrame(frameTimestamp => frameEntries.push({
            type,
            eventTimestamp: +eventTimestamp.toFixed(3),
            firstFrameTimestamp: +frameTimestamp.toFixed(3),
            eventToFirstFrameMs: +Math.max(0, frameTimestamp - eventTimestamp).toFixed(3),
            action,
          }))
        }, { capture: true, passive: true })
      }
      try { new PerformanceObserver(list => entries.push(...list.getEntries().map(entry => ({ name: entry.name, duration: entry.duration, interactionId: entry.interactionId, startTime: entry.startTime, processingStart: entry.processingStart, processingEnd: entry.processingEnd, cancelable: entry.cancelable })))).observe({ type: 'event', buffered: true, durationThreshold: 16 }) } catch {}
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

    await page.goto(`http://127.0.0.1:3000/${locale}`)
    await page.waitForFunction(() => Boolean(document.querySelector('#__nuxt')?.__vue_app__))

    const search = page.getByRole('searchbox', { name: labels.search })
    await search.fill(labels.productSearch)
    const searchButton = page.getByRole('button', { name: labels.searchAction }).first()
    await latency(page, 'search-failure', () => searchButton.click(), () => page.locator('.discovery [role="alert"]').waitFor())
    const retrySearch = page.locator('.discovery [role="alert"]').getByRole('button', { name: labels.searchAction })
    await latency(page, 'search-recovery', () => retrySearch.click(), () => page.getByRole('heading', { name: labels.productName }).waitFor())

    await page.getByRole('combobox', { name: `${labels.size}: ${labels.productName}` }).selectOption(skuId)
    const addButton = page.getByRole('button', { name: labels.add }).first()
    await addButton.waitFor({ state: 'visible' })
    if (!(await addButton.isEnabled())) throw new Error('add-to-cart control unexpectedly disabled')
    await latency(page, 'cart-add-failure', () => addButton.click(), () => page.locator('section[aria-labelledby="cart-heading"] [role="alert"]').first().waitFor())
    await latency(page, 'cart-add-recovery', () => addButton.click(), () => page.getByRole('spinbutton', { name: labels.quantity }).waitFor())
    const quantity = page.getByRole('spinbutton', { name: labels.quantity })
    await latency(page, 'cart-quantity-failure', async () => { await quantity.fill('2'); await quantity.dispatchEvent('change') }, () => page.locator('section[aria-labelledby="cart-heading"] [role="alert"]').first().waitFor())
    await latency(page, 'cart-quantity-recovery', async () => { await quantity.fill('2'); await quantity.dispatchEvent('change') }, async () => { await page.getByText(`${labels.subtotal}: TWD 2400`).waitFor(); await page.waitForFunction(() => !document.querySelector('button[aria-busy="true"]')) })

    const quoteButton = page.getByRole('button', { name: labels.quoteAction })
    await latency(page, 'checkout-quote-failure', () => quoteButton.click(), () => page.getByRole('alert').filter({ hasText: labels.serviceError }).waitFor())
    await latency(page, 'checkout-quote-recovery', () => quoteButton.click(), () => page.getByRole('region', { name: labels.quote }).waitFor())
    const quote = page.getByRole('region', { name: labels.quote })
    const email = quote.getByRole('textbox', { name: labels.email })
    await email.fill(syntheticEmail)
    const confirmation = quote.getByRole('checkbox')
    await confirmation.check()
    const createOrder = quote.getByRole('button', { name: labels.placeOrder })
    await latency(page, 'checkout-create-failure', () => createOrder.click(), () => quote.getByRole('alert').waitFor())
    await latency(page, 'checkout-create-recovery', () => createOrder.click(), () => page.getByText('checkout-issue24-latency').first().waitFor())

    const aftercare = page.getByRole('region', { name: labels.aftercare })
    const orderId = aftercare.getByRole('textbox', { name: labels.orderId })
    const lookup = aftercare.getByRole('button', { name: labels.lookup })
    await orderId.fill('hidden_order')
    await latency(page, 'aftercare-not-found', () => lookup.click(), () => aftercare.getByRole('alert').waitFor())
    await orderId.fill('service_offline')
    await latency(page, 'aftercare-service-error', () => lookup.click(), () => aftercare.getByRole('alert').waitFor())
    await orderId.fill('issue24_latency_order')
    await latency(page, 'aftercare-lookup-success', () => lookup.click(), () => aftercare.getByText('issue24_latency_order').waitFor())
    await aftercare.getByRole('textbox', { name: labels.lineId }).fill('issue24_latency_line')
    await aftercare.getByRole('textbox', { name: labels.reason, exact: true }).first().fill('synthetic latency retry')
    const returnButton = aftercare.getByRole('button', { name: labels.requestReturn })
    await latency(page, 'aftercare-return-failure', () => returnButton.click(), () => aftercare.getByRole('alert').waitFor())
    await latency(page, 'aftercare-return-recovery', () => returnButton.click(), () => aftercare.locator('p').filter({ hasText: 'issue24_latency_return' }).waitFor())

    const pageEventData = await page.evaluate(async () => { await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))); return { entries: window.__issue24EventTiming || [], windows: window.__issue24ActionWindows || [] } })
    const localeEvents = eventTiming.get(locale) || []
    localeEvents.push(...pageEventData.entries.map(entry => ({ ...entry, sample: currentSample, action: pageEventData.windows.find(window => entry.startTime >= window.startTime && entry.startTime <= window.endTime)?.label || null })))
    eventTiming.set(locale, localeEvents)
    await page.close()
    if ((sample + 1) % 5 === 0) console.error(`completed ${sample + 1}/${samples} ${locale} journeys`)
  }
  }

  const output = Object.fromEntries(locales.map(locale => {
    const actions = Object.fromEntries([...measurements.entries()].filter(([name]) => name.startsWith(`${locale}/`)).map(([name, values]) => {
      const label = name.slice(locale.length + 1)
      const frameValues = frameTiming.get(name) || []
      return [label, {
        samples: values.length,
        p50Ms: +percentile(values, 0.5).toFixed(1),
        p75Ms: +percentile(values, 0.75).toFixed(1),
        maxMs: +Math.max(...values).toFixed(1),
        eventToFirstFrameProxy: {
          samples: frameValues.length,
          p50Ms: frameValues.length ? +percentile(frameValues, 0.5).toFixed(1) : null,
          p75Ms: frameValues.length ? +percentile(frameValues, 0.75).toFixed(1) : null,
          maxMs: frameValues.length ? +Math.max(...frameValues).toFixed(1) : null,
        },
      }]
    }))
    const localeEventEntries = eventTiming.get(locale) || []
    const eventDurations = localeEventEntries.filter(item => item.interactionId > 0).map(item => item.duration)
    const visitInteractionMaxima = Array.from({ length: samples }, (_, index) => {
      const visitEntries = localeEventEntries.filter(item => item.sample === index + 1 && item.interactionId > 0)
      const byInteraction = new Map()
      for (const entry of visitEntries) byInteraction.set(entry.interactionId, Math.max(byInteraction.get(entry.interactionId) || 0, entry.duration))
      return byInteraction.size ? Math.max(...byInteraction.values()) : null
    })
    const visitInteractionValues = visitInteractionMaxima.filter(value => value !== null)
    const mappedInteractionEntries = localeEventEntries.filter(item => item.interactionId > 0 && item.action).length
    const unmappedInteractionEntries = localeEventEntries.filter(item => item.interactionId > 0 && !item.action).length
    const raw = rawFrameEvents.get(locale) || []
    const journeyMaxima = Array.from({ length: samples }, (_, index) => {
      const journey = raw.filter(item => item.sample === index + 1).map(item => item.eventToFirstFrameMs)
      return journey.length ? Math.max(...journey) : null
    }).filter(value => value !== null)
    return [locale, {
      actionToSettledUI: actions,
      eventTiming: { thresholdMs: 16, observedEntries: eventDurations.length, uniqueInteractions: new Set(localeEventEntries.filter(item => item.interactionId > 0).map(item => `${item.sample}/${item.interactionId}`)).size, visitsWithObservedCandidate: visitInteractionValues.length, mappedInteractionEntries, unmappedInteractionEntries, visitMaximumOfObservedInteractionsP50Ms: visitInteractionValues.length ? +percentile(visitInteractionValues, 0.5).toFixed(1) : null, visitMaximumOfObservedInteractionsP75Ms: visitInteractionValues.length ? +percentile(visitInteractionValues, 0.75).toFixed(1) : null, visitMaximumOfObservedInteractionsMaxMs: visitInteractionValues.length ? +Math.max(...visitInteractionValues).toFixed(1) : null, entryP50Ms: eventDurations.length ? +percentile(eventDurations, 0.5).toFixed(1) : null, entryP75Ms: eventDurations.length ? +percentile(eventDurations, 0.75).toFixed(1) : null, entryMaxMs: eventDurations.length ? +Math.max(...eventDurations).toFixed(1) : null },
      eventToFirstAnimationFrameProxy: {
        observedEvents: raw.length,
        below16Ms: raw.filter(item => item.eventToFirstFrameMs < 16).length,
        p50Ms: raw.length ? +percentile(raw.map(item => item.eventToFirstFrameMs), 0.5).toFixed(1) : null,
        p75Ms: raw.length ? +percentile(raw.map(item => item.eventToFirstFrameMs), 0.75).toFixed(1) : null,
        maxMs: raw.length ? +Math.max(...raw.map(item => item.eventToFirstFrameMs)).toFixed(1) : null,
        journeysWithEvents: journeyMaxima.length,
        journeysWithAnyBelow16Ms: Array.from({ length: samples }, (_, index) => raw.some(item => item.sample === index + 1 && item.eventToFirstFrameMs < 16)).filter(Boolean).length,
        perJourneyMaximumP50Ms: journeyMaxima.length ? +percentile(journeyMaxima, 0.5).toFixed(1) : null,
        perJourneyMaximumP75Ms: journeyMaxima.length ? +percentile(journeyMaxima, 0.75).toFixed(1) : null,
        perJourneyMaximumMs: journeyMaxima.length ? +Math.max(...journeyMaxima).toFixed(1) : null,
      },
    }]
  }))
  console.log(JSON.stringify({ profile: { browser: `Google Chrome ${browser.version()}`, platform: `${process.platform}/${process.arch}`, viewport: '1440x900', cpuSlowdown: '4x', network: '150ms network latency, 1.6 Mbps down, 750 Kbps up', mockApiResponseDelayMs: fixedServerDelayMs, samplesPerJourney: samples, locales, eventTimingDurationThresholdMs: 16 }, locales: output, rawEventTimingEntries: Object.fromEntries(eventTiming), rawEventToFirstAnimationFrameProxy: Object.fromEntries(rawFrameEvents), limitation: 'Synthetic scripted interactions using Chromium Event Timing entries with a 16ms duration threshold, grouped by interactionId and summarized by each visit maximum. Entries below 16ms are omitted; this is a traceable lab estimate for controlled comparison, not field INP, a complete population measure, or an SLA. First-rAF values are retained only as a separate proxy and are not paint or INP.' }, null, 2))
} finally {
  await browser.close()
}
