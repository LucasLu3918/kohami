import { expect, test } from '@playwright/test'

test('Issue12 guest confirms a current quote and creates a pending mock order from the cart', async ({ page }) => {
  const cart = {
    id: 'cart-issue12', version: 'cart-v1', promotionCode: null,
    items: [{ skuId: 'demo-shirt-s', quantity: 1, unitPriceTwd: 500, availability: 'InStock' }],
  }
  let quote = {
    id: 'quote-issue12', mode: 'mock', expiresAt: new Date(Date.now() + 60_000).toISOString(),
    snapshot: { currency: 'TWD', merchandiseTwd: 500, discountTwd: 0, shippingTwd: 60, totalTwd: 560 },
  }
  let checkoutRequest: { body: Record<string, any>; headers: Record<string, string> } | undefined

  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: cart }))
  await page.route('**/api/v1/products?**', route => route.fulfill({ json: {
    items: [{ skuId: 'demo-shirt-s', slug: 'demo-shirt', colorId: 'navy', garmentSize: 'S', priceTwd: 500, availability: 'InStock', translations: [{ locale: 'zh-TW', name: '合成上衣' }] }], nextCursor: null,
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-demo' } }))
  let quoteNumber = 0
  await page.route('**/api/v1/checkout-quotes', async route => {
    quoteNumber += 1
    const delivery = route.request().postDataJSON().deliveryMethod
    const shippingTwd = delivery === 'mock_pickup' ? 0 : 60
    quote = { ...quote, id: `quote-issue12-${quoteNumber}`, snapshot: { ...quote.snapshot, shippingTwd, totalTwd: 500 + shippingTwd } }
    await route.fulfill({ status: 201, json: quote })
  })
  await page.route('**/api/v1/checkouts', async route => {
    checkoutRequest = { body: route.request().postDataJSON(), headers: route.request().headers() }
    await route.fulfill({ status: 201, json: {
      checkoutId: 'checkout-issue12', orderId: 'order-issue12', mode: 'mock',
      expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(), snapshot: quote.snapshot,
    } })
  })
  await page.route('**/api/v1/checkouts/checkout-issue12/status', route => route.fulfill({ json: {
    checkoutId: 'checkout-issue12', mode: 'mock', version: 'checkout-v1', orderState: 'AwaitingPayment',
    paymentState: 'Created', compensationRequired: false, expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(), snapshot: quote.snapshot,
  } }))

  await page.goto('/cart?lang=zh-TW')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  await expect(page.getByRole('region', { name: '費用明細' })).toContainText('TWD 560')

  await expect(page.getByLabel('聯絡 Email')).toBeVisible()
  const placeOrder = page.getByRole('button', { name: '建立模擬訂單並保留庫存' })
  await expect(placeOrder).toBeDisabled()
  await page.getByLabel('聯絡 Email').fill('buyer@example.test')
  await page.getByRole('checkbox', { name: /確認這份報價/ }).check()
  await expect(placeOrder).toBeEnabled()

  await page.getByLabel('模擬交付方式').selectOption('mock_pickup')
  await expect(page.getByRole('region', { name: '費用明細' })).toHaveCount(0)
  await page.getByRole('button', { name: '重新試算費用' }).click()
  const confirmation = page.getByRole('checkbox', { name: /確認這份報價/ })
  await expect(confirmation).not.toBeChecked()
  await expect(placeOrder).toBeDisabled()
  await confirmation.check()
  await placeOrder.click()

  await expect(page).toHaveURL(/\/checkout\/checkout-issue12/)
  await expect(page.getByText('等待付款')).toBeVisible()
  expect(checkoutRequest?.body).toMatchObject({
    quoteId: quote.id, cartVersion: 'cart-v1',
    contact: { email: 'buyer@example.test' },
    delivery: { method: 'mock_pickup', region: 'demo_supported' }, confirmed: true,
  })
  expect(checkoutRequest?.headers['if-match']).toBe('"cart-v1"')
  expect(checkoutRequest?.headers['idempotency-key']).toBeTruthy()
})
