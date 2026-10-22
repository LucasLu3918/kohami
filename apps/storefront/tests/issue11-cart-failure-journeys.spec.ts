import { expect, test } from '@playwright/test'

const cart = {
  id: 'cart-issue11-mock',
  version: 'cart-v1',
  items: [
    { skuId: 'sku-removed', quantity: 1, unitPriceTwd: 1200, availability: 'InStock' },
    { skuId: 'sku-disabled', quantity: 1, unitPriceTwd: 1500, availability: 'Unavailable' },
    { skuId: 'sku-out-of-stock', quantity: 1, unitPriceTwd: 1600, availability: 'InStock' },
    { skuId: 'sku-price-changed', quantity: 1, unitPriceTwd: 1700, availability: 'InStock' },
  ],
}

test('Issue11 cart explains removed and unavailable SKUs and lets the shopper remove them', async ({ page }) => {
  let currentCart = structuredClone(cart)
  let checkoutAttempts = 0

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'Anonymous' } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-issue11' } }))
  await page.route('**/api/v1/carts/current', async route => {
    if (route.request().method() === 'GET') return route.fulfill({ json: currentCart })
    const skuId = decodeURIComponent(new URL(route.request().url()).pathname.split('/').at(-1) || '')
    currentCart = { ...currentCart, version: 'cart-v2', items: currentCart.items.filter(item => item.skuId !== skuId) }
    return route.fulfill({ json: currentCart })
  })
  await page.route('**/api/v1/products**', route => route.fulfill({ json: {
    items: [
      { slug: 'disabled-item', skuId: 'sku-disabled', colorId: 'navy', garmentSize: 'S', priceTwd: 1500,
        availability: 'Unavailable', translations: [{ locale: 'zh-TW', name: '停售商品' }] },
      { slug: 'out-of-stock-item', skuId: 'sku-out-of-stock', colorId: 'black', garmentSize: 'M', priceTwd: 1600,
        availability: 'OutOfStock', translations: [{ locale: 'zh-TW', name: '缺貨商品' }] },
      { slug: 'price-changed-item', skuId: 'sku-price-changed', colorId: 'gray', garmentSize: 'L', priceTwd: 1800,
        availability: 'InStock', translations: [{ locale: 'zh-TW', name: '改價商品' }] },
    ],
    nextCursor: null,
  } }))
  await page.route('**/api/v1/carts/current/items/**', async route => {
    const skuId = decodeURIComponent(new URL(route.request().url()).pathname.split('/').at(-1) || '')
    currentCart = { ...currentCart, version: 'cart-v2', items: currentCart.items.filter(item => item.skuId !== skuId) }
    return route.fulfill({ json: currentCart })
  })
  await page.route('**/api/v1/checkout-quotes', route => route.fulfill({ status: 409, json: { code: 'PRODUCT_UNAVAILABLE' } }))
  await page.route('**/api/v1/checkouts', async route => {
    checkoutAttempts += 1
    await route.fulfill({ status: 409, json: { code: 'PRODUCT_UNAVAILABLE' } })
  })

  await page.goto('/cart?lang=zh-TW')
  const lines = page.getByRole('listitem')
  await expect(lines).toHaveCount(4)
  await expect(lines.filter({ hasText: 'sku-removed' }).getByText('商品已不存在或目前停售')).toBeVisible()
  await expect(lines.filter({ hasText: '停售商品' }).getByText('商品已不存在或目前停售')).toBeVisible()
  await expect(lines.filter({ hasText: '缺貨商品' }).getByText('商品目前缺貨')).toBeVisible()
  await expect(lines.filter({ hasText: '改價商品' }).getByText('示意價格已更新')).toBeVisible()

  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('alert')).toContainText('商品目前缺貨或已停售')
  expect(checkoutAttempts).toBe(0)

  await lines.filter({ hasText: 'sku-removed' }).getByRole('button', { name: '移除商品' }).click()
  await expect(page.getByRole('listitem')).toHaveCount(3)
  await lines.filter({ hasText: '停售商品' }).getByRole('button', { name: '移除商品' }).click()
  await lines.filter({ hasText: '缺貨商品' }).getByRole('button', { name: '移除商品' }).click()
  await lines.filter({ hasText: '改價商品' }).getByRole('button', { name: '移除商品' }).click()
  await expect(page.getByText('購物車目前是空的。')).toBeVisible()
})

