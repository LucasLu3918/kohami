import { expect, test, type Page } from '@playwright/test'

type CheckoutFixtureOptions = { member: boolean; insufficient?: boolean }

async function stubCheckout(page: Page, options: CheckoutFixtureOptions) {
  let balance = 1200
  let paymentAttempts = 0
  let sessionState = options.member ? 'MemberAuthenticated' : 'Anonymous'
  let checkoutRequest: Record<string, unknown> | undefined
  let status = {
    checkoutId: 'checkout-wallet03', mode: 'mock', version: 'checkout-created',
    orderState: 'AwaitingPayment', paymentState: 'Created', compensationRequired: false,
    expiresAt: '2026-10-21T00:00:00Z',
    snapshot: { currency: 'TWD', merchandiseTwd: 1000, discountTwd: 0, shippingTwd: 200, totalTwd: 1200 },
  }
  const cart = { id: 'cart-wallet03', version: 'cart-v1', items: [{ skuId: 'sku-wallet03', quantity: 1, unitPriceTwd: 1000, availability: 'InStock' }] as Array<Record<string, unknown>> }

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: {
    state: sessionState, mode: 'mock', userId: sessionState === 'MemberAuthenticated' ? 'member-wallet03' : null,
    email: sessionState === 'MemberAuthenticated' ? 'member-wallet03@example.test' : null,
    permissions: [], expiresAt: null,
  } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'csrf-wallet03' } }))
  await page.route('**/api/v1/orders', route => route.fulfill({ json: { items: [], nextCursor: null } }))
  await page.route('**/api/v1/products**', route => route.fulfill({ json: {
    items: [{ productId: 'product-wallet03', slug: 'wallet03-shirt', skuId: 'sku-wallet03',
      colorId: 'navy', garmentSize: 'S', priceTwd: 1000, availability: 'InStock', mode: 'mock',
      translations: [{ locale: 'en', name: 'Wallet checkout shirt' }], media: [] }],
    nextCursor: null,
  } }))
  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: cart }))
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
  await page.route('**/api/v1/payments**', async route => {
    paymentAttempts += 1
    await route.fulfill({ status: 409, json: { code: 'PAYMENT_NOT_AVAILABLE' } })
  })
  await page.route('**/api/v1/checkouts/checkout-wallet03/status', route => route.fulfill({ json: status }))
  return {
    checkoutRequest: () => checkoutRequest,
    paymentAttempts: () => paymentAttempts,
    balance: () => balance,
  }
}

async function prepareCheckoutQuote(page: Page) {
  const cartLoaded = page.waitForResponse(response => response.url().endsWith('/api/v1/carts/current') && response.request().method() === 'GET')
  await page.goto('/en/cart')
  await cartLoaded
  await page.getByRole('button', { name: 'Recalculate fees' }).click()
  await expect(page.getByRole('region', { name: 'Fee summary' })).toBeVisible()
}

async function createCheckoutFromStorefront(page: Page, buttonName: RegExp) {
  await prepareCheckoutQuote(page)
  const quote = page.getByRole('region', { name: 'Fee summary' })
  await expect(quote).toContainText('Order email：member-wallet03@example.test')
  await quote.getByLabel(/Member wallet/).check()
  await quote.getByRole('checkbox', { name: /I confirm this quote/ }).check()
  const request = page.waitForResponse(response => response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST')
  await page.getByRole('button', { name: buttonName }).click()
  return request
}

test('authenticated member email is sent with the wallet checkout request', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: true })
  const response = await createCheckoutFromStorefront(page, /Place order and reserve stock/)

  expect(response.status()).toBe(201)
  expect(fixture.checkoutRequest()).toMatchObject({ paymentMethod: 'wallet', contact: { email: 'member-wallet03@example.test' }, confirmed: true })
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  expect(fixture.balance()).toBe(0)
  expect(fixture.paymentAttempts()).toBe(0)
})

test('insufficient member balance is shown without starting another payment request', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: true, insufficient: true })
  const response = await createCheckoutFromStorefront(page, /Place order and reserve stock/)

  expect(response.status()).toBe(409)
  await expect(page.getByRole('alert')).toContainText('Your member balance is insufficient')
  await expect(page.getByText(/Member wallet · Available TWD 1,200/)).toBeVisible()
  expect(fixture.paymentAttempts()).toBe(0)
  expect(fixture.balance()).toBe(1200)
})

test('anonymous shopper can quote a cart but must sign in before creating an order', async ({ page }) => {
  const fixture = await stubCheckout(page, { member: false })
  await prepareCheckoutQuote(page)

  await expect(page.getByRole('heading', { name: 'Sign in to continue checkout' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Create an account' })).toBeVisible()
  await expect(page.getByLabel('Test email')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Place order and reserve stock' })).toHaveCount(0)
  expect(fixture.checkoutRequest()).toBeUndefined()
  expect(fixture.paymentAttempts()).toBe(0)
})
