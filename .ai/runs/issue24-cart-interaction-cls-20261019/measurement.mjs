import { chromium } from '/Users/lucas/Coding/main/kohami/apps/storefront/node_modules/@playwright/test/index.mjs'
import os from 'node:os'
import { writeFile } from 'node:fs/promises'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '30', 10)
if (!Number.isInteger(samples) || samples < 1) throw new Error('ISSUE24_SAMPLES must be a positive integer')
const origin = process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3000'
const locales = ['zh-TW', 'ja', 'en']
const outputPath = process.env.ISSUE24_OUTPUT || new URL('./metrics.json', import.meta.url)
const skuId = 'sku-issue24-cart-cls'
const localeLabels = {
  'zh-TW': { quantity: '數量', subtotal: '小計', error: '目前無法連線至本機服務，請稍後重試。' },
  ja: { quantity: '数量', subtotal: '小計', error: 'ローカルサービスに接続できません。しばらくしてから再試行してください。' },
  en: { quantity: 'Quantity', subtotal: 'Subtotal', error: 'The local service is unavailable. Try again shortly.' },
}
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--no-proxy-server'] })
const startedAt = new Date().toISOString()
const raw = []
const summary = {}

function percentile(values, p) {
  const sorted = [...values].sort((a, b) => a - b)
  return sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)]
}
function sessionWindow(entries) {
  const shifts = entries.filter(entry => !entry.hadRecentInput).sort((a, b) => a.startTime - b.startTime)
  let max = 0, sum = 0, start = 0, previous = 0
  for (const shift of shifts) {
    if (sum === 0 || shift.startTime - previous >= 1000 || shift.startTime - start >= 5000) {
      sum = shift.value
      start = shift.startTime
    } else sum += shift.value
    previous = shift.startTime
    max = Math.max(max, sum)
  }
  return max
}

