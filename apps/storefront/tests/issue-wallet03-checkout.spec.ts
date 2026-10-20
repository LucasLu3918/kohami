import { expect, test, type Page } from '@playwright/test'

type CheckoutFixtureOptions = { member: boolean; insufficient?: boolean }

async function stubCheckout(page: Page, options: CheckoutFixtureOptions) {
  let balance = 1200
  let paymentAttempts = 0
  let checkoutRequest: Record<string, unknown> | undefined
  let status = {
    checkoutId: 'checkout-wallet03', mode: 'mock', version: 'checkout-created',
    orderState: 'AwaitingPayment', paymentState: 'Created', compensationRequired: false,
    expiresAt: '2026-10-21T00:00:00Z',
    snapshot: { currency: 'TWD', merchandiseTwd: 1000, discountTwd: 0, shippingTwd: 200, totalTwd: 1200 },
  }
  let cart = { id: 'cart-wallet03', version: 'cart-v1', items: [] as Array<Record<string, unknown>> }

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: {
    state: options.member ? 'MemberAuthenticated' : 'Anonymous', mode: 'mock',
    userId: options.member ? 'member-wallet03' : null, permissions: [], expiresAt: null,
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-wallet03' } }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: {
    items: [{ productId: 'product-wallet03', slug: 'wallet03-shirt', skuId: 'sku-wallet03',
      colorId: 'navy', garmentSize: 'S', priceTwd: 1000, availability: 'InStock', mode: 'mock',
      translations: [{ locale: 'en', name: 'Wallet checkout shirt' }], media: [] }],
    nextCursor: null,
  } }))
  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: cart }))
  await page.route('**/api/v1/carts/current/items/**', async route => {
    cart = { ...cart, version: 'cart-v2', items: [{ skuId: 'sku-wallet03', quantity: 1, unitPriceTwd: 1000, availability: 'InStock' }] }
    await route.fulfill({ json: cart })
  })
  await page.route('**/api/v1/checkout-quotes', route => route.fulfill({ status: 201, json: {
    id: 'quote-wallet03', mode: 'mock', expiresAt: '2026-10-21T00:00:00Z',
    snapshot: { currency: 'TWD', merchandiseTwd: 1000, discountTwd: 0, shippingTwd: 200, totalTwd: 1200 },
  } }))
  await page.route('**/api/v1/wallet/current**', route => route.fulfill({ json: {
    mode: 'mock', currency: 'TWD', availableTwd: balance, version: 1, entries: [], nextCursor: null,
  } }))
  await page.route('**/api/v1/checkouts', async route => {
    checkoutRequest = route.request().postDataJSON() as Record<string, unknown>
    if (options.member && options.insufficient) {
      await route.fulfill({ status: 409, json: { code: 'WALLET_INSUFFICIENT_BALANCE' } })
      return
    }
    if (options.member) {
      balance = 0
      status = { ...status, version: 'checkout-confirmed', orderState: 'Confirmed', paymentState: 'Succeeded' }
    }
    await route.fulfill({ status: 201, json: {
      checkoutId: 'checkout-wallet03', orderId: 'order-wallet03', mode: 'mock',
      version: 'checkout-created', expiresAt: status.expiresAt, snapshot: status.snapshot,
    } })
  })
  await page.route('**/api/v1/checkouts/checkout-wallet03/status', route => route.fulfill({ json: status }))
  await page.route('**/api/v1/checkouts/checkout-wallet03/payment-attempts', async route => {
    paymentAttempts += 1
    status = { ...status, version: 'checkout-processing', paymentState: 'Processing' }
    await route.fulfill({ status: 202, json: { id: 'attempt-wallet03', state: 'Processing' } })
  })

  return {
    checkoutRequest: () => checkoutRequest,
    paymentAttempts: () => paymentAttempts,
    balance: () => balance,
  }
}

