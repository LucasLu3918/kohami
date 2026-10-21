import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const testDir = dirname(fileURLToPath(import.meta.url))
const storefrontDir = resolve(testDir, '..')
const checkoutId = 'checkout-stable-foundation-ac11'
const forwardedCookies = []
const status = (id) => ({
  checkoutId: id,
  mode: 'mock',
  version: 'synthetic-version',
  orderState: id === 'checkout-failed' ? 'Cancelled' : 'Confirmed',
  paymentState: id === 'checkout-failed' ? 'Failed' : id === 'checkout-unknown' ? 'Unknown' : 'Succeeded',
  compensationRequired: false,
  expiresAt: '2026-10-16T12:00:00Z',
  snapshot: {
    currency: 'TWD', merchandiseTwd: 500, discountTwd: 0, shippingTwd: 60, totalTwd: 560,
    lines: [{ skuId: 'sku_demo_01', quantity: 1, unitPriceTwd: 500, name: { value: 'Synthetic item A' } }],
  },
})

const api = createServer((request, response) => {
  forwardedCookies.push(request.headers.cookie || '')
  const match = request.url?.match(/^\/api\/v1\/checkouts\/([A-Za-z0-9_-]+)\/status$/)
  if (request.method !== 'GET' || !match) {
    response.writeHead(404, { 'content-type': 'application/problem+json' })
    response.end(JSON.stringify({ code: 'RESOURCE_NOT_FOUND' }))
    return
  }
  if (match[1] === 'checkout-denied') {
    response.writeHead(404, { 'content-type': 'application/problem+json' })
    response.end(JSON.stringify({ code: 'RESOURCE_NOT_FOUND', traceId: 'synthetic-trace' }))
    return
  }
  response.writeHead(200, { 'content-type': 'application/json', 'cache-control': 'no-store' })
  response.end(JSON.stringify(status(match[1])))
})

let nuxt
try {
  await new Promise((resolveListen, reject) => {
    api.once('error', reject)
    api.listen(5080, '127.0.0.1', resolveListen)
  })

  nuxt = spawn(process.execPath, ['.output/server/index.mjs'], {
    cwd: storefrontDir,
    env: { ...process.env, NODE_ENV: 'production', NITRO_HOST: 'localhost', NITRO_PORT: '3000' },
    stdio: 'ignore',
  })

  let ready = false
  for (let attempt = 0; attempt < 80; attempt++) {
    if (nuxt.exitCode !== null) throw new Error(`NUXT_EXITED_${nuxt.exitCode}`)
    try {
      await fetch('http://localhost:3000/')
      ready = true
      break
    } catch {}
    await delay(250)
  }
  assert.equal(ready, true, 'Nuxt production server did not become ready')

  const cases = [
    { lang: 'zh-TW', title: '模擬結帳結果', payment: '模擬成功', item: '合成示意商品 A' },
    { lang: 'ja', title: '模擬チェックアウト結果', payment: '模擬成功', item: 'デモ商品 A' },
    { lang: 'en', title: 'Mock checkout result', payment: 'Mock success', item: 'Demo item A' },
  ]
  const htmlByLocale = new Map()
  for (const item of cases) {
      const response = await fetch(`http://localhost:3000/checkout/${checkoutId}?lang=${item.lang}`, {
      headers: { cookie: 'kohami.guest.read=synthetic-capability' },
    })
    const html = await response.text()
    htmlByLocale.set(item.lang, html)
    assert.equal(response.status, 200)
    assert.match(response.headers.get('cache-control') || '', /no-store/i)
    assert.match(html, new RegExp(`<html[^>]*lang="${item.lang}"`))
    assert.match(html, /name="robots" content="noindex,nofollow"/)
    assert.match(html, new RegExp(item.title))
    assert.match(html, new RegExp(item.payment))
    assert.match(html, new RegExp(item.item))
    assert.match(html, /TWD 560/)
    assert.match(html, new RegExp(checkoutId))
    assert.match(html, /模擬|シミュレーション|Mock/)
  }

  for (const html of htmlByLocale.values()) assert.match(html, new RegExp(checkoutId))
  const failedResponse = await fetch('http://localhost:3000/checkout/checkout-failed?lang=ja')
  const failedHtml = await failedResponse.text()
  assert.equal(failedResponse.status, 200)
  assert.match(failedHtml, /模擬支払は拒否されました/)
  assert.match(failedHtml, /checkout-failed/)
  const unknownResponse = await fetch('http://localhost:3000/checkout/checkout-unknown?lang=en')
  const unknownHtml = await unknownResponse.text()
  assert.match(unknownHtml, /Do not start payment again/)
  const deniedResponse = await fetch('http://localhost:3000/checkout/checkout-denied?lang=en')
  const deniedHtml = await deniedResponse.text()
  assert.equal(deniedResponse.status, 200)
  assert.match(deniedHtml, /This checkout result is unavailable/)
  assert.doesNotMatch(deniedHtml, /synthetic-trace|TWD 560/)
  assert.ok(forwardedCookies.includes('kohami.guest.read=synthetic-capability'), 'SSR must forward the request cookie to the same-origin API proxy')

  console.log('PASS Foundation-AC11 SSR HTML: zh-TW/ja/en summary and mock labeling, noindex/no-store, stable transaction ID, localized failure/unknown result, and cookie forwarding')
} finally {
  if (nuxt && nuxt.exitCode === null) {
    nuxt.kill('SIGTERM')
    await Promise.race([new Promise(resolveExit => nuxt.once('exit', resolveExit)), delay(5000)])
    if (nuxt.exitCode === null) nuxt.kill('SIGKILL')
  }
  if (api.listening) await new Promise(resolveClose => api.close(resolveClose))
}