try {
  for (const locale of locales) {
    const group = []
    summary[locale] = {}
    for (let sample = 1; sample <= samples; sample++) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true })
      const page = await context.newPage()
      const cdp = await context.newCDPSession(page)
      await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
      await cdp.send('Network.enable')
      await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150, downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
      await page.addInitScript(() => {
        Object.defineProperty(window, '__issue24Shifts', { value: [] })
        try {
          new PerformanceObserver(list => window.__issue24Shifts.push(...list.getEntries().map(entry => ({
            startTime: +entry.startTime.toFixed(3), value: entry.value,
            hadRecentInput: entry.hadRecentInput,
            sources: entry.sources?.map(source => ({ node: source.node?.nodeName || null, previousRect: source.previousRect?.toJSON?.() || null, currentRect: source.currentRect?.toJSON?.() || null })) || [],
          }))))
            .observe({ type: 'layout-shift', buffered: true })
        } catch {}
      })
      let writes = 0, cartQuantity = 1
      await page.route('**/api/v1/**', async route => {
        const request = route.request()
        const url = new URL(request.url())
        const path = url.pathname.replace('/api/v1', '')
        let status = 200, body = {}
        if (path === '/auth/session') body = { state: 'Anonymous', mode: 'mock' }
        else if (path === '/auth/csrf') body = { requestToken: 'synthetic-issue24-cart-cls' }
        else if (path === '/products') body = { items: [{ slug: 'cart-cls-shirt', skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1200, availability: 'InStock', translations: [{ locale: 'zh-TW', name: 'CLS 合成襯衫' }, { locale: 'ja', name: 'CLS 合成シャツ' }, { locale: 'en', name: 'CLS synthetic shirt' }] }], nextCursor: null }
        else if (path === '/carts/current' && request.method() === 'GET') body = { id: 'cart-issue24-cls', version: `cart-v${writes + 1}`, items: [{ skuId, quantity: cartQuantity, unitPriceTwd: 1200, availability: 'InStock' }] }
        else if (path === `/carts/current/items/${skuId}` && request.method() === 'PUT') {
          writes++
          await new Promise(resolve => setTimeout(resolve, 120))
          if (writes === 1 || writes === 3) { status = 503; body = { code: 'SERVICE_UNAVAILABLE' } }
          else { cartQuantity = request.postDataJSON().quantity; body = { id: 'cart-issue24-cls', version: `cart-v${writes + 1}`, items: [{ skuId, quantity: cartQuantity, unitPriceTwd: 1200, availability: 'InStock' }] } }
        } else if (path === '/carts' && request.method() === 'POST') body = { id: 'cart-issue24-cls', version: 'cart-v1', items: [] }
        else { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
        await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
      })
      try {
        await page.goto(`${origin}/${locale}/cart`, { waitUntil: 'domcontentloaded', timeout: 30000 })
        const labels = localeLabels[locale]
        const quantity = page.locator('input[type="number"]')
        await quantity.waitFor({ state: 'visible', timeout: 15000 })
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
        await page.evaluate(() => window.__issue24Shifts.splice(0))
        const stages = []
        for (const stage of ['quantity-failure', 'quantity-recovery', 'quantity-second-failure', 'quantity-second-recovery']) {
          const firstFailure = stage.endsWith('failure') && (stage === 'quantity-failure' || stage === 'quantity-second-failure')
          await page.evaluate(name => { window.__issue24Stage = name; window.__issue24StageStart = performance.now() }, stage)
          await quantity.fill(firstFailure ? (stage === 'quantity-failure' ? '2' : '3') : (stage === 'quantity-recovery' ? '2' : '3'))
          const response = page.waitForResponse(r => new URL(r.url()).pathname.includes(`/carts/current/items/${skuId}`))
          await quantity.dispatchEvent('change')
          const result = await response
          if (firstFailure && result.status() !== 503) throw new Error(`${locale}/${stage}: expected synthetic 503, got ${result.status()}`)
          if (!firstFailure && result.status() !== 200) throw new Error(`${locale}/${stage}: expected synthetic 200, got ${result.status()}`)
          if (firstFailure) await page.getByRole('alert').filter({ hasText: labels.error }).waitFor({ state: 'visible' })
          else await page.getByText(`${labels.subtotal}: TWD ${stage.endsWith('recovery') && stage === 'quantity-recovery' ? '2400' : '3600'}`).waitFor({ state: 'visible' })
          await page.waitForTimeout(650)
          const entries = await page.evaluate(() => window.__issue24Shifts.splice(0).map(entry => ({ ...entry })))
          stages.push({ stage, status: result.status(), entries, cls: sessionWindow(entries) })
        }
        const record = { locale, sample, status: 200, stages }
        raw.push(record); group.push(record)
      } catch (error) {
        raw.push({ locale, sample, failure: String(error) })
        throw error
      } finally {
        await context.close()
      }
      if (sample % 5 === 0) console.error(`completed ${sample}/${samples} ${locale}`)
    }
    for (const stage of group[0].stages) {
      const values = group.map(record => record.stages.find(item => item.stage === stage.stage).cls)
      const entries = group.flatMap(record => record.stages.find(item => item.stage === stage.stage).entries)
      summary[locale][stage.stage] = { samples: values.length, p50: +percentile(values, 0.5).toFixed(4), p75: +percentile(values, 0.75).toFixed(4), max: +Math.max(...values).toFixed(4), observedShiftEntries: entries.length, scoredShiftEntries: entries.filter(entry => !entry.hadRecentInput).length, excludedRecentInputShiftEntries: entries.filter(entry => entry.hadRecentInput).length }
    }
  }
  const output = { profile: { browser: `Chrome ${browser.version()}`, platform: `${os.platform()}/${os.arch()}`, viewportCssPx: '1440x900', cpuThrottle: '4x', network: { latencyMs: 150, downloadMbps: 1.6, uploadMbps: 0.75 }, syntheticApiResponseDelayMs: 120, samplesPerLocaleStage: samples, locales, origin, startedAt, finishedAt: new Date().toISOString(), measurement: 'CLS session-window max per interaction stage; entries with hadRecentInput are excluded from scored CLS, and all observed entry metadata is retained. Each stage waits 650ms after UI settlement. P75 uses nearest-rank ceil(n*0.75).' }, summary, raw, limitation: 'Controlled localhost synthetic desktop lab only. These interaction-stage CLS observations are diagnostic and are not field Core Web Vitals, formal INP, or an SLA. The n=30 interaction-stage results do not establish a population field percentile.' }
  await writeFile(outputPath, JSON.stringify(output, null, 2) + '\n')
  console.log(JSON.stringify({ profile: output.profile, summary, journeys: raw.length, rawPath: outputPath }, null, 2))
} finally { await browser.close() }
