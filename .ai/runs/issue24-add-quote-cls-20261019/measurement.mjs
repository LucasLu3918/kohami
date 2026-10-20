import { chromium } from '/Users/lucas/Coding/main/kohami/apps/storefront/node_modules/@playwright/test/index.mjs'
import os from 'node:os'
import { writeFile } from 'node:fs/promises'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '30', 10)
const origin = process.env.KOHAMI_STOREFRONT_ORIGIN || 'http://127.0.0.1:3002'
const outputPath = process.env.ISSUE24_OUTPUT || new URL('./metrics.json', import.meta.url)
const locales = ['zh-TW', 'ja', 'en']
const skuId = 'sku-issue24-add-quote-cls'
const product = {
  productId: 'issue24-add-quote-cls-product', slug: 'issue24-add-quote-cls-shirt',
  translations: ['zh-TW', 'ja', 'en'].map((locale, index) => ({ locale, name: ['CLS 合成襯衫', 'CLS 合成シャツ', 'CLS synthetic shirt'][index], description: 'Synthetic CLS fixture', material: 'Synthetic' })),
  media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: 'Synthetic placeholder' } } }],
  skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1200, availability: 'InStock', mode: 'mock',
}
const words = {
  'zh-TW': { add: '加入購物車', quote: '重新試算', error: '本機展示服務暫時無法使用。' },
  ja: { add: 'カートに追加', quote: '再見積もり', error: 'ローカルデモサービスを利用できません。' },
  en: { add: 'Add to cart', quote: 'Refresh quote', error: 'The local demo service is temporarily unavailable.' },
}
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--no-proxy-server'] })
const startedAt = new Date().toISOString(), raw = [], summary = {}
function percentile(values, p) { const sorted = [...values].sort((a, b) => a - b); return sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)] }
function sessionWindow(entries) {
  const shifts = entries.filter(entry => !entry.hadRecentInput).sort((a, b) => a.startTime - b.startTime)
  let max = 0, sum = 0, start = 0, previous = 0
  for (const shift of shifts) {
    if (sum === 0 || shift.startTime - previous >= 1000 || shift.startTime - start >= 5000) { sum = shift.value; start = shift.startTime }
    else sum += shift.value
    previous = shift.startTime; max = Math.max(max, sum)
  }
  return max
}
try {
  for (const locale of locales) {
    summary[locale] = {}
    for (let sample = 1; sample <= samples; sample++) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
      const page = await context.newPage(), cdp = await context.newCDPSession(page)
      await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
      await cdp.send('Network.enable')
      await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
      await page.addInitScript(() => {
        Object.defineProperty(window, '__issue24Shifts', { value: [] })
        new PerformanceObserver(list => window.__issue24Shifts.push(...list.getEntries().map(entry => ({ startTime: +entry.startTime.toFixed(3), value: entry.value, hadRecentInput: entry.hadRecentInput, sources: entry.sources?.map(source => ({ node: source.node?.nodeName || null, previousRect: source.previousRect?.toJSON?.() || null, currentRect: source.currentRect?.toJSON?.() || null })) || [] })))).observe({ type: 'layout-shift', buffered: true })
      })
      let cartWrites = 0, quoteWrites = 0
      let cart = { id: `cart-${sample}`, version: 'cart-v1', items: [{ skuId, quantity: 1, unitPriceTwd: 1200, availability: 'InStock' }] }
      await page.route('**/api/v1/**', async route => {
        const req = route.request(), url = new URL(req.url()), path = url.pathname.replace('/api/v1', '')
        let status = 200, body = {}
        if (path === '/auth/session') body = { state: 'Anonymous', mode: 'mock' }
        else if (path === '/auth/csrf') body = { requestToken: 'synthetic-issue24-add-quote-cls' }
        else if (path === '/products' && req.method() === 'GET') body = { items: [product], nextCursor: null }
        else if (path === '/carts/current' && req.method() === 'GET') body = cart
        else if (path === '/carts/current/items/' + skuId && req.method() === 'PUT') {
          cartWrites++; await new Promise(resolve => setTimeout(resolve, 120))
          if (cartWrites === 1) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } }
          else { cart = { ...cart, version: 'cart-v2', items: [{ skuId, quantity: req.postDataJSON().quantity, unitPriceTwd: 1200, availability: 'InStock' }] }; body = cart }
        } else if (path === '/checkout-quotes' && req.method() === 'POST') {
          quoteWrites++; await new Promise(resolve => setTimeout(resolve, 120))
          if (quoteWrites === 1) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } }
          else body = { id: 'quote-issue24-add-quote-cls', mode: 'mock', expiresAt: new Date(Date.now() + 900000).toISOString(), snapshot: { currency: 'TWD', merchandiseTwd: 1200, discountTwd: 0, shippingTwd: 60, totalTwd: 1260 } }
        } else { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
        await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
      })
      const stages = []
      try {
        const labels = words[locale]
        await page.goto(`${origin}/${locale}`, { waitUntil: 'domcontentloaded', timeout: 30000 })
        await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') || {}).__vue_app__))
        const searchAction = locale === 'zh-TW' ? '搜尋商品' : locale === 'ja' ? '商品を検索' : 'Search products'
        const catalogAlert = page.getByRole('alert')
        if (await catalogAlert.isVisible().catch(() => false)) {
          await catalogAlert.getByRole('button', { name: searchAction }).click()
        }
        const add = page.getByRole('button', { name: labels.add }).first()
        await add.waitFor({ state: 'visible', timeout: 20000 })
        await page.locator('.product-card select').first().selectOption(skuId)
        await page.waitForFunction(button => Boolean(button && !button.disabled), await add.elementHandle(), { timeout: 15000 })
        await page.waitForTimeout(150)
        await page.evaluate(() => window.__issue24Shifts.splice(0))
        for (const stage of ['add-failure', 'add-retry']) {
          await page.evaluate(name => { window.__issue24Stage = name; window.__issue24StageStart = performance.now() }, stage)
          const responseWait = page.waitForResponse(r => new URL(r.url()).pathname.endsWith(`/carts/current/items/${skuId}`))
          await add.click()
          const response = await responseWait
          if (response.status() !== (stage === 'add-failure' ? 503 : 200)) throw new Error(`${locale}/${stage}: unexpected status ${response.status()}`)
          if (stage === 'add-failure') await page.getByRole('alert').filter({ hasText: labels.error }).first().waitFor({ state: 'visible' })
          else await page.getByText(skuId, { exact: true }).waitFor({ state: 'visible' })
          await page.waitForTimeout(650)
          const entries = await page.evaluate(() => window.__issue24Shifts.splice(0))
          stages.push({ stage, status: response.status(), entries, cls: sessionWindow(entries) })
        }
        for (const stage of ['quote-failure', 'quote-retry']) {
          await page.evaluate(name => { window.__issue24Stage = name; window.__issue24StageStart = performance.now() }, stage)
          const responseWait = page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/checkout-quotes')
          await page.getByRole('button', { name: labels.quote, exact: true }).click()
          const response = await responseWait
          if (response.status() !== (stage === 'quote-failure' ? 503 : 200)) throw new Error(`${locale}/${stage}: unexpected status ${response.status()}`)
          if (stage === 'quote-failure') await page.getByRole('alert').filter({ hasText: labels.error }).first().waitFor({ state: 'visible' })
          else await page.getByRole('heading', { name: locale === 'zh-TW' ? '結帳報價' : locale === 'ja' ? 'チェックアウト見積もり' : 'Checkout quote' }).waitFor({ state: 'visible' })
          await page.waitForTimeout(650)
          const entries = await page.evaluate(() => window.__issue24Shifts.splice(0))
          stages.push({ stage, status: response.status(), entries, cls: sessionWindow(entries) })
        }
        const record = { locale, sample, stages }
        raw.push(record)
        for (const stage of stages) (summary[locale][stage.stage] ||= []).push(stage.cls)
      } catch (error) { raw.push({ locale, sample, failure: String(error) }); throw error }
      finally { await context.close() }
    }
  }
  const groups = Object.fromEntries(locales.map(locale => [locale, Object.fromEntries(Object.entries(summary[locale]).map(([stage, values]) => [stage, { n: values.length, clsP50: percentile(values, .5), clsP75: percentile(values, .75), clsMax: Math.max(...values) }]))]))
  const result = { runId: 'issue24-add-quote-cls-20261019', startedAt, completedAt: new Date().toISOString(), environment: { os: `${os.type()} ${os.release()} ${os.arch()}`, browser: await browser.version(), viewport: '1440x900 CSS px', cpuThrottle: '4x', network: '150ms latency, 1.6Mbps down, 0.75Mbps up', cache: 'fresh context per locale sample; route cache not separately classified', api: 'Playwright loopback route stubs only; cart writes and quote writes delayed 120ms' }, method: { samplesPerLocale: samples, p75: 'nearest-rank ceil(n*0.75)', cls: 'maximum session window, 1s gap / 5s cap, excluding hadRecentInput=true', settle: '650ms after expected UI state' }, groups, raw }
  await writeFile(outputPath, JSON.stringify(result, null, 2))
  console.log(JSON.stringify({ completedAt: result.completedAt, visits: raw.length, groups }, null, 2))
} finally { await browser.close() }
