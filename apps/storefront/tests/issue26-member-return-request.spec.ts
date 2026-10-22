import { expect, test, type Page } from '@playwright/test'

type ReturnLine = { orderLineId: string; skuId: string; name: { value: string }; colorId: string; garmentSize: string; quantity: number; returnableQuantity: number; unitPriceTwd: number; paidTwd: number }
type ReturnActionOptions = { actionAllowed?: boolean; postStatus?: number; lines?: ReturnLine[] }

async function stubMemberOrder(page: Page, options: ReturnActionOptions = {}) {
  let returnRequested = false
  let detailReads = 0
  let orderListReads = 0
  let returnPosts = 0
  let submitted: { body: Record<string, unknown>; headers: Record<string, string> } | undefined
  const fixtureLines = options.lines || [{ orderLineId: 'line-shirt', skuId: 'sku_shirt', name: { value: 'Demo Shirt' }, colorId: 'navy', garmentSize: 'M', quantity: 2, returnableQuantity: 2, unitPriceTwd: 795, paidTwd: 1590 }]
  const snapshotLines = fixtureLines.map(({ returnableQuantity: _available, ...line }) => line)
  const returnableLines = fixtureLines.filter(line => line.returnableQuantity > 0).map(line => ({ orderLineId: line.orderLineId, availableQuantity: line.returnableQuantity }))

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    if (path === '/auth/session') return json({ state: 'MemberAuthenticated', mode: 'mock', email: 'member@example.test' })
    if (path === '/auth/csrf') return json({ requestToken: 'synthetic-csrf-token' })
    if (path === '/products') return json({ items: [] })
    if (path === '/carts/current') return json({ id: 'cart', version: 'cart-v1', items: [] })
    if (path === '/wallet/current') return json({ mode: 'mock', currency: 'TWD', availableTwd: 0, version: 1, entries: [], nextCursor: null })
    if (path === '/orders') {
      orderListReads++
      return json({ items: [{
        id: 'issue26_return_order', mode: 'mock', version: 'order-v1', placedAt: '2026-10-21T12:00:00Z', orderState: 'Confirmed',
        snapshot: { currency: 'TWD', totalTwd: 1650, lines: snapshotLines },
      }] })
    }
    if (path === '/orders/issue26_return_order') {
      detailReads++
      const actions = options.actionAllowed !== false && !returnRequested ? [{ action: 'RequestReturn', allowed: true, reasonCodes: [], returnableLines }] : []
      return json({
        id: 'issue26_return_order', mode: 'mock', version: `order-v${detailReads}`, placedAt: '2026-10-21T12:00:00Z', orderState: 'Confirmed',
        snapshot: { currency: 'TWD', totalTwd: 1650, merchandiseTwd: 1590, discountTwd: 0, shippingTwd: 60, lines: snapshotLines },
        paymentSummary: { state: 'Succeeded', confirmedAt: '2026-10-21T12:01:00Z', confirmedCollectedTwd: 1650, compensationRequired: false },
        shipmentSummary: { state: 'Delivered', shipments: [{ id: 'shipment_demo', state: 'Delivered', createdAt: '2026-10-21T12:02:00Z', history: [{ state: 'Delivered', at: '2026-10-22T12:02:00Z' }], timing: { paymentToShipmentSeconds: 60, shipmentToShippedSeconds: 120, shippedToDeliveredSeconds: 86400 } }] },
        refundSummary: { requestedCount: 0, processingCount: 0, unknownCount: 0, succeededCount: 0, failedCount: 0, succeededTwd: 0, pendingTwd: 0 },
        receiptSummary: { state: 'NotCreated', pendingAdjustmentCount: 0 }, maskedContact: 'm***@example.test',
        returns: returnRequested ? [{ id: 'return_demo', state: 'Requested', caseSummary: { classification: 'Return', openedAt: '2026-10-22T12:00:00Z', firstResponseAt: null, firstResponseElapsedSeconds: null, escalatedAt: null, escalationElapsedSeconds: null }, timeline: [{ state: 'Requested', at: '2026-10-22T12:00:00Z' }], inspectionSummary: null }] : [],
        actions,
      })
    }
    if (path === '/orders/issue26_return_order/receipts') return json({ orderId: 'issue26_return_order', mode: 'mock', taxEffect: 'none', receipts: [], adjustments: [] })
    if (path === '/orders/issue26_return_order/returns' && request.method() === 'POST') {
      returnPosts++
      submitted = { body: request.postDataJSON() as Record<string, unknown>, headers: request.headers() }
      if (options.postStatus && options.postStatus !== 201) return json({ code: options.postStatus === 409 ? 'INVALID_TRANSITION' : options.postStatus === 422 ? 'VALIDATION_ERROR' : 'SERVICE_UNAVAILABLE' }, options.postStatus)
      returnRequested = true
      return json({ id: 'return_demo', state: 'Requested', mode: 'mock' }, 201)
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  return {
    get submitted() { return submitted },
    get detailReads() { return detailReads },
    get orderListReads() { return orderListReads },
    get returnPosts() { return returnPosts },
  }
}

test('Issue26 member can request a return with the active API payload and refresh after acceptance', async ({ page }) => {
  const api = await stubMemberOrder(page)
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()

  const form = order.getByRole('form', { name: 'Request a return' })
  await expect(form).toBeVisible()
  await expect(form.getByLabel('Item')).toContainText('Demo Shirt')
  await expect(form.getByLabel('Quantity')).toHaveAttribute('max', '2')
  await form.getByLabel('Quantity').fill('2')
  await form.getByLabel('Reason').fill('Size does not fit')
  await form.getByRole('button', { name: 'Submit return request' }).click()

  await expect(order).toContainText('Requested')
  await expect(order.getByRole('form', { name: 'Request a return' })).toHaveCount(0)
  expect(api.submitted?.body).toEqual({ orderLineId: 'line-shirt', quantity: 2, reason: 'Size does not fit' })
  expect(api.submitted?.headers['x-csrf-token']).toBe('synthetic-csrf-token')
  expect(api.submitted?.headers['idempotency-key']).toMatch(/^storefront-return-/)
  expect(api.submitted?.headers['origin']).toBe(new URL(page.url()).origin)
  expect(api.submitted?.headers['if-match']).toBeUndefined()
  expect(api.returnPosts).toBe(1)
  expect(api.orderListReads).toBeGreaterThan(1)
  expect(api.detailReads).toBeGreaterThan(1)
})

test('Issue26 stale return request refreshes without automatic retry and reports accessible error', async ({ page }) => {
  const api = await stubMemberOrder(page, { postStatus: 409 })
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()
  const form = order.getByRole('form', { name: 'Request a return' })
  await form.getByLabel('Reason').fill('Changed my mind')
  await form.getByRole('button', { name: 'Submit return request' }).click()

  await expect(order.getByRole('alert')).toContainText('refreshed')
  await expect(order.getByRole('form', { name: 'Request a return' })).toBeVisible()
  expect(api.returnPosts).toBe(1)
  expect(api.detailReads).toBeGreaterThan(1)
  expect(api.orderListReads).toBeGreaterThan(1)
})

test('Issue26 validation and generic return failures preserve entered fields and never auto-retry', async ({ page }) => {
  const api = await stubMemberOrder(page, { postStatus: 422 })
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()
  const form = order.getByRole('form', { name: 'Request a return' })
  await form.getByLabel('Reason').fill('Keep my entered reason')
  await form.getByRole('button', { name: 'Submit return request' }).click()

  await expect(order.getByRole('alert')).toContainText('check the return details')
  await expect(form.getByLabel('Reason')).toHaveValue('Keep my entered reason')
  expect(api.returnPosts).toBe(1)
})

test('Issue26 generic return failure preserves input and waits for an explicit retry', async ({ page }) => {
  const api = await stubMemberOrder(page, { postStatus: 503 })
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()
  const form = order.getByRole('form', { name: 'Request a return' })
  await form.getByLabel('Reason').fill('Retry only after review')
  await form.getByRole('button', { name: 'Submit return request' }).click()

  await expect(order.getByRole('alert')).toContainText('entries are saved')
  await expect(form.getByLabel('Reason')).toHaveValue('Retry only after review')
  expect(api.returnPosts).toBe(1)
})

test('Issue26 return form rejects quantities above the selected line and blank reasons locally', async ({ page }) => {
  const api = await stubMemberOrder(page)
  await page.goto('/zh-TW/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: '查看訂單明細' }).click()
  const form = order.getByRole('form', { name: '申請退貨' })
  await form.getByLabel('數量').fill('3')
  await form.getByLabel('退貨原因').fill('理由')
  await form.getByRole('button', { name: '送出退貨申請' }).click()
  await expect(form.getByRole('alert')).toContainText('不超過剩餘可退件數')
  await expect(form.getByLabel('數量')).toHaveAttribute('max', '2')

  await form.getByLabel('數量').fill('1')
  await form.getByLabel('退貨原因').fill('   ')
  await form.getByRole('button', { name: '送出退貨申請' }).click()
  await expect(form.getByRole('alert')).toContainText('並填寫原因')
  expect(api.returnPosts).toBe(0)
})

test('Issue26 does not show return request unless the backend allows it', async ({ page }) => {
  await stubMemberOrder(page, { actionAllowed: false })
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()
  await expect(order.getByRole('form', { name: 'Request a return' })).toHaveCount(0)
})

test('Issue26 return form hides exhausted lines and caps quantity to remaining free units', async ({ page }) => {
  const api = await stubMemberOrder(page, { lines: [
    { orderLineId: 'line-exhausted', skuId: 'sku_exhausted', name: { value: 'Already returned item' }, colorId: 'black', garmentSize: 'S', quantity: 2, returnableQuantity: 0, unitPriceTwd: 500, paidTwd: 1000 },
    { orderLineId: 'line-partial', skuId: 'sku_partial', name: { value: 'Partially returnable item' }, colorId: 'blue', garmentSize: 'M', quantity: 3, returnableQuantity: 1, unitPriceTwd: 600, paidTwd: 1800 },
  ] })
  await page.goto('/en/account')
  const order = page.getByRole('group', { name: 'issue26_return_order' })
  await order.getByRole('button', { name: 'View order details' }).click()
  const form = order.getByRole('form', { name: 'Request a return' })
  const item = form.getByLabel('Item')
  await expect(item.locator('option')).toHaveCount(1)
  await expect(item).toContainText('Partially returnable item')
  await expect(item).not.toContainText('Already returned item')
  const quantity = form.getByLabel('Quantity')
  await expect(quantity).toHaveAttribute('max', '1')
  await quantity.fill('2')
  await form.getByLabel('Reason').fill('Return remaining unit')
  await form.getByRole('button', { name: 'Submit return request' }).click()
  await expect(form.getByRole('alert')).toContainText('remaining return quantity')
  expect(api.returnPosts).toBe(0)

  await quantity.fill('1')
  await form.getByRole('button', { name: 'Submit return request' }).click()
  await expect(order.getByRole('form', { name: 'Request a return' })).toHaveCount(0)
  await expect.poll(() => api.returnPosts).toBe(1)
  expect(api.submitted?.body).toEqual({ orderLineId: 'line-partial', quantity: 1, reason: 'Return remaining unit' })
  expect(api.returnPosts).toBe(1)
})
