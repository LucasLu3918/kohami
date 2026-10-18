import { expect, test } from '@playwright/test'

test('desktop keyboard journey searches, selects a product, reviews a quote, and creates a mock checkout', async ({ page }) => {
  let cart = { id: 'cart-issue24-keyboard', version: 'cart-v1', items: [] as Array<{ skuId: string; quantity: number; unitPriceTwd: number }> }
  const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
  const product = {
    productId: 'product-issue24-keyboard',
    slug: 'keyboard-demo-shirt',
    translations: [{ locale: 'zh-TW', name: '鍵盤驗收示意襯衫' }, { locale: 'ja', name: 'キーボード確認用シャツ' }, { locale: 'en', name: 'Keyboard demo shirt' }],
    media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖' } } }],
    skuId: 'sku-issue24-keyboard-m',
    colorId: 'oatmeal',
    garmentSize: 'M',
    priceTwd: 1200,
    availability: 'InStock',
    mode: 'mock' as const,
  }
  const snapshot = { currency: 'TWD', merchandiseTwd: 1200, discountTwd: 0, shippingTwd: 60, totalTwd: 1260 }

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname === '/api/v1/auth/session') return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (url.pathname === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: 'synthetic-csrf-token' } })
    if (url.pathname === '/api/v1/products') return route.fulfill({ json: { items: [product], nextCursor: null } })
    if (url.pathname === '/api/v1/carts/current') return route.fulfill({ json: cart })
    if (url.pathname === `/api/v1/carts/current/items/${product.skuId}` && request.method() === 'PUT') {
      cart = { ...cart, version: 'cart-v2', items: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd }] }
      return route.fulfill({ json: cart })
    }
    if (url.pathname === '/api/v1/checkout-quotes' && request.method() === 'POST') {
      return route.fulfill({ json: { id: 'quote-issue24-keyboard', mode: 'mock', expiresAt, snapshot } })
    }
    if (url.pathname === '/api/v1/checkouts' && request.method() === 'POST') {
      return route.fulfill({ json: { checkoutId: 'checkout-issue24-keyboard', orderId: 'order-issue24-keyboard', mode: 'mock', version: 'checkout-v1', expiresAt, snapshot, statusUrl: '/api/v1/checkouts/checkout-issue24-keyboard/status' } })
    }
    if (url.pathname === '/api/v1/checkouts/checkout-issue24-keyboard/status') {
      return route.fulfill({ json: {
        checkoutId: 'checkout-issue24-keyboard', mode: 'mock', version: 'checkout-status-v1',
        orderState: 'AwaitingPayment', paymentState: 'Created', compensationRequired: false,
        expiresAt, snapshot: { ...snapshot, lines: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd }] },
      } })
    }
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  await page.goto('/zh-TW')

  // Enter the main content through the skip link, then reach search using Tab only.
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: '略過導覽' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()

  const search = page.getByRole('searchbox', { name: '搜尋名稱、SKU 或商品描述' })
  let reachedSearch = false
  for (let index = 0; index < 30; index += 1) {
    await page.keyboard.press('Tab')
    if (await search.evaluate(element => element === document.activeElement)) {
      reachedSearch = true
      break
    }
  }
  expect(reachedSearch).toBe(true)
  await page.keyboard.type('鍵盤驗收')
  for (const label of ['顏色', '顏色與衣服尺寸', '庫存']) {
    await page.keyboard.press('Tab')
    await expect(page.getByRole('combobox', { name: label, exact: true })).toBeFocused()
  }
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: '搜尋商品' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.getByRole('heading', { name: '鍵盤驗收示意襯衫' })).toBeVisible()

  // Tab through the card to its SKU selector, choose the available size, and add it.
  const skuSelector = page.getByRole('combobox', { name: '顏色與衣服尺寸: 鍵盤驗收示意襯衫' })
  let reachedSku = false
  for (let index = 0; index < 40; index += 1) {
    await page.keyboard.press('Tab')
    if (await skuSelector.evaluate(element => element === document.activeElement)) {
      reachedSku = true
      break
    }
  }
  expect(reachedSku).toBe(true)
  await page.keyboard.press('o')
  await page.keyboard.press('Tab')
  await expect(skuSelector).toHaveValue(product.skuId)
  const addProduct = page.getByRole('button', { name: '加入購物車' }).first()
  await expect(addProduct).toBeEnabled()
  await expect(addProduct).toBeFocused()
  const addItem = page.waitForResponse(response => response.request().method() === 'PUT' && new URL(response.url()).pathname === `/api/v1/carts/current/items/${product.skuId}`)
  await page.keyboard.press('Enter')
  expect((await addItem).status()).toBe(200)

  // Continue by keyboard through cart quantity/delivery controls to quote and checkout.
  const quantity = page.getByRole('spinbutton', { name: '數量' })
  let reachedQuantity = false
  for (let index = 0; index < 12; index += 1) {
    await page.keyboard.press('Tab')
    if (await quantity.evaluate(element => element === document.activeElement)) {
      reachedQuantity = true
      break
    }
  }
  expect(reachedQuantity).toBe(true)
  await page.keyboard.press('Tab') // Delivery method
  await page.keyboard.press('Tab') // Quote action
  await expect(page.getByRole('button', { name: '重新試算' })).toBeFocused()
  const quoteResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes')
  await page.keyboard.press('Enter')
  expect((await quoteResponse).status()).toBe(200)
  await expect(page.getByRole('heading', { name: '結帳報價' })).toBeVisible()

  const quote = page.getByRole('region', { name: '結帳報價' })
  const email = quote.getByRole('textbox', { name: '測試 Email' })
  let reachedEmail = false
  for (let index = 0; index < 12; index += 1) {
    await page.keyboard.press('Tab')
    if (await email.evaluate(element => element === document.activeElement)) {
      reachedEmail = true
      break
    }
  }
  expect(reachedEmail).toBe(true)
  await page.keyboard.type('buyer@example.test')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('checkbox')).toBeFocused()
  await page.keyboard.press('Space')
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: '建立模擬訂單並保留庫存' })).toBeFocused()
  const checkoutResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkouts')
  await page.keyboard.press('Enter')
  expect((await checkoutResponse).status()).toBe(200)
  await expect(page.getByText('checkout-issue24-keyboard')).toBeVisible()
  await expect(page.getByText('等待付款')).toBeVisible()
})