async function createCheckoutFromStorefront(page: Page, buttonName: RegExp) {
  const cartLoaded = page.waitForResponse(response => response.url().endsWith('/api/v1/carts/current') && response.request().method() === 'GET')
  await page.goto('/en')
  await cartLoaded
  const sessionRefreshed = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/session') && response.request().method() === 'GET')
  await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent('pageshow')))
  await sessionRefreshed
  await expect(page.getByText('Your cart is empty.')).toBeVisible()
  await page.getByRole('searchbox', { name: 'Search product name, SKU or description' }).fill('wallet03')
  const productsLoaded = page.waitForResponse(response => {
    const url = new URL(response.url())
    return url.pathname === '/api/v1/products' && url.searchParams.get('q') === 'wallet03' && response.request().method() === 'GET'
  })
  await page.getByRole('search').getByRole('button', { name: 'Search products' }).click()
  await productsLoaded
  const products = page.getByRole('article')
  await expect(products).toHaveCount(1)
  let product = products.first()
  let variant = product.getByRole('combobox', { name: /Color and garment size:/ })
  let availableVariant: string | undefined
  for (let index = 0; index < await products.count() && !availableVariant; index += 1) {
    product = products.nth(index)
    variant = product.getByRole('combobox', { name: /Color and garment size:/ })
    availableVariant = await variant.locator('option').evaluateAll(options => options.find(option => (option as HTMLOptionElement).value && !(option as HTMLOptionElement).disabled)?.getAttribute('value'))
  }
  if (!availableVariant) throw new Error('No in-stock SKU is available in the local catalog')
  await variant.selectOption(availableVariant)
  await product.getByRole('button', { name: 'Add to cart' }).click()
  await page.getByRole('button', { name: 'Refresh quote' }).click()
  const quote = page.getByRole('region', { name: 'Checkout quote' })
  await quote.getByLabel('Test email').fill('wallet03@example.test')
  await quote.getByRole('checkbox', { name: /I confirm this quote/ }).check()
  const request = page.waitForResponse(response => response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST')
  await page.getByRole('button', { name: buttonName }).click()
  return request
}

test('member checkout pays in the checkout request and refreshes status and balance', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: true })
  const response = await createCheckoutFromStorefront(page, /Create wallet order and pay/)

  expect(response.status()).toBe(201)
  expect(fixture.checkoutRequest()).toMatchObject({ paymentMethod: 'wallet', confirmed: true })
  await expect(page.getByText(/Available member balance: TWD 0/)).toBeVisible()
  await expect(page.getByText('Wallet payment succeeded; the order is confirmed.')).toBeVisible()
  await expect(page.getByRole('region', { name: 'Mock checkout' })).toContainText('Confirmed')
  expect(fixture.balance()).toBe(0)
  expect(fixture.paymentAttempts()).toBe(0)
})

test('insufficient member balance is shown without starting another payment request', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: true, insufficient: true })
  const response = await createCheckoutFromStorefront(page, /Create wallet order and pay/)

  expect(response.status()).toBe(409)
  await expect(page.getByRole('region', { name: 'Member wallet payment' }).getByRole('alert')).toContainText('Your member balance is insufficient')
  await expect(page.getByText(/Available member balance: TWD 1,200/)).toBeVisible()
  expect(fixture.paymentAttempts()).toBe(0)
  expect(fixture.balance()).toBe(1200)
})

test('guest checkout omits the wallet method and retains mock payment attempts', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: false })
  const response = await createCheckoutFromStorefront(page, /Create mock order and reserve stock/)

  expect(response.status()).toBe(201)
  expect(fixture.checkoutRequest()).not.toHaveProperty('paymentMethod')
  const checkout = page.getByRole('region', { name: 'Mock checkout' })
  const paymentAttempt = page.waitForResponse(response => response.url().endsWith('/api/v1/checkouts/checkout-wallet03/payment-attempts') && response.request().method() === 'POST')
  await checkout.getByRole('button', { name: 'Start mock payment' }).click()
  expect((await paymentAttempt).status()).toBe(202)
  expect(fixture.paymentAttempts()).toBe(1)
})
