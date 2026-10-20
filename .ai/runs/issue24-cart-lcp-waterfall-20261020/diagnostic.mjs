import { chromium } from '../../../apps/storefront/node_modules/@playwright/test/index.mjs'
import { writeFile } from 'node:fs/promises'
import os from 'node:os'
const origin = process.env.ISSUE24_ORIGIN || 'http://127.0.0.1:3199'
const locales = ['zh-TW', 'ja', 'en']
const rounds = 3
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--no-proxy-server'] })
const rows = []
try {
  for (const locale of locales) for (let sample = 1; sample <= rounds; sample++) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
    const page = await context.newPage()
    const cdp = await context.newCDPSession(page)
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
    await cdp.send('Network.enable')
    await cdp.send('Network.setCacheDisabled', { cacheDisabled: true })
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 150,
      downloadThroughput: 1.6 * 1024 * 1024 / 8, uploadThroughput: 750 * 1024 / 8, connectionType: 'cellular3g' })
    await page.addInitScript(() => {
      window.__issue24Lcp = []
      window.__issue24Api = []
      new PerformanceObserver(list => window.__issue24Lcp.push(...list.getEntries().map(entry => {
        const el = entry.element
        return { startTime: +entry.startTime.toFixed(1), size: entry.size, tag: el?.tagName || null,
          id: el?.id || null, classes: typeof el?.className === 'string' ? el.className : '',
          text: el?.textContent?.trim().replace(/\s+/g, ' ').slice(0, 100) || '' }
      }))).observe({ type: 'largest-contentful-paint', buffered: true })
      addEventListener('fetch', () => {})
      new PerformanceObserver(list => window.__issue24Api.push(...list.getEntries()
        .filter(entry => new URL(entry.name).pathname.startsWith('/api/'))
        .map(entry => ({ path: new URL(entry.name).pathname, startTime: +entry.startTime.toFixed(1),
          responseStart: +entry.responseStart.toFixed(1), responseEnd: +entry.responseEnd.toFixed(1), duration: +entry.duration.toFixed(1) }))))
        .observe({ type: 'resource', buffered: true })
    })
    const apiResponses = []
    page.on('response', response => { if (new URL(response.url()).pathname.startsWith('/api/')) apiResponses.push({ path: new URL(response.url()).pathname, status: response.status() }) })
    const errors = []
    page.on('pageerror', error => errors.push(error.message))
    let status = null
    try {
      const response = await page.goto(`${origin}/${locale}/cart`, { waitUntil: 'networkidle', timeout: 30000 })
      status = response?.status() ?? null
      await page.locator('#main-content h1').waitFor({ state: 'visible', timeout: 10000 })
      await page.evaluate(() => document.fonts.ready)
      await page.waitForTimeout(500)
      const data = await page.evaluate(() => ({
        lcpCandidates: window.__issue24Lcp,
        apiTiming: window.__issue24Api,
        resources: performance.getEntriesByType('resource').map(entry => ({
          path: new URL(entry.name).pathname, initiatorType: entry.initiatorType,
          startTime: +entry.startTime.toFixed(1), responseStart: +entry.responseStart.toFixed(1),
          responseEnd: +entry.responseEnd.toFixed(1), duration: +entry.duration.toFixed(1),
          transferSize: entry.transferSize, encodedBodySize: entry.encodedBodySize, decodedBodySize: entry.decodedBodySize,
        })).filter(entry => entry.path.startsWith('/_nuxt/') || entry.path.startsWith('/api/')),
      }))
      rows.push({ locale, sample, status, ...data, apiResponses, errors })
    } catch (error) { rows.push({ locale, sample, status, apiResponses, errors: [...errors, String(error)] }) }
    await cdp.detach()
    await context.close()
  }
  const result = { profile: { browser: `Chrome ${browser.version()}`, platform: `${os.platform()}/${os.arch()}`,
    hardware: { model: 'MacBook Pro Mac17,8', chip: 'Apple M5 Pro', cpuCores: '18 (6 Super, 12 Performance)', memoryGB: 48, os: 'macOS 26.6 (25G72)' },
    viewportCssPx: '1440x900', cpuThrottle: '4x', network: { latencyMs: 150, downloadMbps: 1.6, uploadMbps: 0.75 },
    cache: 'Cold per sample: new browser context, Chrome cache disabled.', syntheticApiDelayMs: 120,
    samplesPerLocale: rounds, startedAt: process.env.ISSUE24_STARTED_AT || null, finishedAt: new Date().toISOString(),
    route: '/{locale}/cart', origin }, rows, limitation: 'Small-sample lab attribution only; not a P75 acceptance estimate, field INP/Core Web Vitals, or SLA.' }
  const out = new URL('./diagnostic.json', import.meta.url)
  await writeFile(out, JSON.stringify(result, null, 2) + '\n')
  console.log(JSON.stringify({ browser: result.profile.browser, rows: rows.map(row => ({ locale: row.locale, sample: row.sample, status: row.status, lcp: row.lcpCandidates?.at(-1), apiResponses: row.apiResponses, errors: row.errors })), output: out.pathname }, null, 2))
  if (rows.length !== locales.length * rounds || rows.some(row => row.status !== 200 || row.errors?.length || row.apiResponses?.some(x => x.status !== 200))) process.exitCode = 1
} finally { await browser.close() }
