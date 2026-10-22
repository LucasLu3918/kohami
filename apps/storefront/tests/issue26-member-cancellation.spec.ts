import { expect, test } from '@playwright/test'

const orderId = 'issue26_cancel_order'
const orderVersion = 'order-v7'

function order(state = 'AwaitingPayment', version = orderVersion) {
  return {
    id: orderId, mode: 'mock', version, placedAt: '2026-10-21T12:00:00Z', orderState: state,
    snapshot: { currency: 'TWD', totalTwd: 1650, merchandiseTwd: 1590, discountTwd: 0, shippingTwd: 60, lines: [{ skuId: 'sku_demo_shirt', name: { value: 'Demo Shirt' }, colorId: 'navy', garmentSize: 'M', quantity: 2, unitPriceTwd: 795, paidTwd: 1590 }] },
  }
}

function detail(state = 'AwaitingPayment', version = orderVersion, canCancel = true) {
  return {
    ...order(state, version),
    paymentSummary: { state: 'Created', confirmedAt: null, confirmedCollectedTwd: 0, compensationRequired: false },
    shipmentSummary: { state: 'NotCreated', shipments: [] },
    refundSummary: { requestedCount: 0, processingCount: 0, unknownCount: 0, succeededCount: 0, failedCount: 0, succeededTwd: 0, pendingTwd: 0 },
    receiptSummary: { state: 'None', pendingAdjustmentCount: 0 },
    maskedContact: 'm***@example.test', returns: [],
    actions: [{ action: 'Cancel', allowed: canCancel, reasonCodes: canCancel ? [] : ['INVALID_TRANSITION'] }],
  }
}

async function installMemberApi(page: import('@playwright/test').Page, options: {
  cancel?: (request: import('@playwright/test').Request) => Promise<{ status: number; body: unknown }>
  detail?: (readCount: number) => unknown
  cancelDelayMs?: number
} = {}) {
  let orderState = 'AwaitingPayment'
  let detailReads = 0
  const cancelRequests: import('@playwright/test').Request[] = []
  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url())
    const path = url.pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    if (path === '/auth/session') return json({ state: 'MemberAuthenticated', mode: 'mock', email: 'member@example.test' })
    if (path === '/products') return json({ items: [] })
    if (path === '/carts/current') return json({ id: 'cart', version: 'cart-v1', items: [] })
    if (path === '/wallet/current') return json({ mode: 'mock', currency: 'TWD', availableTwd: 0, version: 1, entries: [], nextCursor: null })
    if (path === '/auth/csrf') return json({ requestToken: 'synthetic-csrf-token-issue26' })
    if (path === '/orders') return json({ items: [order(orderState, orderState === 'Cancelled' ? 'order-v8' : orderVersion)] })
    if (path === `/orders/${orderId}`) {
      detailReads += 1
      return json(options.detail ? options.detail(detailReads) : detail(orderState, orderState === 'Cancelled' ? 'order-v8' : orderVersion, orderState === 'AwaitingPayment'))
    }
    if (path === `/orders/${orderId}/receipts`) return json({ orderId, mode: 'mock', taxEffect: 'none', receipts: [], adjustments: [] })
    if (path === `/orders/${orderId}/cancellations` && route.request().method() === 'POST') {
      cancelRequests.push(route.request())
      if (options.cancelDelayMs) await new Promise(resolve => setTimeout(resolve, options.cancelDelayMs))
      const result = options.cancel ? await options.cancel(route.request()) : { status: 202, body: { id: orderId, operationId: 'cancel-op', mode: 'mock', state: 'Cancelled', version: 'order-v8', reasonCodes: [] } }
      if (result.status === 202) orderState = 'Cancelled'
      if (result.status === 409 || result.status === 412) orderState = 'Confirmed'
      return json(result.body, result.status)
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })
  return { cancelRequests, getDetailReads: () => detailReads }
}

test('Issue26 member cancels eligible awaiting-payment order with contract headers and refreshes state', async ({ page }) => {
  const api = await installMemberApi(page, { cancelDelayMs: 250 })
  await page.goto('/en/account')
  const orderCard = page.getByRole('group', { name: orderId })
  await orderCard.getByRole('button', { name: 'View order details' }).click()
  const cancel = orderCard.getByRole('button', { name: 'Cancel order' })
  await expect(cancel).toBeVisible()
  await cancel.click()
  await expect(orderCard.getByRole('status')).toContainText('Cancelling order…')
  await expect(orderCard).toContainText('Cancelled')
  await expect(orderCard.getByRole('button', { name: 'Cancel order' })).toHaveCount(0)
  expect(api.cancelRequests).toHaveLength(1)
  const request = api.cancelRequests[0]!
  expect(request.postDataJSON()).toEqual({ expectedVersion: orderVersion })
  expect(request.headers()['x-csrf-token']).toBe('synthetic-csrf-token-issue26')
  expect(request.headers()['if-match']).toBe(`"${orderVersion}"`)
  expect(request.headers()['idempotency-key']).toMatch(/^storefront-cancel-/)
  expect(request.headers()['idempotency-key']!.length).toBeLessThanOrEqual(128)
  expect(request.headers().origin).toBe(new URL(request.url()).origin)
  expect(api.getDetailReads()).toBeGreaterThanOrEqual(2)
})

test('Issue26 cancellation is hidden when backend actions deny it', async ({ page }) => {
  await installMemberApi(page, { detail: () => detail('AwaitingPayment', orderVersion, false) })
  await page.goto('/en/account')
  const orderCard = page.getByRole('group', { name: orderId })
  await orderCard.getByRole('button', { name: 'View order details' }).click()
  await expect(orderCard.getByRole('button', { name: 'Cancel order' })).toHaveCount(0)
})

for (const status of [409, 412]) {
  test(`Issue26 stale cancellation ${status} refreshes state without retry`, async ({ page }) => {
    const api = await installMemberApi(page, {
      cancel: async () => ({ status, body: { code: status === 409 ? 'INVALID_TRANSITION' : 'VERSION_CONFLICT' } }),
      detail: (reads) => reads > 1 ? detail('Confirmed', 'order-v8', false) : detail(),
    })
    await page.goto('/en/account')
    const orderCard = page.getByRole('group', { name: orderId })
    await orderCard.getByRole('button', { name: 'View order details' }).click()
    await orderCard.getByRole('button', { name: 'Cancel order' }).click()
    await expect(orderCard).toContainText('Confirmed')
    await expect(orderCard).toContainText('could not be cancelled')
    await expect(orderCard.getByRole('button', { name: 'Cancel order' })).toHaveCount(0)
    expect(api.cancelRequests).toHaveLength(1)
    expect(api.getDetailReads()).toBeGreaterThanOrEqual(2)
  })
}

test('Issue26 generic cancellation failure is accessible and does not expose response details', async ({ page }) => {
  const api = await installMemberApi(page, { cancel: async () => ({ status: 503, body: { code: 'SENSITIVE_INTERNAL_DETAIL' } }) })
  await page.goto('/en/account')
  const orderCard = page.getByRole('group', { name: orderId })
  await orderCard.getByRole('button', { name: 'View order details' }).click()
  await orderCard.getByRole('button', { name: 'Cancel order' }).click()
  await expect(orderCard.getByRole('alert')).toContainText('could not be cancelled')
  await expect(orderCard).not.toContainText('SENSITIVE_INTERNAL_DETAIL')
  expect(api.cancelRequests).toHaveLength(1)
})
