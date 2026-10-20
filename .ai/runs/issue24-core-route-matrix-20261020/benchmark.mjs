import { chromium } from '/Users/lucas/Coding/main/kohami/apps/storefront/node_modules/@playwright/test/index.mjs'
import { writeFile } from 'node:fs/promises'
import os from 'node:os'
const origin = 'http://127.0.0.1:3199'
const samplesPerCache = Number.parseInt(process.env.ISSUE24_SAMPLES_PER_CACHE || '15', 10)
if (!Number.isInteger(samplesPerCache) || samplesPerCache < 1) throw new Error('ISSUE24_SAMPLES_PER_CACHE must be a positive integer')
const outputPath = process.env.ISSUE24_OUTPUT || '.ai/runs/issue24-core-route-matrix-20261020/metrics.json'
const locales = ['zh-TW', 'ja', 'en']
const targets = [
  { id: 'home-search-list', path: locale => `/${locale}` },
  { id: 'product-detail', path: locale => `/${locale}/products/issue24-core-lab-shirt` },
  { id: 'cart-checkout-form', path: locale => `/${locale}/cart` },
  { id: 'checkout-result', path: locale => `/${locale}/checkout/issue24-core-lab-checkout` },
]
const results = []
const failures = []
const cacheEvents = []
function percentile(xs, p) { const sorted = [...xs].sort((a,b) => a-b); return sorted.length ? sorted[Math.max(0, Math.ceil(sorted.length*p)-1)] : null }
function summarize(values) {
  const lcp = values.map(x => x.lcpMs).filter(Number.isFinite)
  const cls = values.map(x => x.cls).filter(Number.isFinite)
  const lcpP50 = percentile(lcp, .5), lcpP75 = percentile(lcp, .75)
  const clsP50 = percentile(cls, .5), clsP75 = percentile(cls, .75)
  return { samples: values.length, lcpObserved: lcp.length, clsObserved: cls.length,
    lcpP50Ms: lcpP50 === null ? null : +lcpP50.toFixed(1), lcpP75Ms: lcpP75 === null ? null : +lcpP75.toFixed(1), lcpMaxMs: lcp.length ? +Math.max(...lcp).toFixed(1) : null,
    clsP50: clsP50 === null ? null : +clsP50.toFixed(4), clsP75: clsP75 === null ? null : +clsP75.toFixed(4), clsMax: cls.length ? +Math.max(...cls).toFixed(4) : null,
    http200: values.filter(x => x.status === 200).length, pageErrors: values.reduce((a,x) => a+x.pageErrors.length, 0),
    cacheHits: values.reduce((a,x) => a+x.cacheHits, 0), cacheableAssetResponses: values.reduce((a,x) => a+x.cacheableAssetResponses, 0) }
}
async function configure(page, cacheDisabled) {
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
  await cdp.send('Network.enable')
  await cdp.send('Network.setCacheDisabled', { cacheDisabled })
  await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150,
    downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
  let cacheHits = 0, cacheableAssetResponses = 0
  cdp.on('Network.requestServedFromCache', () => cacheHits++)
  cdp.on('Network.responseReceived', e => { if (e.response.fromDiskCache || e.response.fromServiceWorker) cacheHits++ })
  page.on('response', response => { if (/\/_nuxt\//.test(response.url()) && response.status() === 200) cacheableAssetResponses++ })
  return { cacheCounts: () => ({ cacheHits, cacheableAssetResponses }), detach: () => cdp.detach() }
}
async function capture(locale, target, cacheMode, index, context) {
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', e => errors.push(e.message))
  const timing = await configure(page, cacheMode === 'cold')
  await page.addInitScript(() => {
    const state = { lcp: [], shifts: [] }
    Object.defineProperty(window, '__issue24CoreVitals', { value: state })
    try {
      new PerformanceObserver(list => state.lcp.push(...list.getEntries().map(entry => entry.startTime)))
        .observe({ type: 'largest-contentful-paint', buffered: true })
      new PerformanceObserver(list => state.shifts.push(...list.getEntries().filter(entry => !entry.hadRecentInput)
        .map(entry => ({ startTime: entry.startTime, value: entry.value })))
      ).observe({ type: 'layout-shift', buffered: true })
    } catch {}
  })
  let response
  try {
    response = await page.goto(`${origin}${target.path(locale)}`, { waitUntil: 'networkidle', timeout: 30000 })
    await page.locator('#main-content h1').waitFor({ state: 'visible', timeout: 10000 })
    await page.evaluate(() => document.fonts.ready)
    await page.waitForTimeout(500)
    const metrics = await page.evaluate(() => {
      const lcpEntries = window.__issue24CoreVitals.lcp
      const shifts = [...window.__issue24CoreVitals.shifts].sort((a,b) => a.startTime-b.startTime)
      let cls = 0, sum = 0, start = 0, previous = 0
      for (const shift of shifts) {
        if (sum === 0 || shift.startTime-previous >= 1000 || shift.startTime-start >= 5000) { sum=shift.value; start=shift.startTime }
        else sum += shift.value
        previous = shift.startTime
        cls = Math.max(cls, sum)
      }
      return { lcpMs: lcpEntries.length ? Math.max(...lcpEntries) : null, cls }
    })
    const counts = timing.cacheCounts()
    const record = { locale, route: target.id, path: target.path(locale), cache: cacheMode, sample: index,
      status: response?.status() ?? null, ...metrics, ...counts, pageErrors: errors }
    results.push(record)
    if (record.status !== 200 || record.lcpMs === null || errors.length) failures.push(record)
    return record
  } catch (error) {
    const record = { locale, route: target.id, path: target.path(locale), cache: cacheMode, sample: index,
      status: response?.status() ?? null, lcpMs: null, cls: null, ...timing.cacheCounts(), pageErrors: [...errors, String(error)] }
    results.push(record); failures.push(record); return record
  } finally { await timing.detach(); await page.close() }
}
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--no-proxy-server'] })
try {
  const browserVersion = browser.version()
  for (const locale of locales) {
    const completedGroups = []
  for (const target of targets) {
      for (let i=1; i<=samplesPerCache; i++) {
        const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
        await capture(locale, target, 'cold', i, context)
        await context.close()
      }
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
      await capture(locale, target, 'warmup', 0, context)
      for (let i=1; i<=samplesPerCache; i++) await capture(locale, target, 'warm', i, context)
      await context.close()
      completedGroups.push(`${locale}/${target.id}`)
      const checkpointGroups = {}
      for (const item of results) if (item.cache === 'cold' || item.cache === 'warm') {
        const key = `${item.locale}/${item.route}/${item.cache}`
        checkpointGroups[key] = summarize(results.filter(x => x.locale===item.locale && x.route===item.route && x.cache===item.cache))
      }
      await writeFile(outputPath, JSON.stringify({ completion: 'partial', profile: { browser: `Chrome ${browser.version()}`, platform: `${os.platform()}/${os.arch()}`, viewportCssPx: '1440x900', cpuThrottle: '4x', network: { latencyMs: 150, downloadMbps: 1.6, uploadMbps: .75 }, syntheticApiDelayMs: 120, samplesPerLocaleRouteCache: samplesPerCache, hardware: JSON.parse(process.env.ISSUE24_HARDWARE_JSON || '{}'), completedGroups, groups: checkpointGroups }, failures, raw: results }, null, 2) + '\n')
      console.error(`completed ${locale}/${target.id}: 15 cold + 15 warm`)
    }
  }
  const grouped = {}
  for (const locale of locales) for (const target of targets) for (const cache of ['cold','warm']) {
    const key = `${locale}/${target.id}/${cache}`
    grouped[key] = summarize(results.filter(x => x.locale===locale && x.route===target.id && x.cache===cache))
  }
  const output = { completion: failures.length ? 'completed_with_failures' : 'complete', profile: { browser: `Chrome ${browserVersion}`, platform: `${os.platform()}/${os.arch()}`,
    viewportCssPx: '1440x900', cpuThrottle: '4x', network: { latencyMs: 150, downloadMbps: 1.6, uploadMbps: .75 },
    syntheticApiDelayMs: 120, samplesPerLocaleRouteCache: samplesPerCache,
    hardware: JSON.parse(process.env.ISSUE24_HARDWARE_JSON || '{}'),
    cache: 'cold: new context per navigation with browser cache disabled; warm: one context per locale/route after one discarded warmup navigation; CDP cache events collected.',
    measurement: 'Navigation LCP is max buffered LargestContentfulPaint startTime; CLS is maximum session window (1s gap, 5s length), excluding hadRecentInput; wait networkidle, visible main h1, fonts.ready and 500ms.',
    routes: targets.map(x=>x.id), locales, startedAt: process.env.ISSUE24_STARTED_AT || null, finishedAt: new Date().toISOString() },
    groups: grouped, failures, raw: results,
    limitation: 'Desktop localhost synthetic lab only. No field Core Web Vitals, formal INP, real API/SQL/payment/LAN, mobile, or SLA claim.' }
  await writeFile(outputPath, JSON.stringify(output,null,2)+'\n')
  console.log(JSON.stringify({ profile: output.profile, groups: output.groups, failureCount: failures.length,
    totalNavigations: results.length, rawPath: outputPath },null,2))
  if (failures.length || results.filter(x => x.cache === 'cold' || x.cache === 'warm').length !== locales.length * targets.length * samplesPerCache * 2) process.exitCode = 1
} finally { await browser.close() }
