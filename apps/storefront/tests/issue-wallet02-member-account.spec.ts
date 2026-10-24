import { expect, test } from '@playwright/test'

const firstPage = {
  mode: 'mock',
  currency: 'TWD',
  availableTwd: 12340,
  version: 2,
  entries: [
    { id: 'ledger-credit-2', type: 'SyntheticCredit', amountTwd: 5000, currency: 'TWD', sourceId: 'synthetic-test-credit:2', createdAt: '2026-10-20T02:00:00Z' },
    { id: 'ledger-debit-1', type: 'CheckoutDebit', amountTwd: 1260, currency: 'TWD', sourceId: 'checkout-synthetic-1', createdAt: '2026-10-19T02:00:00Z' },
  ],
  nextCursor: 'opaque-cursor-page-2',
}

async function stubMemberAccount(page: import('@playwright/test').Page) {
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'MemberAuthenticated', mode: 'mock', userId: 'member-a', permissions: [], expiresAt: null } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [] } }))
}

test('member account links to the wallet page, which keeps the ledger collapsed and paginates entries', async ({ page }) => {
  const walletRequests: URL[] = []
  await stubMemberAccount(page)
  await page.route('**/api/v1/wallet/current**', async route => {
    const url = new URL(route.request().url())
    walletRequests.push(url)
    expect(route.request().method()).toBe('GET')
    expect(url.searchParams.get('limit')).toBe('20')

    if (url.searchParams.has('cursor')) {
      expect(url.searchParams.get('cursor')).toBe('opaque-cursor-page-2')
      await route.fulfill({ json: { ...firstPage, entries: [{ id: 'ledger-credit-3', type: 'SyntheticCredit', amountTwd: 1000, currency: 'TWD', sourceId: 'synthetic-test-credit:3', createdAt: '2026-10-18T02:00:00Z' }], nextCursor: null } })
      return
    }

    await route.fulfill({ json: firstPage })
  })

  await page.goto('/zh-TW/account')

  await expect(page.getByRole('heading', { name: '會員儲值餘額' })).toBeVisible()
  await expect(page.getByText('TWD 12,340')).toBeVisible()
  await expect(page.getByRole('link', { name: '前往儲值' })).toBeVisible()
  await expect(page.getByText('餘額帳本')).toHaveCount(0)

  await page.getByRole('link', { name: '前往儲值' }).click()
  await expect(page).toHaveURL('/zh-TW/wallet')
  await expect(page.getByText(/餘額異動記錄於系統/)).toBeVisible()
  const ledger = page.locator('details.wallet-ledger')
  await expect(ledger).not.toHaveAttribute('open', '')
  await expect(page.getByText('系統入帳')).toHaveCount(0)
  await ledger.locator('summary').click()
  await expect(page.getByText('系統入帳')).toBeVisible()
  await expect(page.getByText('結帳扣抵')).toBeVisible()
  await expect(page.getByText(/synthetic-test-credit|checkout-synthetic-1/)).toHaveCount(0)

  await page.getByRole('button', { name: '載入更多' }).click()
  await expect(page.getByText('ledger-credit-3')).toHaveCount(0)
  await expect(page.getByText('TWD 1,000')).toBeVisible()
  expect(walletRequests).toHaveLength(3)
  expect(walletRequests[0].searchParams.has('cursor')).toBe(false)
  expect(walletRequests[1].searchParams.has('cursor')).toBe(false)
  expect(walletRequests[2].searchParams.get('cursor')).toBe('opaque-cursor-page-2')
})

