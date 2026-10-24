import { expect, test } from '@playwright/test'

test('member can add synthetic balance with the provided test card', async ({ page }) => {
  let balance = 240
  let topUpRequest: Record<string, unknown> | undefined
  let readCount = 0

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: {
    state: 'MemberAuthenticated', mode: 'mock', userId: 'member-wallet02',
    email: 'member-wallet02@example.test', permissions: [], expiresAt: null,
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-wallet02' } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/wallet/current**', route => {
    readCount += 1
    return route.fulfill({ json: {
      mode: 'mock', currency: 'TWD', availableTwd: balance, version: readCount,
      entries: balance === 1240 ? [{ id: 'entry-wallet02', type: 'MemberTopUp', amountTwd: 1000, currency: 'TWD', sourceId: 'source-wallet02', createdAt: '2026-10-22T00:00:00Z' }] : [],
      nextCursor: null,
    } })
  })
  await page.route('**/api/v1/wallet/top-ups', async route => {
    topUpRequest = route.request().postDataJSON() as Record<string, unknown>
    expect(route.request().headers()['x-csrf-token']).toBe('csrf-wallet02')
    expect(route.request().headers()['idempotency-key']).toBeTruthy()
    balance = 1240
    await route.fulfill({ status: 201, json: { entryId: 'entry-wallet02', mode: 'mock', amountTwd: 1000, availableTwd: balance } })
  })

  await page.goto('/zh-TW/wallet')
  await expect(page.getByText('TWD 240', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: '帶入卡號' }).click()
  await expect(page.getByLabel('卡號')).toHaveValue('4111111111111111')
  await page.getByRole('button', { name: '新增餘額' }).click()

  await expect.poll(() => topUpRequest).toEqual({ amountTwd: 1000, cardNumber: '4111111111111111' })
  await expect(page.getByText('TWD 1,240', { exact: true })).toBeVisible()
  await page.locator('details.wallet-ledger summary').click()
  await expect(page.getByText('卡片加值')).toBeVisible()
  expect(readCount).toBe(2)
})

test('invalid synthetic card is rejected without changing the wallet balance', async ({ page }) => {
  let balance = 240
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: {
    state: 'MemberAuthenticated', mode: 'mock', userId: 'member-wallet02',
    email: 'member-wallet02@example.test', permissions: [], expiresAt: null,
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-wallet02' } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/wallet/current**', route => route.fulfill({ json: {
    mode: 'mock', currency: 'TWD', availableTwd: balance, version: 1, entries: [], nextCursor: null,
  } }))
  await page.route('**/api/v1/wallet/top-ups', async route => {
    balance = 240
    await route.fulfill({ status: 422, json: { code: 'CARD_INVALID' } })
  })

  await page.goto('/zh-TW/wallet')
  await page.getByLabel('卡號').fill('4111111111111112')
  await page.getByRole('button', { name: '新增餘額' }).click()

  await expect(page.getByRole('alert')).toHaveText('卡號不正確。')
  await expect(page.getByText('TWD 240', { exact: true })).toBeVisible()
  expect(balance).toBe(240)
})
