import { chromium } from '/Users/lucas/Coding/main/kohami/apps/storefront/node_modules/playwright/index.mjs'
import { writeFile } from 'node:fs/promises'

const samples = Number.parseInt(process.env.ISSUE24_SAMPLES || '10', 10)
const locales = ['zh-TW', 'ja', 'en']
const pages = [
  { id: 'home', path: locale => `/${locale}` },
  { id: 'policies', path: locale => `/${locale}/policies` },
  { id: 'cart', path: locale => `/${locale}/cart` },
]
if (!Number.isInteger(samples) || samples < 1) throw new Error('ISSUE24_SAMPLES must be a positive integer')

const browser = await chromium.launch({ channel: 'chrome', headless: true })
const groups = {}
const failures = []

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
  for (const locale of locales) {
    for (const target of pages) {
      const key = `${locale}/${target.id}`
      groups[key] = []
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
          const state = { longTasks: [], longTaskSupported: PerformanceObserver.supportedEntryTypes.includes('longtask') }
          Object.defineProperty(window, '__issue24Tbt', { value: state })
          if (state.longTaskSupported) {
            new PerformanceObserver(list => state.longTasks.push(...list.getEntries().map(entry => ({
              startTime: entry.startTime,
              duration: entry.duration,
              name: entry.name,
            })))).observe({ type: 'longtask', buffered: true })
          }
        })

        await page.route('**/api/v1/**', async route => {
          const requestPath = new URL(route.request().url()).pathname.replace('/api/v1', '')
          let status = 200
          let body = {}
          if (requestPath === '/auth/session') body = { state: 'Anonymous', mode: 'mock' }
          else if (requestPath === '/auth/csrf') body = { requestToken: 'synthetic-issue24-tbt' }
          else if (requestPath === '/products') body = { items: [], nextCursor: null }
          else if (requestPath === '/carts/current') body = { id: 'cart-issue24-tbt', version: 'cart-v1', items: [] }
          else { status = 404; body = { code: 'RESOURCE_NOT_FOUND' } }
          await new Promise(resolve => setTimeout(resolve, 120))
          await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
        })
        page.on('pageerror', error => failures.push({ page: key, sample: sample + 1, error: error.message }))

        const response = await page.goto(`http://127.0.0.1:3000${target.path(locale)}`, { waitUntil: 'networkidle' })
        await page.locator('#main-content h1').waitFor({ state: 'visible' })
        await page.evaluate(() => document.fonts.ready)
        const quietStart = await page.evaluate(() => performance.now())
        await page.waitForTimeout(5000)
        const measured = await page.evaluate((windowEnd) => {
          const paints = performance.getEntriesByType('paint')
          const fcp = paints.find(entry => entry.name === 'first-contentful-paint')?.startTime ?? null
          const tasks = window.__issue24Tbt.longTasks
          const inWindow = fcp === null ? [] : tasks.filter(task => task.startTime >= fcp && task.startTime < windowEnd)
          const blockingMs = inWindow.reduce((sum, task) => sum + Math.max(0, task.duration - 50), 0)
          return {
            fcpMs: fcp,
            quietStartMs: windowEnd - 5000,
            windowEndMs: windowEnd,
            longTaskSupported: window.__issue24Tbt.longTaskSupported,
            longTasks: inWindow,
            tbtEstimateMs: Math.round(blockingMs * 10) / 10,
          }
        }, quietStart + 5000)
        const row = { status: response?.status() ?? null, ...measured }
        groups[key].push(row)
        if (row.status !== 200 || !row.longTaskSupported || row.fcpMs === null) failures.push({ page: key, sample: sample + 1, reason: 'missing_status_fcp_or_longtask_support', status: row.status, fcpMs: row.fcpMs, longTaskSupported: row.longTaskSupported })
        await cdp.detach()
        await page.close()
        console.error(`completed ${sample + 1}/${samples} ${key}: ${row.tbtEstimateMs} ms`)
      }
    }
  }

  const percentile = (values, p) => [...values].sort((a, b) => a - b)[Math.max(0, Math.ceil(values.length * p) - 1)]
  const summary = Object.fromEntries(Object.entries(groups).map(([key, rows]) => {
    const values = rows.map(row => row.tbtEstimateMs).filter(Number.isFinite)
    return [key, {
      samples: values.length,
      tbtEstimateMs: values.length ? {
        p50: percentile(values, 0.5),
        p75: percentile(values, 0.75),
        max: Math.max(...values),
      } : null,
      fcpObserved: rows.filter(row => row.fcpMs !== null).length,
      status200: rows.filter(row => row.status === 200).length,
      longTaskCount: rows.reduce((sum, row) => sum + row.longTasks.length, 0),
    }]
  }))
  const output = {
    profile: {
      browser: `Google Chrome ${browser.version()}`,
      platform: `${process.platform}/${process.arch}`,
      viewport: '1440x900 CSS px',
      cpuSlowdown: '4x',
      network: '150ms latency, 1.6 Mbps down, 750 Kbps up',
      syntheticApiDelayMs: 120,
      samplesPerLocaleRoute: samples,
      locales,
      routeIds: pages.map(page => page.id),
      cache: 'shared browser context; synthetic route fixture; route assets warm after initial navigations',
      tbtWindow: 'FCP through 5 seconds after Playwright networkidle; sum max(0, long-task duration - 50ms) for tasks starting in the window',
      methodLimit: 'Trace-based TTI and Lighthouse scoring were not available. This is a reproducible Long Tasks API TBT estimate over a declared window, not an official Lighthouse TBT result or field INP.',
    },
    summary,
    groups,
    failures,
  }
  await writeFile(new URL('./metrics.json', import.meta.url), `${JSON.stringify(output, null, 2)}\n`)
  console.log(JSON.stringify({ profile: output.profile, summary, failures }, null, 2))
  if (failures.length || Object.values(summary).some(group => group.samples !== samples || group.fcpObserved !== samples || group.status200 !== samples)) process.exitCode = 1
} finally {
  await browser.close()
}
