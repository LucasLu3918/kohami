import { expect, test } from '@playwright/test'

const orderId = 'order_issue18_synthetic'
const timestamp = (minutes: number) => new Date(Date.UTC(2026, 9, 17, 12, minutes)).toISOString()

const order = {
  id: orderId,
  mode: 'mock',
  version: 'order-v1',
  placedAt: timestamp(0),
  orderState: 'Confirmed',
  snapshot: { lines: [] },
  paymentSummary: { state: 'Succeeded', confirmedAt: timestamp(1), confirmedCollectedTwd: 100, compensationRequired: false },
  shipmentSummary: {
    state: 'Delivered',
    shipments: [{
      id: 'shipment_issue18_synthetic',
      mode: 'mock',
      state: 'Delivered',
      createdAt: timestamp(2),
      history: [
        { state: 'Pending', at: timestamp(2) },
        { state: 'Shipped', at: timestamp(3) },
        { state: 'InTransit', at: timestamp(4) },
        { state: 'Delivered', at: timestamp(5) },
      ],
      timing: { paymentToShipmentSeconds: 60, shipmentToShippedSeconds: 60, shippedToDeliveredSeconds: 120 },
    }],
  },
  refundSummary: {},
  receiptSummary: {},
  maskedContact: '***@example.test',
  returns: [],
  actions: [],
}

test('Issue18 customer sees localized UTC mock shipment milestones and elapsed durations', async ({ page }) => {
  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url())
    if (url.pathname.endsWith('/products')) return route.fulfill({ json: { items: [], nextCursor: null } })
    if (url.pathname.endsWith('/carts/current')) return route.fulfill({ json: { id: 'cart_issue18_synthetic', version: 'cart-v1', items: [] } })
    if (url.pathname.endsWith(`/orders/${orderId}/receipts`)) return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
    if (url.pathname.endsWith(`/orders/${orderId}`)) return route.fulfill({ json: order })
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  await page.goto('/en')
  await expect(page.getByRole('heading', { name: 'Order delivery and aftercare (mock)' })).toBeVisible()
  await page.waitForFunction(() => Boolean((document.getElementById('__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  await page.getByLabel('Order ID').fill(orderId)
  const orderResponse = page.waitForResponse(response => response.request().method() === 'GET' && response.url().includes(`/api/v1/orders/${orderId}`) && !response.url().endsWith('/receipts'))
  await page.getByRole('button', { name: 'Load my order' }).click()
  expect((await orderResponse).status()).toBe(200)
  await expect(page.getByText(orderId).first()).toBeVisible()

  const timeline = page.locator('.shipment-timeline')
  await expect(timeline).toContainText('Shipment timeline')
  await expect(timeline).toContainText('Mock shipment data. There is no real parcel or carrier tracking.')
  await expect(timeline).toContainText('In transit')
  await expect(timeline).toContainText('Delivered')
  await expect(timeline).toContainText('120 seconds')
  await expect(timeline.locator('time').first()).toHaveAttribute('datetime', timestamp(2))
  await expect(timeline).not.toContainText('actor')
  await expect(timeline).not.toContainText('eventId')
})
