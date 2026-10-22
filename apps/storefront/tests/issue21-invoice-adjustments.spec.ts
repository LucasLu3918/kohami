import { expect, test } from '@playwright/test'

test('Issue21 customer order shows matching DEMO receipt and refund adjustment without tax effect', async ({ page }) => {
  const orderId = 'issue21_invoice_order'
  const receiptId = 'DEMO-issue21_collection'
  const adjustmentId = 'DEMO-ADJ-issue21_refund'
  const requestedPaths: string[] = []

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname.replace('/api/v1', '')
    requestedPaths.push(`${request.method()} ${path}`)
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })

    if (path === '/auth/session') return json({ state: 'Anonymous', mode: 'mock' })
    if (path === '/products') return json({ items: [], nextCursor: null })
    if (path === '/carts/current') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === `/orders/${orderId}`) return json({
      id: orderId,
      mode: 'mock',
      version: 'order-v1',
      placedAt: '2026-10-17T00:00:00Z',
      orderState: 'Confirmed',
      snapshot: { currency: 'TWD', totalTwd: 500, lines: [] },
      shipmentSummary: { state: 'NotCreated', shipments: [] },
      refundSummary: null,
      receiptSummary: { state: 'Confirmed', pendingAdjustmentCount: 0 },
      returns: [],
      actions: [],
    })
    if (path === `/orders/${orderId}/receipts`) return json({
      orderId,
      mode: 'mock',
      taxEffect: 'none',
      receipts: [{ id: receiptId, kind: 'Receipt', state: 'Issued', amountTwd: 500, displayNotice: 'System receipt record; no tax effect.' }],
      adjustments: [{ id: adjustmentId, kind: 'Adjustment', state: 'Adjusted', amountTwd: 500, displayNotice: 'System refund adjustment; no tax effect.' }],
    })

    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/en')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  const aftercare = page.getByRole('region', { name: 'Order delivery and aftercare (mock)' })
  await aftercare.getByLabel('Order ID').fill(orderId)
  await aftercare.getByRole('button', { name: 'Load my order' }).click()

  await expect(aftercare).toContainText('System receipt record; no tax effect.')
  await expect(aftercare).toContainText(`${receiptId} · Issued · TWD 500`)
  await expect(aftercare).toContainText(`${adjustmentId} · Adjusted · TWD 500`)
  expect(requestedPaths).toContain(`GET /orders/${orderId}/receipts`)
})
