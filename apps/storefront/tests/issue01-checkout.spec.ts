import { expect, test } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { resolve } from 'node:path'

test('anonymous shopper can create an idempotent cart, place a mock order and recover unknown payment status', async ({ page, context }) => {
  test.setTimeout(480_000)
  const cartCreateResponse = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/carts',
  )

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'KOHAMI 本機展示' })).toBeVisible()
  await expect(page.getByText('購物車目前是空的。')).toBeVisible()

  const createdCartResponse = await cartCreateResponse
  expect(createdCartResponse.status()).toBe(201)
  const createdCart = await createdCartResponse.json() as { id: string; items: unknown[] }
  expect(createdCart.items).toHaveLength(0)

  const createRequest = createdCartResponse.request()
  const replayResponse = await context.request.fetch(createdCartResponse.url(), {
    method: 'POST',
    headers: await createRequest.allHeaders(),
    data: createRequest.postData() || '{}',
  })
  expect(replayResponse.status()).toBe(201)
  expect((await replayResponse.json()).id).toBe(createdCart.id)

  const addDemoItem = page.getByRole('button', { name: '加入購物車' }).nth(1)
  await expect(addDemoItem).toBeEnabled()
  await addDemoItem.click()
  const cartRegion = page.getByRole('region', { name: '購物車' })
  await expect(cartRegion.getByText('合成示意商品 B')).toBeVisible()

  const apiBase = 'https://localhost:3000/api/v1'
  const cartResponse = await context.request.get(`${apiBase}/carts/current`)
  expect(cartResponse.status()).toBe(200)
  const currentCart = await cartResponse.json() as { version: string; items: Array<{ skuId: string; quantity: number }> }
  const csrfResponse = await context.request.get(`${apiBase}/auth/csrf`)
  const csrf = (await csrfResponse.json() as { requestToken: string }).requestToken
  const originHeaders = { origin: 'https://localhost:3000', 'x-csrf-token': csrf, 'if-match': `\"${currentCart.version}\"` }
  const unsupported = await context.request.post(`${apiBase}/checkout-quotes`, {
    headers: { ...originHeaders, 'idempotency-key': 'issue01-unsupported-region' },
    data: { cartVersion: currentCart.version, deliveryMethod: 'mock_home', region: 'demo_unsupported' },
  })
  expect(unsupported.status()).toBe(409)
  expect((await unsupported.json()).code).toBe('FEE_UNAVAILABLE')

  const clientAmount = await context.request.post(`${apiBase}/checkout-quotes`, {
    headers: { ...originHeaders, 'idempotency-key': 'issue01-client-amount' },
    data: { cartVersion: currentCart.version, deliveryMethod: 'mock_home', region: 'demo_supported', totalTwd: 1 },
  })
  expect(clientAmount.status()).toBe(422)

  const decimalQuantity = await context.request.put(`${apiBase}/carts/current/items/sku_demo_02`, {
    headers: { ...originHeaders, 'idempotency-key': 'issue01-decimal-quantity' },
    data: { quantity: 1.5 },
  })
  expect(decimalQuantity.status()).toBe(422)
  const overflowQuantity = await context.request.put(`${apiBase}/carts/current/items/sku_demo_02`, {
    headers: { ...originHeaders, 'idempotency-key': 'issue01-overflow-quantity' },
    data: { quantity: 2147483648 },
  })
  expect(overflowQuantity.status()).toBe(422)
  const unchangedCart = await (await context.request.get(`${apiBase}/carts/current`)).json() as typeof currentCart
  expect(unchangedCart.items).toHaveLength(1)
  expect(unchangedCart.items[0]).toMatchObject({ skuId: 'sku_demo_02', quantity: 1 })

  const windowId = process.env.KOHAMI_ISSUE01_WINDOW_ID
  if (!windowId) throw new Error('ISSUE01_FIXTURE_WINDOW_ID_MISSING')
  const root = resolve(process.cwd(), '../..')
  const fixtureEnv = { ...process.env, KOHAMI_M0_TEST_CONFIG: resolve(root, '.local/m0/fixture-private.json') }
  const setFixturePrice = (price: number) => execFileSync('/usr/local/share/dotnet/dotnet', [
    resolve(root, 'tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll'),
    'fixture-window', 'price', root, windowId, 'sku_demo_02', String(price),
  ], { cwd: root, env: fixtureEnv, stdio: 'pipe' })
  try {
    setFixturePrice(901)
    await page.getByRole('button', { name: '重新試算' }).click()
    await expect(page.getByText('商品或報價已變更，請重新試算。')).toBeVisible()
  } finally {
    setFixturePrice(900)
  }

  await page.getByLabel('模擬交付方式').selectOption('mock_pickup')
  const pickupQuote = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes',
  )
  await page.getByRole('button', { name: '重新試算' }).click()
  const pickupResponse = await pickupQuote
  expect(pickupResponse.status()).toBe(201)
  expect((await pickupResponse.json()).snapshot.shippingTwd).toBe(0)
  await expect(page.locator('dd.grand-total')).toHaveText('TWD 900')

  await page.getByLabel('模擬交付方式').selectOption('mock_home')
  const staleQuotePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes',
  )
  await page.getByRole('button', { name: '重新試算' }).click()
  const staleQuote = await staleQuotePromise
  expect(staleQuote.status()).toBe(201)
  const quoteRegion = page.getByRole('region', { name: '結帳報價' })
  await page.waitForTimeout(5 * 60_000 + 1000)

  await quoteRegion.getByLabel('測試 Email').fill('buyer@example.test')
  await quoteRegion.getByLabel(/我確認以此報價建立模擬訂單/).check()
  const expiredCheckout = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkouts',
  )
  await page.getByRole('button', { name: '建立模擬訂單並保留庫存' }).click()
  expect((await expiredCheckout).status()).toBe(409)
  await expect(page.getByText('報價已過期，請重新試算。')).toBeVisible()

  await page.getByRole('button', { name: '重新試算' }).click()
  await expect(page.getByRole('heading', { name: '結帳報價' })).toBeVisible()
  await expect(page.locator('dd.grand-total')).toHaveText('TWD 960')

  await quoteRegion.getByLabel('測試 Email').fill('buyer@example.test')
  await quoteRegion.getByLabel(/我確認以此報價建立模擬訂單/).check()
  const checkoutResponse = page.waitForResponse(response =>
    response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkouts',
  )
  await page.getByRole('button', { name: '建立模擬訂單並保留庫存' }).click()
  const placedOrderResponse = await checkoutResponse
  expect(placedOrderResponse.status()).toBe(201)
  const checkout = await placedOrderResponse.json() as { checkoutId: string; orderId: string; mode: string; snapshot: { totalTwd: number } }
  expect(checkout.mode).toBe('mock')
  expect(checkout.snapshot.totalTwd).toBe(960)
  const checkoutRequest = placedOrderResponse.request()
  const checkoutReplay = await context.request.fetch(placedOrderResponse.url(), {
    method: 'POST', headers: await checkoutRequest.allHeaders(), data: checkoutRequest.postData() || '{}',
  })
  expect(checkoutReplay.status()).toBe(201)
  expect((await checkoutReplay.json()).checkoutId).toBe(checkout.checkoutId)
  await expect(page.getByText('等待付款')).toBeVisible()

  await page.getByRole('button', { name: '啟動模擬付款' }).click()
  const refreshStatus = page.getByRole('button', { name: '重新查詢狀態' })
  await expect.poll(async () => {
    await refreshStatus.click()
    return await page.getByText(/付款狀態:/).textContent()
  }, { timeout: 20_000 }).toContain('結果待確認')
  await expect(page.getByText('付款結果未知時請先查詢狀態，不可重複啟動付款。')).toBeVisible()
  await expect(page.getByRole('button', { name: '啟動模擬付款' })).toHaveCount(0)

  await page.reload()
  await page.getByLabel('模擬結帳 ID').fill(checkout.checkoutId)
  await page.getByRole('button', { name: '查詢模擬付款狀態' }).click()
  await expect(page.getByText('結果待確認')).toBeVisible()
  await expect(page.getByRole('button', { name: '啟動模擬付款' })).toHaveCount(0)

  const evidencePath = process.env.KOHAMI_ISSUE01_EVIDENCE_PATH
  if (evidencePath) {
    const { writeFileSync } = await import('node:fs')
    writeFileSync(evidencePath, JSON.stringify({ cartId: createdCart.id, checkoutId: checkout.checkoutId, orderId: checkout.orderId, totalTwd: checkout.snapshot.totalTwd, outcomes: ['unsupported-region=FEE_UNAVAILABLE', 'client-amount=VALIDATION_ERROR', 'decimal-quantity=VALIDATION_ERROR', 'overflow-quantity=VALIDATION_ERROR', 'changed-price=QUOTE_CHANGED', 'pickup-shipping=0', 'expired-quote=QUOTE_EXPIRED', 'checkout-idempotency=replayed-same-id', 'payment=Unknown-persisted-after-reload'] }, null, 2) + '\n', { mode: 0o600 })
  }
})