test('Issue11 changes cart quantity, recovers from stock rejection and retries quote load', async ({ page }) => {
  let currentCart = { id: 'cart-issue11-journey', version: 'cart-v1', items: [{
    skuId: 'sku-issue11-add', quantity: 1, unitPriceTwd: 1000, availability: 'InStock',
  }] }
  let rejectedQuantity = false
  let quoteFailures = 0
  const quoteSnapshot = { currency: 'TWD', merchandiseTwd: 2000, discountTwd: 0, shippingTwd: 60, totalTwd: 2060 }

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'Anonymous' } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-issue11' } }))
  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: currentCart }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/carts/current/items/sku-issue11-add', async route => {
    const { quantity } = route.request().postDataJSON() as { quantity: number }
    if (quantity === 3) {
      rejectedQuantity = true
      return route.fulfill({ status: 409, json: { code: 'STOCK_CHANGED' } })
    }
    currentCart = { ...currentCart, version: `cart-v${quantity + 1}`, items: [{
      skuId: 'sku-issue11-add', quantity, unitPriceTwd: 1000, availability: 'InStock',
    }] }
    return route.fulfill({ status: 200, json: currentCart })
  })
  await page.route('**/api/v1/checkout-quotes', async route => {
    quoteFailures += 1
    if (quoteFailures === 1) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
    return route.fulfill({ status: 201, json: {
      id: 'quote-issue11-retried', mode: 'mock', expiresAt: '2099-01-01T00:00:00Z', snapshot: quoteSnapshot,
    } })
  })

  await page.goto('/cart?lang=zh-TW')
  const line = page.getByRole('listitem').filter({ hasText: 'sku-issue11-add' })
  const quantity = line.getByRole('spinbutton', { name: '數量' })
  await quantity.fill('2')
  await quantity.press('Tab')
  await expect(line).toContainText('TWD 2000')

  await quantity.fill('3')
  await quantity.press('Tab')
  await expect(page.getByRole('alert')).toContainText('可售庫存不足，請確認商品數量後重試。')
  expect(rejectedQuantity).toBeTruthy()
  await expect(quantity).toHaveValue('2')

  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('alert')).toContainText('目前無法連線至本機服務，請稍後重試。')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('region', { name: '費用明細' })).toBeVisible()
  expect(quoteFailures).toBe(2)
})

test('Issue11 expired quote rejection clears stale totals and offers a fresh quote path', async ({ page }) => {
  let quoteCount = 0
  let checkoutAttempts = 0
  const currentCart = { id: 'cart-issue11-expired', version: 'cart-v1', items: [
    { skuId: 'sku-expired', quantity: 1, unitPriceTwd: 1000, availability: 'InStock' },
  ] }
  const quoteSnapshot = { currency: 'TWD', merchandiseTwd: 1000, discountTwd: 0, shippingTwd: 60, totalTwd: 1060 }

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: {
    state: 'MemberAuthenticated', userId: 'member-issue11', email: 'member@example.test',
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-issue11' } }))
  await page.route('**/api/v1/wallet/current**', route => route.fulfill({ json: {
    mode: 'mock', currency: 'TWD', availableTwd: 0, version: 1, entries: [], nextCursor: null,
  } }))
  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: currentCart }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: {
    items: [{ slug: 'expired-item', skuId: 'sku-expired', colorId: 'navy', garmentSize: 'S', priceTwd: 1000,
      availability: 'InStock', translations: [{ locale: 'zh-TW', name: '測試商品' }] }],
    nextCursor: null,
  } }))
  await page.route('**/api/v1/checkout-quotes', async route => {
    quoteCount += 1
    await route.fulfill({ status: 201, json: {
      id: `quote-issue11-${quoteCount}`, mode: 'mock',
      expiresAt: quoteCount === 1 ? '2020-01-01T00:00:00Z' : '2099-01-01T00:00:00Z', snapshot: quoteSnapshot,
    } })
  })
  await page.route('**/api/v1/checkouts', async route => {
    checkoutAttempts += 1
    await route.fulfill({ status: 409, json: { code: 'QUOTE_EXPIRED' } })
  })

  await page.goto('/cart?lang=zh-TW')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  const quoteRegion = page.getByRole('region', { name: '費用明細' })
  await expect(quoteRegion).toBeVisible()
  await quoteRegion.getByRole('checkbox', { name: /確認這份報價/ }).check()
  await page.getByRole('button', { name: '建立模擬訂單並保留庫存' }).click()

  await expect(page.getByRole('alert')).toContainText('報價已過期，請重新試算並確認金額。')
  await expect(page.getByRole('region', { name: '費用明細' })).toHaveCount(0)
  expect(checkoutAttempts).toBe(1)

  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('region', { name: '費用明細' })).toBeVisible()
  await expect(page.getByRole('alert')).toHaveCount(0)
  expect(quoteCount).toBe(2)
})