test('anonymous visitors do not request the wallet and logout clears private wallet data', async ({ page }) => {
  let sessionState = 'MemberAuthenticated'
  let walletReads = 0
  await page.route('**/api/v1/auth/session', async route => {
    if (route.request().method() === 'DELETE') {
      sessionState = 'Anonymous'
      await route.fulfill({ status: 204, body: '' })
      return
    }
    await route.fulfill({ json: { state: sessionState, mode: 'mock', userId: 'member-a', permissions: [], expiresAt: null } })
  })
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-demo' } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [] } }))
  await page.route('**/api/v1/wallet/current**', route => {
    walletReads += 1
    return route.fulfill({ json: { ...firstPage, nextCursor: null } })
  })

  await page.goto('/zh-TW/account')
  await expect(page.getByText('TWD 12,340')).toBeVisible()
  await page.getByRole('button', { name: '登出' }).click()
  await expect(page.getByText('請先使用 KOHAMI 帳號登入。')).toBeVisible()
  await expect(page.getByText('TWD 12,340')).toHaveCount(0)
  expect(walletReads).toBe(1)

  sessionState = 'Anonymous'
  await page.goto('/en/account')
  await expect(page.getByText('Please sign in with your KOHAMI account.')).toBeVisible()
  await expect(page.getByText(/Wallet balance/)).toHaveCount(0)
  expect(walletReads).toBe(1)
})

test('wallet authentication failure clears the private balance and shows a sign-in message', async ({ page }) => {
  let walletReads = 0
  await stubMemberAccount(page)
  await page.route('**/api/v1/wallet/current**', route => {
    walletReads += 1
    return walletReads === 1
      ? route.fulfill({ json: firstPage })
      : route.fulfill({ status: 401, json: { code: 'AUTHENTICATION_REQUIRED' } })
  })

  await page.goto('/zh-TW/account')
  await expect(page.getByText('TWD 12,340')).toBeVisible()
  await page.getByRole('link', { name: '前往儲值' }).click()

  await expect(page.getByText('請重新登入以查看錢包。')).toBeVisible()
  await expect(page.getByText('TWD 12,340')).toHaveCount(0)
  await expect(page.getByText('合成入帳')).toHaveCount(0)
  await expect(page.getByRole('alert')).toBeVisible()
})

test('wallet empty and service error states are visible without private data', async ({ page }) => {
  let shouldFail = false
  await stubMemberAccount(page)
  await page.route('**/api/v1/wallet/current**', route => shouldFail
    ? route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
    : route.fulfill({ json: { ...firstPage, entries: [], nextCursor: null } }))

  await page.goto('/zh-TW/wallet')
  await page.locator('details.wallet-ledger summary').click()
  await expect(page.getByText('目前沒有帳本紀錄。')).toBeVisible()
  await expect(page.getByText('餘額異動記錄於系統')).toBeVisible()

  shouldFail = true
  await page.reload()
  await expect(page.getByText('錢包服務暫時無法使用。')).toBeVisible()
  await expect(page.getByText('TWD 12,340')).toHaveCount(0)
})

test('wallet mock labels and balance stay localized without changing the amount', async ({ page }) => {
  await stubMemberAccount(page)
  await page.route('**/api/v1/wallet/current**', route => route.fulfill({ json: { ...firstPage, nextCursor: null } }))

  for (const example of [
    { path: '/zh-TW/account', heading: '會員儲值餘額', action: '前往儲值', walletPath: '/zh-TW/wallet', label: '餘額異動記錄於系統' },
    { path: '/ja/account', heading: 'ウォレット残高', action: '残高をチャージ', walletPath: '/ja/wallet', label: '残高はシステムに記録されます' },
    { path: '/en/account', heading: 'Wallet balance', action: 'Top up balance', walletPath: '/en/wallet', label: 'Balance is recorded in the system' },
  ]) {
    await page.goto(example.path)
    await expect(page.getByRole('heading', { name: example.heading })).toBeVisible()
    await expect(page.getByText(/TWD 12,340/)).toBeVisible()
    await page.getByRole('link', { name: example.action }).click()
    await expect(page).toHaveURL(example.walletPath)
    await expect(page.getByText(new RegExp(example.label))).toBeVisible()
    await expect(page.getByText(/TWD 12,340/)).toBeVisible()
  }
})
