import { expect, test, type Locator, type Page } from '@playwright/test'
import fixture from './fixtures/issue24-api-fixture.json' with { type: 'json' }

const locales = [
  {
    path: 'zh-TW',
    skip: '略過導覽',
    search: '搜尋名稱、SKU 或商品描述',
    searchAction: '搜尋商品',
    color: '顏色',
    size: '顏色與衣服尺寸',
    stock: '庫存',
    product: '共用驗收示意商品',
    sku: '顏色與衣服尺寸: 共用驗收示意商品',
    add: '加入購物車',
    quantity: '數量',
    delivery: '模擬交付方式',
    quoteAction: '重新試算',
    quote: '結帳報價',
    email: '測試 Email',
    placeOrder: '建立模擬訂單並保留庫存',
    resultLink: '查看唯讀結帳摘要',
    resultHeading: '模擬結帳結果',
    refresh: '重新查詢狀態',
    states: { Created: '尚未啟動', Processing: '處理中', Succeeded: '模擬成功', Failed: '模擬未通過', Unknown: '結果待確認' },
    failedNotice: '模擬付款未通過',
    unknownNotice: '不要重複啟動付款',
    refreshFailed: '畫面保留上次取得的伺服器結果',
  },
  {
    path: 'ja',
    skip: 'メインコンテンツへスキップ',
    search: '商品名、SKU、説明を検索',
    searchAction: '商品を検索',
    color: '色',
    size: '色と衣服サイズ',
    stock: '在庫',
    product: '共通確認デモ商品',
    sku: '色と衣服サイズ: 共通確認デモ商品',
    add: 'カートに追加',
    quantity: '数量',
    delivery: '模擬受取方法',
    quoteAction: '再見積もり',
    quote: 'チェックアウト見積もり',
    email: 'テストメール',
    placeOrder: '模擬注文を作成して在庫を確保',
    resultLink: 'チェックアウト概要を表示',
    resultHeading: '模擬チェックアウト結果',
    refresh: '状態を再確認',
    states: { Created: '未開始', Processing: '処理中', Succeeded: '模擬成功', Failed: '模擬拒否', Unknown: '確認待ち' },
    failedNotice: '模擬支払は拒否されました',
    unknownNotice: '支払を再開しないでください',
    refreshFailed: '前回取得したサーバー結果を表示しています',
  },
  {
    path: 'en',
    skip: 'Skip to main content',
    search: 'Search product name, SKU or description',
    searchAction: 'Search products',
    color: 'Color',
    size: 'Color and garment size',
    stock: 'Stock',
    product: 'Shared acceptance demo item',
    sku: 'Color and garment size: Shared acceptance demo item',
    add: 'Add to cart',
    quantity: 'Quantity',
    delivery: 'Mock delivery method',
    quoteAction: 'Refresh quote',
    quote: 'Checkout quote',
    email: 'Test email',
    placeOrder: 'Create mock order and reserve stock',
    resultLink: 'View checkout summary',
    resultHeading: 'Mock checkout result',
    refresh: 'Refresh status',
    states: { Created: 'Not started', Processing: 'Processing', Succeeded: 'Mock success', Failed: 'Mock declined', Unknown: 'Unconfirmed' },
    failedNotice: 'Mock payment was declined',
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
    let cart = { id: `cart-issue24-${locale.path}`, version: 'cart-v1', items: [] as Array<{ skuId: string; quantity: number; unitPriceTwd: number }> }
    let paymentState: PaymentState = 'Created'
    let failStatusReads = false
    const checkoutId = `checkout-issue24-${locale.path}`
    const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
    const product = fixture.product
    const snapshot = { currency: 'TWD', merchandiseTwd: product.priceTwd, discountTwd: 0, shippingTwd: 60, totalTwd: product.priceTwd + 60 }

    await page.route('**/api/v1/**', async route => {
      const request = route.request()
      const url = new URL(request.url())
      if (url.pathname === '/api/v1/auth/session') return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
      if (url.pathname === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: `synthetic-${locale.path}-csrf` } })
      if (url.pathname === '/api/v1/products') return route.fulfill({ json: { items: [product], nextCursor: null } })
      if (url.pathname === '/api/v1/carts/current') return route.fulfill({ json: cart })
      if (url.pathname === `/api/v1/carts/current/items/${product.skuId}` && request.method() === 'PUT') {
        cart = { ...cart, version: 'cart-v2', items: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd }] }
        return route.fulfill({ json: cart })
      }
      if (url.pathname === '/api/v1/checkout-quotes' && request.method() === 'POST') {
        return route.fulfill({ json: { id: `quote-issue24-${locale.path}`, mode: 'mock', expiresAt, snapshot } })
      }
      if (url.pathname === '/api/v1/checkouts' && request.method() === 'POST') {
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
    await page.goto(`/${locale.path}`)
    expect((await initialCartResponse).status()).toBe(200)
    await page.keyboard.press('Tab')
    const skipLink = page.getByRole('link', { name: locale.skip })
    await expect(skipLink).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()

    const search = page.getByRole('searchbox', { name: locale.search })
    await tabTo(page, search)
    await page.keyboard.type(product.skuId)
    for (const label of [locale.color, locale.size, locale.stock]) {
      await page.keyboard.press('Tab')
      await expect(page.getByRole('combobox', { name: label, exact: true })).toBeFocused()
    }
    await page.keyboard.press('Tab')
    const searchButton = page.getByRole('search').getByRole('button', { name: locale.searchAction })
    await expect(searchButton).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('heading', { name: locale.product })).toBeVisible()

    const skuSelector = page.getByRole('combobox', { name: locale.sku })
    await tabTo(page, skuSelector)
    await page.keyboard.press('o')
    await expect(skuSelector).toHaveValue(product.skuId)
    await page.keyboard.press('Tab')
    const addButton = page.getByRole('button', { name: locale.add }).first()
    await expect(addButton).toBeFocused()
    const addItem = page.waitForResponse(response => response.request().method() === 'PUT' && new URL(response.url()).pathname.endsWith(`/items/${product.skuId}`))
    await page.keyboard.press('Enter')
    expect((await addItem).status()).toBe(200)

    const quantity = page.getByRole('spinbutton', { name: locale.quantity })
    await tabTo(page, quantity)
    await page.keyboard.press('Tab')
    await expect(page.getByRole('combobox', { name: locale.delivery })).toBeFocused()
    await page.keyboard.press('Tab')
    const quoteButton = page.getByRole('button', { name: locale.quoteAction })
    await expect(quoteButton).toBeFocused()
    const quoteResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes')
    await page.keyboard.press('Enter')
    expect((await quoteResponse).status()).toBe(200)

    const quote = page.getByRole('region', { name: locale.quote })
    const email = quote.getByRole('textbox', { name: locale.email })
    await tabTo(page, email)
    await page.keyboard.type('buyer@example.test')
    await page.keyboard.press('Tab')
    const confirmation = quote.getByRole('checkbox')
    await expect(confirmation).toBeFocused()
    await page.keyboard.press('Space')
    const placeOrder = quote.getByRole('button', { name: locale.placeOrder })
    await page.keyboard.press('Tab')
    await expect(placeOrder).toBeFocused()
    const orderResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkouts')
    await page.keyboard.press('Enter')
    expect((await orderResponse).status()).toBe(200)

    const resultLink = page.getByRole('link', { name: locale.resultLink })
    await tabTo(page, resultLink)
    await page.keyboard.press('Enter')
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
