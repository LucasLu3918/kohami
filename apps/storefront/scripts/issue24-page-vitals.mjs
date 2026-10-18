import { chromium } from '@playwright/test'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '30', 10)
if (!Number.isInteger(samples) || samples < 1) throw new Error('ISSUE24_SAMPLES must be a positive integer')
const locales = (process.env.ISSUE24_LOCALES || 'zh-TW,ja,en').split(',').map(value => value.trim())
if (!locales.length || locales.some(locale => !['zh-TW', 'ja', 'en'].includes(locale))) throw new Error('ISSUE24_LOCALES must contain zh-TW, ja, and/or en')
const fixedServerDelayMs = 120
const pages = [
  { id: 'home', path: locale => `/${locale}` },
  { id: 'policies', path: locale => `/${locale}/policies` },
  { id: 'cart', path: locale => `/${locale}/cart` },
]
const browser = await chromium.launch({ channel: 'chrome', headless: true })
const results = new Map()
const failures = []

function percentile(values, p) {
  const sorted = [...values].sort((a, b) => a - b)
  return sorted[Math.max(0, Math.ceil(sorted.length * p) - 1)]
}

function round(value) { return +value.toFixed(1) }

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true })
  for (const locale of locales) {
    for (const target of pages) {
      const key = `${locale}/${target.id}`
      const samplesForPage = []
      for (let sample = 0; sample < samples; sample += 1) {
        const page = await context.newPage()
        const cdp = await context.newCDPSession(page)
        await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
        await cdp.send('Network.enable')
        await cdp.send('Network.emulateNetworkConditions', {
          offline: false,
          latency: 150,
          downloadThroughput: 1.6 * 1024 * 1024 / 8,
          uploadThroughput: 750 * 1024 / 8,
          connectionType: 'cellular3g',
        })
        await page.addInitScript(() => {
          const state = { lcp: [], shifts: [] }
          Object.defineProperty(window, '__issue24PageVitals', { value: state })
          try {
            new PerformanceObserver(list => state.lcp.push(...list.getEntries().map(entry => entry.startTime)))
              .observe({ type: 'largest-contentful-paint', buffered: true })
            new PerformanceObserver(list => state.shifts.push(...list.getEntries()
              .filter(entry => !entry.hadRecentInput)
              .map(entry => ({ startTime: entry.startTime, value: entry.value })))
            ).observe({ type: 'layout-shift', buffered: true })
          } catch {}
        })

        await page.route('**/api/v1/**', async route => {
          const request = route.request()
          const url = new URL(request.url())
          const path = url.pathname.replace('/api/v1', '')
          let status = 200
          let body = {}
          if (path === '/auth/session') body = { state: 'Anonymous', mode: 'mock' }
          else if (path === '/auth/csrf') body = { requestToken: 'synthetic-issue24-vitals' }
          else if (path === '/products') body = { items: [], nextCursor: null }
          else if (path === '/carts/current') body = { id: 'cart-issue24-vitals', version: 'cart-v1', items: [] }
          else { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
          await new Promise(resolve => setTimeout(resolve, fixedServerDelayMs))
          await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
        })
        page.on('pageerror', error => failures.push({ page: key, sample: sample + 1, message: error.message }))

        const response = await page.goto(`http://localhost:3000${target.path(locale)}`, { waitUntil: 'networkidle' })
        await page.locator('#main-content h1').waitFor({ state: 'visible' })
        await page.evaluate(() => document.fonts.ready)
        await page.waitForTimeout(500)
        const metrics = await page.evaluate(() => {
          const lcpEntries = window.__issue24PageVitals.lcp
          const shifts = [...window.__issue24PageVitals.shifts].sort((a, b) => a.startTime - b.startTime)
          let cls = 0
          let sessionValue = 0
          let sessionStart = 0
          let previousShift = 0
          for (const shift of shifts) {
            if (sessionValue === 0 || shift.startTime - previousShift >= 1000 || shift.startTime - sessionStart >= 5000) {
              sessionValue = shift.value
              sessionStart = shift.startTime
            } else sessionValue += shift.value
            previousShift = shift.startTime
            cls = Math.max(cls, sessionValue)
          }
          return { lcpMs: lcpEntries.length ? Math.max(...lcpEntries) : null, cls }
        })
        if (response?.status() !== 200 || metrics.lcpMs === null) {
          failures.push({ page: key, sample: sample + 1, status: response?.status() ?? null, lcpObserved: metrics.lcpMs !== null })
        }
        samplesForPage.push({ ...metrics, status: response?.status() ?? null })
        await page.close()
        if ((sample + 1) % 10 === 0) console.error(`completed ${sample + 1}/${samples} ${key}`)
      }
      results.set(key, samplesForPage)
    }
  }

  const summary = Object.fromEntries([...results].map(([key, values]) => {
    const lcp = values.map(value => value.lcpMs).filter(Number.isFinite)
    const cls = values.map(value => value.cls).filter(Number.isFinite)
    return [key, {
      samples: values.length,
      lcp: { observed: lcp.length, p50Ms: lcp.length ? round(percentile(lcp, 0.5)) : null, p75Ms: lcp.length ? round(percentile(lcp, 0.75)) : null, maxMs: lcp.length ? round(Math.max(...lcp)) : null },
      cls: { observed: cls.length, p50: cls.length ? +percentile(cls, 0.5).toFixed(4) : null, p75: cls.length ? +percentile(cls, 0.75).toFixed(4) : null, max: cls.length ? +Math.max(...cls).toFixed(4) : null },
      status200: values.filter(value => value.status === 200).length,
    }]
  }))
  const output = {
    profile: {
      browser: `Chrome ${browser.version()}`,
      platform: `${process.platform}/${process.arch}`,
      viewport: '1440x900',
      cpuSlowdown: '4x',
      network: '150ms latency, 1.6 Mbps down, 750 Kbps up',
      mockApiResponseDelayMs: fixedServerDelayMs,
      samplesPerRoute: samples,
      locales,
      cache: 'One browser context reused for all routes; route assets are warm after first navigation.',
      observation: 'LCP observer buffered on navigation; CLS uses maximum session window with 1s gap and 5s window, excluding hadRecentInput entries; observe 500ms after network idle and fonts ready.',
    },
    routes: summary,
    failures,
    limitation: 'Local synthetic route fixtures and desktop Chrome diagnostics only; not field Core Web Vitals, actual network or SLA evidence.',
  }
  console.log(JSON.stringify(output, null, 2))
  if (failures.length || Object.values(summary).some(item => item.lcp.observed !== samples || item.cls.observed !== samples || item.status200 !== samples)) process.exitCode = 1
} finally {
  await browser.close()
}
