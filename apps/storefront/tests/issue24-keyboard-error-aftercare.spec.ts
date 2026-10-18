import { expect, test } from '@playwright/test'
import type { Locator, Page } from '@playwright/test'

async function skipToMain(page: Page) {
  await page.keyboard.press('Tab')
  const skipLink = page.getByRole('link', { name: 'Skip to main content' })
  await expect(skipLink).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()
}

async function tabTo(page: Page, target: Locator, limit = 160) {
  for (let index = 0; index < limit; index += 1) {
    if (await target.evaluate(element => element === document.activeElement)) return
    await page.keyboard.press('Tab')
  }
  throw new Error('Keyboard navigation did not reach the expected control')
}

test('desktop keyboard checkout recovers from quote and order creation failures', async ({ page }) => {
  let quoteReads = 0
  let checkoutWrites = 0
  const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
  const cart = {
    id: 'cart-issue24-recovery', version: 'cart-v1',
    items: [{ skuId: 'sku-issue24-recovery', quantity: 1, unitPriceTwd: 1200, availability: 'InStock' }],
  }
  const snapshot = { currency: 'TWD', merchandiseTwd: 1200, discountTwd: 0, shippingTwd: 60, totalTwd: 1260 }

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname === '/api/v1/auth/session') return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (url.pathname === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: 'synthetic-issue24-csrf' } })
    if (url.pathname === '/api/v1/products') return route.fulfill({ json: { items: [], nextCursor: null } })
    if (url.pathname === '/api/v1/carts/current') return route.fulfill({ json: cart })
    if (url.pathname === '/api/v1/checkout-quotes' && request.method() === 'POST') {
      quoteReads += 1
      if (quoteReads === 1) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      return route.fulfill({ json: { id: 'quote-issue24-recovery', mode: 'mock', expiresAt, snapshot } })
    }
    if (url.pathname === '/api/v1/checkouts' && request.method() === 'POST') {
      checkoutWrites += 1
      if (checkoutWrites === 1) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      return route.fulfill({ json: {
        checkoutId: 'checkout-issue24-recovery', orderId: 'order-issue24-recovery', mode: 'mock',
        version: 'checkout-v1', expiresAt, snapshot,
        statusUrl: '/api/v1/checkouts/checkout-issue24-recovery/status',
      } })
    }
    if (url.pathname === '/api/v1/checkouts/checkout-issue24-recovery/status') {
      return route.fulfill({ json: {
        checkoutId: 'checkout-issue24-recovery', mode: 'mock', version: 'status-v1',
        orderState: 'AwaitingPayment', paymentState: 'Created', compensationRequired: false,
        expiresAt, snapshot: { ...snapshot, lines: [{ skuId: 'sku-issue24-recovery', quantity: 1, unitPriceTwd: 1200 }] },
      } })
    }
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  await page.goto('/en')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  await skipToMain(page)

  const quoteButton = page.getByRole('button', { name: 'Refresh quote' })
  await tabTo(page, quoteButton)
  const failedQuote = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/checkout-quotes')
  await page.keyboard.press('Enter')
  expect((await failedQuote).status()).toBe(503)
  const quoteAlert = page.getByRole('alert').filter({ hasText: 'The local demo service is temporarily unavailable.' })
  await expect(quoteAlert).toBeVisible()
  await expect(quoteButton).toBeFocused()

  const recoveredQuote = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/checkout-quotes' && response.status() === 200)
  await page.keyboard.press('Enter')
  expect((await recoveredQuote).status()).toBe(200)
  const quote = page.getByRole('region', { name: 'Checkout quote' })
  await expect(quote).toBeVisible()

  const email = quote.getByRole('textbox', { name: 'Test email' })
  await tabTo(page, email)
  await page.keyboard.type('buyer@example.test')
  await page.keyboard.press('Tab')
  const confirmation = quote.getByRole('checkbox')
  await expect(confirmation).toBeFocused()
  await page.keyboard.press('Space')
  const createCheckout = quote.getByRole('button', { name: 'Create mock order and reserve stock' })
  await page.keyboard.press('Tab')
  await expect(createCheckout).toBeFocused()

  const failedCheckout = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/checkouts')
  await page.keyboard.press('Enter')
  expect((await failedCheckout).status()).toBe(503)
  await expect(quote.getByRole('alert')).toContainText('The local demo service is temporarily unavailable.')
  await expect(email).toHaveValue('buyer@example.test')
  await expect(confirmation).toBeChecked()
  await expect(createCheckout).toBeFocused()

  const recoveredCheckout = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/checkouts' && response.status() === 200)
  await page.keyboard.press('Enter')
  expect((await recoveredCheckout).status()).toBe(200)
  await expect(page.getByText('checkout-issue24-recovery')).toBeVisible()
  await expect(page.getByText('Awaiting payment')).toBeVisible()
})

