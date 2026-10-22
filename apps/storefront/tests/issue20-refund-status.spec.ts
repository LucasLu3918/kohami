import { expect, test } from '@playwright/test'

test('Issue20 customer order shows server-confirmed refund totals and outcomes', async ({ page }) => {
  const refundSummary = {
    requestedCount: 1,
    processingCount: 1,
    unknownCount: 1,
    succeededCount: 1,
    failedCount: 1,
    succeededTwd: 613,
    pendingTwd: 977,
  }

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })

    if (path === '/auth/session') return json({ state: 'Anonymous', mode: 'mock' })
    if (path === '/products') return json({ items: [], nextCursor: null })
    if (path === '/carts/current') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === '/orders/issue20_refund_order') return json({
      id: 'issue20_refund_order',
      mode: 'mock',
      version: 'order-v2',
      placedAt: '2026-10-17T14:00:00Z',
      orderState: 'Confirmed',
      snapshot: { currency: 'TWD', merchandiseTwd: 1590, discountTwd: 0, shippingTwd: 60, totalTwd: 1650, lines: [] },
      paymentSummary: { state: 'Succeeded', confirmedCollectedTwd: 1650, compensationRequired: false },
      shipmentSummary: { state: 'Delivered', shipments: [] },
      refundSummary,
      receiptSummary: { state: 'NotCreated', pendingAdjustmentCount: 0 },
      maskedContact: '***@example.test',
      returns: [],
      actions: [],
    })
    if (path === '/orders/issue20_refund_order/receipts') return json({
      orderId: 'issue20_refund_order', taxEffect: 'none', receipts: [], adjustments: [],
    })

    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/en')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  const aftercare = page.getByRole('region', { name: 'Order delivery and aftercare (mock)' })
  await aftercare.getByLabel('Order ID').fill('issue20_refund_order')
  const orderResponse = page.waitForResponse(response => response.url().includes('/api/v1/orders/issue20_refund_order') && response.request().method() === 'GET')
  await aftercare.getByRole('button', { name: 'Load my order' }).click()
  expect((await orderResponse).status()).toBe(200)

  const refundStatus = aftercare.getByRole('region', { name: 'Refund status (mock)' })
  await expect(refundStatus).toContainText(/Requested\s*1/)
  await expect(refundStatus).toContainText(/Processing\s*1/)
  await expect(refundStatus).toContainText(/Awaiting confirmation\s*1/)
  await expect(refundStatus).toContainText(/Succeeded\s*1/)
  await expect(refundStatus).toContainText(/Failed\s*1/)
  await expect(refundStatus).toContainText(/Refunded\s*TWD 613/)
  await expect(refundStatus).toContainText(/Still reserved\s*TWD 977/)
  await expect(refundStatus).toContainText('Refund records are stored by the system; external refund processing is not connected.')
  await expect(refundStatus).not.toContainText('refund_')
})
