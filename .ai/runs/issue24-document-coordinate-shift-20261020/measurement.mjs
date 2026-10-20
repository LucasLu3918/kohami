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
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true })
      const page = await context.newPage(), cdp = await context.newCDPSession(page)
      await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
      await cdp.send('Network.enable')
      await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
      await page.addInitScript(() => {
        Object.defineProperty(window, '__issue24Shifts', { value: [] })
        Object.defineProperty(window, '__issue24ScrollSamples', { value: [] })
        const recordScroll = () => window.__issue24ScrollSamples.push({ at: +performance.now().toFixed(3), scrollY: window.scrollY })
        addEventListener('scroll', recordScroll, { passive: true })
        recordScroll()
        const selectorFor = node => {
          if (!(node instanceof Element)) return { selector: null, unique: false }
          const identity = element => {
            if (element.id) return `#${CSS.escape(element.id)}`
            const testId = element.getAttribute('data-testid')
            if (testId) return `${element.localName}[data-testid="${CSS.escape(testId)}"]`
            const labelledBy = element.getAttribute('aria-labelledby')
            if (labelledBy) return `${element.localName}[aria-labelledby="${CSS.escape(labelledBy)}"]`
            if (element.classList.length) return `${element.localName}${[...element.classList].slice(0, 2).map(name => `.${CSS.escape(name)}`).join('')}`
            const role = element.getAttribute('role')
            return role ? `${element.localName}[role="${CSS.escape(role)}"]` : null
          }
          const direct = identity(node)
          if (direct && document.querySelectorAll(direct).length === 1) return { selector: direct, unique: true }
          const parts = []
          for (let current = node; current && current instanceof Element && parts.length < 6; current = current.parentElement) {
            let part = identity(current) || current.localName
            const parent = current.parentElement
            if (!identity(current) && parent) {
              const peers = [...parent.children].filter(peer => peer.localName === current.localName)
              if (peers.length > 1) part += `:nth-of-type(${peers.indexOf(current) + 1})`
            }
            parts.unshift(part)
            const selector = parts.join(' > ')
            if (document.querySelectorAll(selector).length === 1) return { selector, unique: true }
          }
          const selector = parts.join(' > ')
          return { selector, unique: Boolean(selector) && document.querySelectorAll(selector).length === 1 }
        }
        const visibleInViewport = rect => {
          const viewportWidth = document.documentElement.clientWidth
          const viewportHeight = document.documentElement.clientHeight
          const width = Math.max(0, Math.min(rect.right, viewportWidth) - Math.max(rect.left, 0))
          const height = Math.max(0, Math.min(rect.bottom, viewportHeight) - Math.max(rect.top, 0))
          const area = Math.max(0, rect.width) * Math.max(0, rect.height)
          return { intersects: width > 0 && height > 0, visibleAreaRatio: area ? +(width * height / area).toFixed(4) : 0, viewport: { width: viewportWidth, height: viewportHeight } }
        }
        try { new PerformanceObserver(list => window.__issue24Shifts.push(...list.getEntries().map(entry => ({
          startTime: +entry.startTime.toFixed(3),
          observedAt: +performance.now().toFixed(3),
          scrollYAtObservation: window.scrollY,
          value: entry.value,
          hadRecentInput: entry.hadRecentInput,
          sources: entry.sources?.map(source => {
            const node = source.node
            const previousRect = source.previousRect?.toJSON?.() || null
            const currentRect = source.currentRect?.toJSON?.() || null
            const currentDomRect = node?.getBoundingClientRect()
            const selector = selectorFor(node)
            return {
              node: node?.nodeName || null,
              selector: selector.selector,
              selectorUnique: selector.unique,
              id: node?.id || null,
              classes: node instanceof Element ? [...node.classList] : [],
              role: node?.getAttribute?.('role') || null,
              previousRect,
              currentRect,
              currentDomRect: currentDomRect ? { x: currentDomRect.x, y: currentDomRect.y, width: currentDomRect.width, height: currentDomRect.height } : null,
              previousViewportVisibility: previousRect ? visibleInViewport(previousRect) : null,
              currentViewportVisibility: currentRect ? visibleInViewport(currentRect) : null,
              currentDomViewportVisibility: currentDomRect ? visibleInViewport(currentDomRect) : null,
            }
          }) || [],
        })))).observe({ type: 'layout-shift', buffered: true }) } catch {}
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
      const geometry = []
      const readGeometry = async label => page.evaluate(label => {
        const selectors = ['ul.cart-list', 'section[aria-labelledby="cart-heading"] > label', 'section[aria-labelledby="cart-heading"] > button', 'section[aria-labelledby="checkout-heading"]']
        return { label, at: +performance.now().toFixed(3), scrollY: window.scrollY, elements: selectors.map(selector => { const element = document.querySelector(selector); if (!element) return { selector, present: false }; const rect = element.getBoundingClientRect(); return { selector, present: true, viewportTop: rect.top, documentTop: rect.top + window.scrollY, height: rect.height } }) }
      }, label)
      try {
        const labels = words[locale]
        page.on('pageerror', error => console.error(`PAGEERROR ${locale}/${sample}: ${error.message}`))
        page.on('requestfailed', request => console.error(`REQUESTFAILED ${locale}/${sample}: ${request.url()} ${request.failure()?.errorText}`))
        await page.goto(`${origin}/${locale}`, { waitUntil: 'domcontentloaded', timeout: 30000 })
        await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') || {}).__vue_app__), { timeout: 15000 })
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
          geometry.push(await readGeometry(`${stage}:before`))
          await page.evaluate(name => { window.__issue24Stage = name; window.__issue24StageStart = performance.now() }, stage)
          const responseWait = page.waitForResponse(r => new URL(r.url()).pathname.endsWith(`/carts/current/items/${skuId}`))
          await add.click()
          const response = await responseWait
          if (response.status() !== (stage === 'add-failure' ? 503 : 200)) throw new Error(`${locale}/${stage}: unexpected status ${response.status()}`)
          if (stage === 'add-failure') await page.getByRole('alert').filter({ hasText: labels.error }).first().waitFor({ state: 'visible' })
          else await page.getByText(skuId, { exact: true }).waitFor({ state: 'visible' })
          await page.waitForTimeout(650)
          geometry.push(await readGeometry(`${stage}:after`))
          const entries = await page.evaluate(() => window.__issue24Shifts.splice(0))
          stages.push({ stage, status: response.status(), entries, cls: sessionWindow(entries) })
        }
        for (const stage of ['quote-failure', 'quote-retry']) {
          geometry.push(await readGeometry(`${stage}:before`))
          await page.evaluate(name => { window.__issue24Stage = name; window.__issue24StageStart = performance.now() }, stage)
          const responseWait = page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/checkout-quotes')
          await page.getByRole('button', { name: labels.quote, exact: true }).click()
          const response = await responseWait
          if (response.status() !== (stage === 'quote-failure' ? 503 : 200)) throw new Error(`${locale}/${stage}: unexpected status ${response.status()}`)
          if (stage === 'quote-failure') await page.getByRole('alert').filter({ hasText: labels.error }).first().waitFor({ state: 'visible' })
          else await page.getByRole('heading', { name: locale === 'zh-TW' ? '結帳報價' : locale === 'ja' ? 'チェックアウト見積もり' : 'Checkout quote' }).waitFor({ state: 'visible' })
          await page.waitForTimeout(650)
          geometry.push(await readGeometry(`${stage}:after`))
          const entries = await page.evaluate(() => window.__issue24Shifts.splice(0))
          stages.push({ stage, status: response.status(), entries, cls: sessionWindow(entries) })
        }
        const record = { locale, sample, geometry, stages }
        raw.push(record)
        for (const stage of stages) (summary[locale][stage.stage] ||= []).push(stage.cls)
      } catch (error) { raw.push({ locale, sample, failure: String(error) }); throw error }
      finally { await context.close() }
      if (sample % 5 === 0) console.error(`completed ${sample}/${samples} ${locale}`)
    }
  }
  const groups = Object.fromEntries(locales.map(locale => [locale, Object.fromEntries(Object.entries(summary[locale]).map(([stage, values]) => [stage, { n: values.length, clsP50: percentile(values, .5), clsP75: percentile(values, .75), clsMax: Math.max(...values) }]))]))
  const result = { runId: 'issue24-document-coordinate-shift-20261020', startedAt, completedAt: new Date().toISOString(), environment: { os: `${os.type()} ${os.release()} ${os.arch()}`, browser: await browser.version(), viewport: '1440x900 CSS px', cpuThrottle: '4x', network: '150ms latency, 1.6Mbps down, 0.75Mbps up', cache: 'fresh context per locale sample; route cache not separately classified', api: 'Playwright loopback route stubs only; cart writes and quote writes delayed 120ms' }, method: { samplesPerLocale: samples, p75: 'nearest-rank ceil(n*0.75)', cls: 'maximum session window, 1s gap / 5s cap, excluding hadRecentInput=true', settle: '650ms after expected UI state', recentInputLayoutShift: 'Preserves hadRecentInput and source node selector, uniqueness, rectangles, viewport visibility, observation-time scrollY, and before/after document-coordinate geometry; diagnostic attribution only, not CLS/INP or a pass threshold.' }, groups, raw }
  await writeFile(outputPath, JSON.stringify(result, null, 2))
  console.log(JSON.stringify({ completedAt: result.completedAt, visits: raw.length, groups }, null, 2))
} finally { await browser.close() }