test('desktop keyboard aftercare keeps safe errors and form values available for retry', async ({ page }) => {
  let returnAttempts = 0
  let returnCreated = false
  const order = () => ({
    id: 'issue24_owner_order', version: 'order-v1', orderState: 'Confirmed',
    snapshot: { lines: [{ orderLineId: 'issue24_line', skuId: 'demo-sku', quantity: 1 }] },
    shipmentSummary: { state: 'Delivered', shipments: [] },
    returns: returnCreated ? [{
      id: 'issue24_return_case', state: 'Requested', version: 'return-v1',
      caseSummary: { classification: 'Return', openedAt: '2026-10-18T00:00:00Z', firstResponseAt: null, firstResponseElapsedSeconds: null, escalatedAt: null, escalationElapsedSeconds: null },
      timeline: [{ state: 'Requested', at: '2026-10-18T00:00:00Z' }], inspectionSummary: null,
      lines: [{ orderLineId: 'issue24_line', requestedQuantity: 1, acceptedQuantity: 0, receivedUnitIds: [], restockedUnitIds: [], quarantinedUnitIds: [] }],
    }] : [],
  })

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    if (path === '/auth/session') return json({ state: 'Anonymous', mode: 'mock' })
    if (path === '/auth/csrf') return json({ requestToken: 'synthetic-issue24-csrf' })
    if (path === '/products') return json({ items: [], nextCursor: null })
    if (path === '/carts/current') return json({ id: 'issue24_cart', version: 'cart-v1', items: [] })
    if (path === '/orders/hidden_order') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === '/orders/service_offline') return json({ code: 'SERVICE_UNAVAILABLE' }, 503)
    if (path === '/orders/issue24_owner_order' && request.method() === 'GET') return json(order())
    if (path === '/orders/issue24_owner_order/receipts') return json({ orderId: 'issue24_owner_order', taxEffect: 'none', receipts: [], adjustments: [] })
    if (path === '/orders/issue24_owner_order/returns' && request.method() === 'POST') {
      returnAttempts += 1
      if (returnAttempts === 1) return json({ code: 'SERVICE_UNAVAILABLE' }, 503)
      returnCreated = true
      return json({ id: 'issue24_return_case', state: 'Requested', version: 'return-v1', quantity: 1, orderLineId: 'issue24_line' }, 201)
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/en')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  await skipToMain(page)

  const aftercare = page.getByRole('region', { name: 'Order delivery and aftercare (mock)' })
  const orderId = aftercare.getByRole('textbox', { name: 'Order ID' })
  await tabTo(page, orderId)

  await page.keyboard.type('hidden_order')
  await page.keyboard.press('Tab')
  await expect(aftercare.getByRole('button', { name: 'Load my order' })).toBeFocused()
  const hiddenLookup = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/orders/hidden_order')
  await page.keyboard.press('Enter')
  expect((await hiddenLookup).status()).toBe(404)
  await expect(aftercare.getByRole('alert')).toHaveText('This order is unavailable for the current identity. Check the account or order proof, then try again.')
  await expect(aftercare).not.toContainText('issue24_owner_order')

  await tabTo(page, orderId)
  await page.keyboard.press('Meta+A')
  await page.keyboard.type('service_offline')
  await page.keyboard.press('Tab')
  const offlineLookup = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/orders/service_offline')
  await page.keyboard.press('Enter')
  expect((await offlineLookup).status()).toBe(503)
  await expect(aftercare.getByRole('alert')).toHaveText('The local service could not load this order. Try again later.')
  await expect(orderId).toHaveValue('service_offline')

  await tabTo(page, orderId)
  await page.keyboard.press('Meta+A')
  await page.keyboard.type('issue24_owner_order')
  await page.keyboard.press('Tab')
  const validLookup = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/orders/issue24_owner_order')
  await page.keyboard.press('Enter')
  expect((await validLookup).status()).toBe(200)
  await expect(aftercare).toContainText('issue24_owner_order')

  const lineId = aftercare.getByRole('textbox', { name: 'Order line ID' })
  await tabTo(page, lineId)
  await page.keyboard.type('issue24_line')
  await page.keyboard.press('Tab')
  await page.keyboard.type('1')
  await page.keyboard.press('Tab')
  const reason = aftercare.getByRole('textbox', { name: 'Reason', exact: true }).first()
  await expect(reason).toBeFocused()
  await page.keyboard.type('Synthetic Issue 24 keyboard return request')
  await page.keyboard.press('Tab')
  const requestReturn = aftercare.getByRole('button', { name: 'Request a return' })
  await expect(requestReturn).toBeFocused()

  const failedReturn = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/orders/issue24_owner_order/returns')
  await page.keyboard.press('Enter')
  expect((await failedReturn).status()).toBe(503)
  await expect(aftercare.getByRole('alert')).toHaveText('The local service could not load this order. Try again later.')
  await expect(reason).toHaveValue('Synthetic Issue 24 keyboard return request')
  await expect(requestReturn).toBeFocused()

  const recoveredReturn = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/orders/issue24_owner_order/returns' && response.status() === 201)
  await page.keyboard.press('Enter')
  expect((await recoveredReturn).status()).toBe(201)
  await expect(aftercare.getByRole('region', { name: 'Return case timeline' })).toContainText('Requested')
  await expect(aftercare.getByText('issue24_return_case', { exact: true })).toBeVisible()
})
