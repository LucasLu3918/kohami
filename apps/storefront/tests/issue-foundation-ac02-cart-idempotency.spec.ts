import { expect, test } from '@playwright/test'

test('Foundation-AC02 replays one cart after a lost response and browser token rotation', async ({ page, context }) => {
  test.setTimeout(90_000)
  const api = 'https://localhost:3000/api/v1'
  await page.goto(`${api}/auth/csrf`)

  const getCsrf = () => page.evaluate(async () => {
    const response = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin' })
    if (!response.ok) throw new Error('CSRF_BOOTSTRAP_FAILED')
    return (await response.json() as { requestToken: string }).requestToken
  })
  const firstToken = await getCsrf()
  const key = process.env.KOHAMI_AC02_IDEMPOTENCY_KEY
  if (!key) throw new Error('Foundation_AC02_IDEMPOTENCY_KEY_MISSING')

  let lostResponseStatus = 0
  let committedCartId = ''
  await page.route('**/api/v1/carts', async route => {
    if (route.request().method() !== 'POST') return route.continue()
    const committed = await route.fetch()
    lostResponseStatus = committed.status()
    committedCartId = ((await committed.json()) as { id: string }).id
    await route.fulfill({ response: committed, status: 503, body: '{"code":"SYNTHETIC_RESPONSE_LOST"}' })
  })
  const firstAttempt = await page.evaluate(async ({ key, csrf }) => {
    const response = await fetch('/api/v1/carts', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'content-type': 'application/json', 'x-csrf-token': csrf, 'idempotency-key': key },
      body: '{}',
    })
    return response.status
  }, { key, csrf: firstToken })
  expect(lostResponseStatus).toBe(201)
  expect(firstAttempt).toBe(503)
  await page.unroute('**/api/v1/carts')

  const initialCookies = await context.cookies(api)
  const initialBrowserCookie = initialCookies.find(cookie => cookie.name === 'kohami.browser')?.value
  const initialCommandCookie = initialCookies.find(cookie => cookie.name === 'kohami.command')?.value
  expect(initialBrowserCookie).toBeTruthy()
  expect(initialCommandCookie).toBeTruthy()

  await context.clearCookies({ name: 'kohami.browser' })
  await context.clearCookies({ name: 'kohami.csrf' })
  const rotatedToken = await getCsrf()
  expect(rotatedToken).not.toBe(firstToken)
  const rotatedCookies = await context.cookies(api)
  expect(rotatedCookies.find(cookie => cookie.name === 'kohami.browser')?.value).not.toBe(initialBrowserCookie)
  expect(rotatedCookies.find(cookie => cookie.name === 'kohami.command')?.value).toBe(initialCommandCookie)

  const replay = await page.evaluate(async ({ key, csrf }) => {
    const response = await fetch('/api/v1/carts', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'content-type': 'application/json', 'x-csrf-token': csrf, 'idempotency-key': key },
      body: '{}',
    })
    return { status: response.status, body: await response.json() as { id: string; items: unknown[] } }
  }, { key, csrf: rotatedToken })
  expect(replay.status).toBe(201)
  expect(replay.body.items).toEqual([])
  expect(replay.body.id).toBe(committedCartId)

  const currentCart = await context.request.get(`${api}/carts/current`)
  expect(currentCart.status()).toBe(200)
  expect((await currentCart.json() as { id: string }).id).toBe(replay.body.id)

  const wrongToken = await context.request.post(`${api}/carts`, {
    headers: { origin: 'https://localhost:3000', 'x-csrf-token': 'invalid-synthetic-token', 'idempotency-key': `${key}-wrong-token` },
    data: {},
  })
  expect(wrongToken.status()).toBe(403)
  expect((await wrongToken.json() as { code: string }).code).toBe('CSRF_INVALID')

  const wrongOrigin = await context.request.post(`${api}/carts`, {
    headers: { origin: 'https://attacker.invalid', 'x-csrf-token': rotatedToken, 'idempotency-key': `${key}-wrong-origin` },
    data: {},
  })
  expect(wrongOrigin.status()).toBe(403)
  expect((await wrongOrigin.json() as { code: string }).code).toBe('CSRF_INVALID')

  const evidencePath = process.env.KOHAMI_AC02_EVIDENCE_PATH
  if (!evidencePath) throw new Error('Foundation_AC02_EVIDENCE_PATH_MISSING')
  const { chmodSync, writeFileSync } = await import('node:fs')
  writeFileSync(evidencePath, JSON.stringify({ cartId: replay.body.id }) + '\n', { mode: 0o600 })
  chmodSync(evidencePath, 0o600)
})
