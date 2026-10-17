import { expect, test } from '@playwright/test'

test('Issue16 member account lists only server-owned orders and clears private data on logout', async ({ page }) => {
  let session = 'MemberAuthenticated'
  await page.route('**/api/v1/auth/session', async route => {
    if (route.request().method() === 'DELETE') {
      session = 'Anonymous'
      await route.fulfill({ status: 204, body: '' })
      return
    }
    await route.fulfill({ json: { state: session, mode: 'mock', userId: 'member-a', permissions: [], expiresAt: null } })
  })
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [{
    id: 'order-member-a', mode: 'mock', version: 'order-v1', placedAt: '2026-10-17T00:00:00Z', orderState: 'Confirmed',
    snapshot: { currency: 'TWD', totalTwd: 560, lines: [{ skuId: 'sku-demo', quantity: 1 }] },
  }] } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-demo' } }))

  await page.goto('/account?lang=zh-TW')
  await expect(page.getByRole('heading', { name: '我的訂單' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'order-member-a' })).toBeVisible()
  await page.getByRole('button', { name: '登出' }).click()
  await expect(page.getByText('請先使用 KOHAMI 帳號登入。')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'order-member-a' })).toHaveCount(0)
})

test('Issue16 member can explicitly claim a guest order with the current browser proof', async ({ page }) => {
  let claims = 0
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'MemberAuthenticated', mode: 'mock', userId: 'member-a', permissions: [], expiresAt: null } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-demo' } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: claims ? [{
    id: 'order-guest-a', mode: 'mock', version: 'order-v2', placedAt: '2026-10-17T00:00:00Z', orderState: 'Confirmed',
    snapshot: { currency: 'TWD', totalTwd: 560, lines: [{ skuId: 'sku-demo', quantity: 1 }] },
  }] : [] } }))
  await page.route('**/api/v1/orders/order-guest-a/member-claims', async route => {
    expect(route.request().headers()['x-csrf-token']).toBe('csrf-demo')
    expect(route.request().headers()['idempotency-key']).toBeTruthy()
    expect(route.request().postDataJSON()).toEqual({})
    claims += 1
    await route.fulfill({ status: 200, json: { orderId: 'order-guest-a', state: 'Claimed', version: 'order-v2' } })
  })

  await page.goto('/account?lang=zh-TW')
  await page.getByLabel('訪客訂單編號').fill('order-guest-a')
  await page.getByRole('button', { name: '歸戶訂單' }).click()
  await expect(page.getByRole('heading', { name: 'order-guest-a' })).toBeVisible()
  expect(claims).toBe(1)
})

test('Issue16 anonymous session cannot request or retain the private order list', async ({ page }) => {
  let orderReads = 0
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'Anonymous', mode: 'mock', userId: null, permissions: [], expiresAt: null } }))
  await page.route('**/api/v1/orders', route => {
    orderReads += 1
    return route.fulfill({ status: 401, json: { code: 'AUTHENTICATION_REQUIRED' } })
  })

  await page.goto('/account?lang=zh-TW')
  await expect(page.getByText('請先使用 KOHAMI 帳號登入。')).toBeVisible()
  expect(orderReads).toBe(0)
  await expect(page.getByRole('heading', { name: 'order-member-a' })).toHaveCount(0)
})

test('Issue16 order service errors show a recoverable message without stale orders', async ({ page }) => {
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'MemberAuthenticated', mode: 'mock', userId: 'member-a', permissions: [], expiresAt: null } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } }))

  await page.goto('/account?lang=zh-TW')
  await expect(page.getByText('訂單服務暫時無法使用。')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'order-member-a' })).toHaveCount(0)
})
