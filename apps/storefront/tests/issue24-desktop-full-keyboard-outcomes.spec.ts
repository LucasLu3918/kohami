import { expect, test, type Locator, type Page } from '@playwright/test'
import fixture from './fixtures/issue24-api-fixture.json' with { type: 'json' }

const locales = [
  {
    path: 'zh-TW',
    skip: '略過導覽',
    quantity: '數量',
    delivery: '配送方式',
    quoteAction: '重新試算費用',
    quote: '費用明細',
    email: '訂單 Email',
    placeOrder: '建立訂單並保留庫存',
    resultLink: '查看唯讀結帳摘要',
    resultHeading: '結帳結果',
    refresh: '重新查詢狀態',
    states: { Created: '尚未啟動', Processing: '處理中', Succeeded: '系統已確認', Failed: '未通過', Unknown: '結果待確認' },
    failedNotice: '付款未通過',
    unknownNotice: '不要重複啟動付款',
    refreshFailed: '畫面保留上次取得的伺服器結果',
  },
  {
    path: 'ja',
    skip: 'メインコンテンツへスキップ',
    quantity: '数量',
    delivery: '受取方法',
    quoteAction: '費用を再計算',
    quote: '費用明細',
    email: '注文 Email',
    placeOrder: '注文を作成して在庫を確保',
    resultLink: 'チェックアウト概要を表示',
    resultHeading: '決済結果',
    refresh: '状態を再確認',
    states: { Created: '未開始', Processing: '処理中', Succeeded: '確認済み', Failed: '未承認', Unknown: '確認待ち' },
    failedNotice: '支払いを確認できませんでした',
    unknownNotice: '支払を再開しないでください',
    refreshFailed: '前回取得したサーバー結果を表示しています',
  },
  {
    path: 'en',
    skip: 'Skip to main content',
    quantity: 'Quantity',
    delivery: 'Delivery method',
    quoteAction: 'Recalculate fees',
    quote: 'Fee summary',
    email: 'Order email',
    placeOrder: 'Place order and reserve stock',
    resultLink: 'View checkout summary',
    resultHeading: 'Checkout result',
    refresh: 'Refresh status',
    states: { Created: 'Not started', Processing: 'Processing', Succeeded: 'Confirmed', Failed: 'Not approved', Unknown: 'Unconfirmed' },
    failedNotice: 'Payment was not approved',
    unknownNotice: 'Do not start payment again',
    refreshFailed: 'Showing the last server result',
  },
] as const

type PaymentState = keyof typeof locales[number]['states']

async function tabTo(page: Page, target: Locator, limit = 100) {
  for (let index = 0; index < limit; index += 1) {
    if (await target.evaluate(element => element === document.activeElement)) return
    await page.keyboard.press('Tab')
  }
  throw new Error('Keyboard navigation did not reach the expected control')
}

