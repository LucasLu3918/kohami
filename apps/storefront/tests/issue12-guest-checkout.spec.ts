import { expect, test } from '@playwright/test'

test('Issue12 guest can review a quote but must sign in before creating an order', async ({ page }) => {
  const cart = {
    id: 'cart-issue12', version: 'cart-v1', promotionCode: null,
    items: [{ skuId: 'demo-shirt-s', quantity: 1, unitPriceTwd: 500, availability: 'InStock' }],
  }
  let quote = {
    id: 'quote-issue12', mode: 'mock', expiresAt: new Date(Date.now() + 60_000).toISOString(),
    snapshot: { currency: 'TWD', merchandiseTwd: 500, discountTwd: 0, shippingTwd: 60, totalTwd: 560 },
  }
  let checkoutRequests = 0

  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: cart }))
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'Anonymous', mode: 'mock', userId: null, permissions: [], expiresAt: null } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-demo' } }))
  await page.route('**/api/v1/products?**', route => route.fulfill({ json: {
    items: [{ skuId: 'demo-shirt-s', slug: 'demo-shirt', colorId: 'navy', garmentSize: 'S', priceTwd: 500, availability: 'InStock', translations: [{ locale: 'zh-TW', name: '合成上衣' }] }], nextCursor: null,
  } }))
  let quoteNumber = 0
  await page.route('**/api/v1/checkout-quotes', async route => {
    quoteNumber += 1
    const delivery = route.request().postDataJSON().deliveryMethod
    const shippingTwd = delivery === 'mock_pickup' ? 0 : 60
    quote = { ...quote, id: `quote-issue12-${quoteNumber}`, snapshot: { ...quote.snapshot, shippingTwd, totalTwd: 500 + shippingTwd } }
    await route.fulfill({ status: 201, json: quote })
  })
  await page.route('**/api/v1/checkouts', async route => {
    checkoutRequests += 1
    await route.fulfill({ status: 401, json: { code: 'AUTHENTICATION_REQUIRED' } })
  })

  await page.goto('/cart?lang=zh-TW')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('region', { name: '費用明細' })).toContainText('TWD 560')

  await expect(page.getByRole('heading', { name: '登入會員以繼續結帳' })).toBeVisible()
  await expect(page.getByRole('link', { name: '會員登入' })).toBeVisible()
  await expect(page.getByRole('link', { name: '建立會員' })).toBeVisible()
  await expect(page.getByLabel('聯絡 Email')).toHaveCount(0)
  await expect(page.getByRole('button', { name: '建立模擬訂單並保留庫存' })).toHaveCount(0)
  expect(checkoutRequests).toBe(0)

  await page.getByLabel('模擬交付方式').selectOption('mock_pickup')
  await expect(page.getByRole('region', { name: '費用明細' })).toHaveCount(0)
  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('region', { name: '費用明細' })).toContainText('TWD 500')
  await expect(page.getByRole('heading', { name: '登入會員以繼續結帳' })).toBeVisible()
  expect(checkoutRequests).toBe(0)
})
