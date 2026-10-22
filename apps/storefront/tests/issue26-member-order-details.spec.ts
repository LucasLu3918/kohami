import { expect, test } from '@playwright/test'

test('Issue26 member can expand an order to read aftercare details and receipts', async ({ page }) => {
  const requested: string[] = []
  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url())
    const path = url.pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    if (path === '/auth/session') return json({ state: 'MemberAuthenticated', mode: 'mock', email: 'member@example.test' })
    if (path === '/products') return json({ items: [] })
    if (path === '/carts/current') return json({ id: 'cart', version: 'cart-v1', items: [] })
    if (path === '/wallet/current') return json({ mode: 'mock', currency: 'TWD', availableTwd: 0, version: 1, entries: [], nextCursor: null })
    if (path === '/orders') return json({ items: [{
      id: 'issue26_member_order', mode: 'mock', version: 'order-v1', placedAt: '2026-10-21T12:00:00Z', orderState: 'Confirmed',
      snapshot: { currency: 'TWD', totalTwd: 1650, merchandiseTwd: 1590, discountTwd: 0, shippingTwd: 60, lines: [{ skuId: 'sku_demo_shirt', productSlug: 'shirt', name: { value: 'Demo Shirt' }, colorId: 'navy', garmentSize: 'M', quantity: 2, unitPriceTwd: 795, paidTwd: 1590 }] },
    }] })
    if (path === '/orders/issue26_member_order') {
      requested.push(path)
      return json({
        id: 'issue26_member_order', mode: 'mock', version: 'order-v1', placedAt: '2026-10-21T12:00:00Z', orderState: 'Confirmed',
        snapshot: { currency: 'TWD', totalTwd: 1650, merchandiseTwd: 1590, discountTwd: 0, shippingTwd: 60, lines: [{ skuId: 'sku_demo_shirt', name: { value: 'Demo Shirt' }, colorId: 'navy', garmentSize: 'M', quantity: 2, unitPriceTwd: 795, paidTwd: 1590 }] },
        paymentSummary: { state: 'Succeeded', confirmedAt: '2026-10-21T12:01:00Z', confirmedCollectedTwd: 1650, compensationRequired: false },
        shipmentSummary: { state: 'Delivered', shipments: [{ id: 'shipment_demo', state: 'Delivered', createdAt: '2026-10-21T12:02:00Z', history: [{ state: 'Pending', at: '2026-10-21T12:02:00Z' }, { state: 'Delivered', at: '2026-10-22T12:02:00Z' }], timing: { paymentToShipmentSeconds: 60, shipmentToShippedSeconds: 120, shippedToDeliveredSeconds: 86400 } }] },
        refundSummary: { requestedCount: 1, processingCount: 0, unknownCount: 0, succeededCount: 0, failedCount: 0, succeededTwd: 0, pendingTwd: 795 },
        receiptSummary: { state: 'Confirmed', pendingAdjustmentCount: 0 }, maskedContact: 'm***@example.test',
        returns: [{ id: 'return_demo', state: 'Requested', caseSummary: { classification: 'Return', openedAt: '2026-10-22T12:00:00Z', firstResponseAt: null, firstResponseElapsedSeconds: null, escalatedAt: null, escalationElapsedSeconds: null }, timeline: [{ state: 'Requested', at: '2026-10-22T12:00:00Z' }], inspectionSummary: null }], actions: [],
      })
    }
    if (path === '/orders/issue26_member_order/receipts') {
      requested.push(path)
      return json({ orderId: 'issue26_member_order', mode: 'mock', taxEffect: 'none', receipts: [{ id: 'receipt_demo', state: 'Issued', amountTwd: 1650, displayNotice: 'Demo receipt; no tax effect.', createdAt: '2026-10-21T12:01:00Z' }], adjustments: [] })
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/en/account')
  const order = page.getByRole('group', { name: /issue26_member_order/ })
  await expect(order).toBeVisible()
  await expect(order).not.toContainText('Payment status')
  await order.getByRole('button', { name: 'View order details' }).click()
  await expect(order).toContainText('Payment status')
  await expect(order).toContainText('Succeeded')
  await expect(order).toContainText('Delivered')
  await expect(order).toContainText('Return case timeline')
  await expect(order).toContainText('Refund status (mock)')
  await expect(order).toContainText('Demo receipt; no tax effect.')
  expect(requested).toEqual(['/orders/issue26_member_order', '/orders/issue26_member_order/receipts'])
  await expect(order.getByRole('button', { name: 'Hide order details' })).toHaveAttribute('aria-expanded', 'true')
})