for (const locale of locales) {
  test(`${locale.path} desktop keyboard journey reaches checkout outcomes and recovers from a failed refresh`, async ({ page }) => {
    const cart = { id: `cart-issue24-${locale.path}`, version: 'cart-v1', items: [{ skuId: fixture.product.skuId, quantity: 1, unitPriceTwd: fixture.product.priceTwd, availability: 'InStock' }] }
    let paymentState: PaymentState = 'Created'
    let failStatusReads = false
    let checkoutEmail: string | null = null
    const checkoutId = `checkout-issue24-${locale.path}`
    const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
    const product = fixture.product
    const snapshot = { currency: 'TWD', merchandiseTwd: product.priceTwd, discountTwd: 0, shippingTwd: 60, totalTwd: product.priceTwd + 60 }

    await page.route('**/api/v1/**', async route => {
      const request = route.request()
      const url = new URL(request.url())
      if (url.pathname === '/api/v1/auth/session') return route.fulfill({ json: { state: 'MemberAuthenticated', mode: 'mock', userId: `member-issue24-${locale.path}`, email: 'member-issue24@example.test', permissions: [], expiresAt: null } })
      if (url.pathname === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: `synthetic-${locale.path}-csrf` } })
      if (url.pathname === '/api/v1/wallet/current') return route.fulfill({ json: { mode: 'mock', currency: 'TWD', availableTwd: 0, version: 1, entries: [], nextCursor: null } })
      if (url.pathname === '/api/v1/products') return route.fulfill({ json: { items: [product], nextCursor: null } })
      if (url.pathname === '/api/v1/carts/current') return route.fulfill({ json: cart })
      if (url.pathname === '/api/v1/checkout-quotes' && request.method() === 'POST') {
        return route.fulfill({ json: { id: `quote-issue24-${locale.path}`, mode: 'mock', expiresAt, snapshot } })
      }
      if (url.pathname === '/api/v1/checkouts' && request.method() === 'POST') {
        checkoutEmail = (request.postDataJSON() as { contact?: { email?: string } }).contact?.email ?? null
        return route.fulfill({ json: {
          checkoutId,
          orderId: `order-issue24-${locale.path}`,
          mode: 'mock',
          version: 'checkout-v1',
          expiresAt,
          snapshot,
          statusUrl: `/api/v1/checkouts/${checkoutId}/status`,
        } })
      }
      if (url.pathname === `/api/v1/checkouts/${checkoutId}/status`) {
        if (failStatusReads) {
          return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
        }
        const succeeded = paymentState === 'Succeeded'
        return route.fulfill({ json: {
          checkoutId,
          mode: 'mock',
          version: `status-${paymentState}`,
          orderState: succeeded ? 'Confirmed' : 'AwaitingPayment',
          paymentState,
          compensationRequired: false,
          expiresAt,
          snapshot: { ...snapshot, lines: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd }] },
        } })
      }
      return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
    })

    const initialCartResponse = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/v1/carts/current' && response.request().method() === 'GET',
    )
    await page.goto(`/${locale.path}/cart`)
    expect((await initialCartResponse).status()).toBe(200)
    await page.keyboard.press('Tab')
    const skipLink = page.getByRole('link', { name: locale.skip })
    await expect(skipLink).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()

    const quantity = page.getByRole('spinbutton', { name: locale.quantity })
    await tabTo(page, quantity)
    const delivery = page.getByRole('combobox', { name: locale.delivery })
    await tabTo(page, delivery)
    await expect(delivery).toBeFocused()
    await page.keyboard.press('Tab')
    const quoteButton = page.getByRole('button', { name: locale.quoteAction })
    await expect(quoteButton).toBeFocused()
    const quoteResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes')
    await page.keyboard.press('Enter')
    expect((await quoteResponse).status()).toBe(200)

    const quote = page.getByRole('region', { name: locale.quote })
    await expect(quote).toContainText(`${locale.email}：member-issue24@example.test`)
    const confirmation = quote.getByRole('checkbox')
    await tabTo(page, confirmation)
    await expect(confirmation).toBeFocused()
    await page.keyboard.press('Space')
    const placeOrder = quote.getByRole('button', { name: locale.placeOrder })
    await page.keyboard.press('Tab')
    await expect(placeOrder).toBeFocused()
    const orderResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkouts')
    await page.keyboard.press('Enter')
    expect((await orderResponse).status()).toBe(200)
    expect(checkoutEmail).toBe('member-issue24@example.test')

    await expect(page).toHaveURL(new RegExp(`/checkout/${checkoutId}$`))
    const result = page.getByRole('region', { name: locale.resultHeading })
    await expect(result.getByText(locale.states.Created)).toBeVisible()
    await expect(result.getByText(`TWD ${snapshot.totalTwd}`)).toBeVisible()

    const refresh = result.getByRole('button', { name: locale.refresh })
    await tabTo(page, refresh)
    const advanceWithKeyboard = async (state: PaymentState) => {
      paymentState = state
      const response = page.waitForResponse(statusResponse =>
        new URL(statusResponse.url()).pathname === `/api/v1/checkouts/${checkoutId}/status` && statusResponse.request().method() === 'GET',
      )
      await page.keyboard.press('Enter')
      expect((await response).status()).toBe(200)
      await expect(result.getByText(locale.states[state], { exact: true })).toBeVisible()
      await expect(refresh).toBeFocused()
    }

    await advanceWithKeyboard('Processing')
    const terminalState: PaymentState = locale.path === 'zh-TW' ? 'Succeeded' : locale.path === 'ja' ? 'Failed' : 'Unknown'
    await advanceWithKeyboard(terminalState)
    if (terminalState === 'Failed') await expect(result.getByRole('status')).toContainText(locale.failedNotice)
    if (terminalState === 'Unknown') await expect(result.getByRole('status')).toContainText(locale.unknownNotice)

    failStatusReads = true
    const failedRefresh = page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/v1/checkouts/${checkoutId}/status` && response.status() === 503,
    )
    await page.keyboard.press('Enter')
    expect((await failedRefresh).status()).toBe(503)
    await expect(result.getByRole('alert')).toContainText(locale.refreshFailed)
    await expect(result.getByText(locale.states[terminalState], { exact: true })).toBeVisible()
    await expect(result.getByText(`TWD ${snapshot.totalTwd}`)).toBeVisible()
    await expect(refresh).toBeFocused()

    failStatusReads = false
    await advanceWithKeyboard(terminalState)
  })
}
