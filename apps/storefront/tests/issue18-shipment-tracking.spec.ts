import { expect, test } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'

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
  await expect(page.getByRole('heading', { name: 'Order delivery and aftercare' })).toBeVisible()
  await page.waitForFunction(() => Boolean((document.getElementById('__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  await page.getByLabel('Order ID').fill(orderId)
  const orderResponse = page.waitForResponse(response => response.request().method() === 'GET' && response.url().includes(`/api/v1/orders/${orderId}`) && !response.url().endsWith('/receipts'))
  await page.getByRole('button', { name: 'Load my order' }).click()
  expect((await orderResponse).status()).toBe(200)
  await expect(page.getByText(orderId).first()).toBeVisible()

  const timeline = page.locator('.shipment-timeline')
  await expect(timeline).toContainText('Shipment timeline')
  await expect(timeline).toContainText('Status is saved by the system. A carrier is not connected, so parcels cannot be tracked.')
  await expect(timeline).toContainText('In transit')
  await expect(timeline).toContainText('Delivered')
  await expect(timeline).toContainText('120 seconds')
  await expect(timeline.locator('time').first()).toHaveAttribute('datetime', timestamp(2))
  await expect(timeline).not.toContainText('actor')
  await expect(timeline).not.toContainText('eventId')
})

test('Issue18 live Owner shipment journey persists SQL and renders the guest tracking timeline', async ({ page }) => {
  const orderId = process.env.KOHAMI_ISSUE18_ORDER_ID
  const artifactPath = process.env.KOHAMI_ISSUE18_SESSION_ARTIFACT
  const sourceConfig = process.env.KOHAMI_ISSUE18_SOURCE_CONFIG
  const commerceDll = process.env.KOHAMI_ISSUE18_COMMERCE_DLL
  const dotnet = process.env.KOHAMI_ISSUE18_DOTNET
  if (!orderId || !artifactPath || !sourceConfig || !commerceDll || !dotnet) {
    throw new Error('ISSUE18_LIVE_BROWSER_ENVIRONMENT_INCOMPLETE')
  }
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as {
    sessionCookie: string
    browserCookie: string
    commandCookie: string
    guestReadCookie?: string
  }
  if (!artifact.guestReadCookie) throw new Error('ISSUE18_GUEST_READ_CAPABILITY_MISSING')
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
    { name: 'kohami.command', value: artifact.commandCookie, url: 'https://localhost:3000' },
    { name: 'kohami.guest.read', value: artifact.guestReadCookie, url: 'https://localhost:3000' },
  ])

  await page.goto('/en')
  await page.waitForFunction(() => Boolean((document.getElementById('__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  const ownerSession = await page.evaluate(async () => {
    const response = await fetch('/api/v1/auth/session')
    const body = await response.json() as { state?: string }
    return { status: response.status, state: body.state }
  })
  expect(ownerSession).toEqual({ status: 200, state: 'AdminAuthenticated' })
  const ownerPanel = page.getByRole('region', { name: 'Order delivery and aftercare' })
  await ownerPanel.getByLabel('Order ID').fill(orderId)
  const ownerOrderResponse = page.waitForResponse(response => response.request().method() === 'GET'
    && response.url().includes(`/api/v1/orders/${orderId}`) && !response.url().endsWith('/receipts'))
  await ownerPanel.getByRole('button', { name: 'Load my order' }).click()
  const ownerOrderResult = await ownerOrderResponse
  const ownerOrderPayload = await ownerOrderResult.json() as { code?: string }
  expect(ownerOrderResult.status(), `owner order read HTTP ${ownerOrderResult.status()} code ${ownerOrderPayload.code || 'NONE'}`).toBe(200)

  const shipmentResponse = page.waitForResponse(response => response.url().includes(`/api/v1/admin/orders/${orderId}/shipments`)
    && response.request().method() === 'POST')
  await ownerPanel.getByRole('button', { name: 'Create mock shipment' }).click()
  const created = await shipmentResponse
  expect(created.status()).toBe(201)
  const shipment = await created.json() as { id: string }
  await expect(ownerPanel.getByLabel('Shipment ID')).toHaveValue(shipment.id)

  for (const state of ['Shipped', 'InTransit', 'Delivered']) {
    await ownerPanel.getByLabel('Next state').selectOption(state)
    const transition = page.waitForResponse(response => response.url().includes(`/api/v1/admin/shipments/${shipment.id}/transitions`)
      && response.request().method() === 'POST')
    await ownerPanel.getByRole('button', { name: 'Update shipment state' }).click()
    expect((await transition).status()).toBe(200)
  }

  const customerPage = await page.context().newPage()
  await customerPage.context().clearCookies({ name: 'kohami.session' })
  await customerPage.context().clearCookies({ name: 'kohami.command' })
  await customerPage.goto('/en')
  await customerPage.waitForFunction(() => Boolean((document.getElementById('__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  const customerPanel = customerPage.getByRole('region', { name: 'Order delivery and aftercare' })
  await customerPanel.getByLabel('Order ID').fill(orderId)
  const customerOrderResponse = customerPage.waitForResponse(response => response.request().method() === 'GET'
    && response.url().includes(`/api/v1/orders/${orderId}`) && !response.url().endsWith('/receipts'))
  await customerPanel.getByRole('button', { name: 'Load my order' }).click()
  const response = await customerOrderResponse
  expect(response.status()).toBe(200)
  const orderResponse = await response.json() as {
    paymentSummary: { confirmedAt: string }
    shipmentSummary: { shipments: Array<{
      id: string
      state: string
      createdAt: string
      history: Array<Record<string, unknown> & { state: string; at: string }>
      timing: { paymentToShipmentSeconds: number; shipmentToShippedSeconds: number; shippedToDeliveredSeconds: number }
    }> }
  }
  const publicShipment = orderResponse.shipmentSummary.shipments.find(item => item.id === shipment.id)
  expect(orderResponse.paymentSummary.confirmedAt).toMatch(/(?:Z|\+00:00)$/)
  expect(publicShipment?.state).toBe('Delivered')
  expect(publicShipment?.createdAt).toMatch(/(?:Z|\+00:00)$/)
  expect(publicShipment?.history.map(item => item.state)).toEqual(['Pending', 'Shipped', 'InTransit', 'Delivered'])
  expect(publicShipment?.history.every(item => /(?:Z|\+00:00)$/.test(item.at)
    && !('actor' in item) && !('eventId' in item) && !('reason' in item))).toBe(true)
  expect(Object.values(publicShipment!.timing).every(seconds => seconds >= 0)).toBe(true)

  const timeline = customerPage.locator('.shipment-timeline')
  await expect(timeline).toContainText('Shipment timeline')
  await expect(timeline).toContainText('Status is saved by the system. A carrier is not connected, so parcels cannot be tracked.')
  await expect(timeline).toContainText('Delivered')
  await expect(timeline).toContainText(/\d+ seconds/)
  await expect(timeline.locator('time').first()).toHaveAttribute('datetime', publicShipment!.createdAt)
  await expect(timeline).not.toContainText('actor')
  await expect(timeline).not.toContainText('eventId')

  execFileSync(dotnet, [commerceDll, 'issue18-verify-shipment', sourceConfig, orderId, shipment.id], { stdio: 'inherit' })
})
