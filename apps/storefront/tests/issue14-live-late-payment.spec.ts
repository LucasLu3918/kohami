import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'

type LatePaymentCase = {
  checkoutId: string
  orderId: string
  checkoutReadCookie: string
}
type SessionArtifact = { sessionCookie: string; browserCookie: string; commandCookie: string }

test('Issue14 live late payment is visible to checkout owner and authorized support', async ({ page }) => {
  const item = JSON.parse(process.env.KOHAMI_ISSUE14_CHECKOUT || '{}') as LatePaymentCase
  const artifactPath = process.env.KOHAMI_ISSUE14_SESSION_ARTIFACT
  expect(item.checkoutId).toMatch(/^checkout_[A-Za-z0-9_-]+$/)
  expect(item.orderId).toMatch(/^order_[A-Za-z0-9_-]+$/)
  expect(artifactPath).toBeTruthy()
  const session = JSON.parse(readFileSync(artifactPath!, 'utf8')) as SessionArtifact
  await page.context().addCookies([
    { name: 'kohami.session', value: session.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: session.browserCookie, url: 'https://localhost:3000' },
    { name: 'kohami.command', value: session.commandCookie, url: 'https://localhost:3000' },
    { name: 'kohami.checkout.read', value: item.checkoutReadCookie, url: 'https://localhost:3000' },
  ])
  await page.goto(`/checkout/${encodeURIComponent(item.checkoutId)}?lang=en`)
  await expect(page.getByRole('heading', { name: 'Checkout result' })).toBeVisible()
  await expect(page.getByText('Expired', { exact: true })).toBeVisible()
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  await expect(page.getByRole('status')).toHaveText('The order remains cancelled or expired. A late payment result is being refunded; the order was not reopened.')

  const supportOrder = await page.evaluate(async id => {
    const response = await fetch(`/api/v1/orders/${encodeURIComponent(id)}`)
    return { status: response.status, body: await response.json() as {
      id?: string
      orderState?: string
      paymentSummary?: { state?: string; compensationRequired?: boolean; confirmedCollectedTwd?: number }
    } }
  }, item.orderId)
  expect(supportOrder.status).toBe(200)
  expect(supportOrder.body).toMatchObject({
    id: item.orderId,
    orderState: 'Expired',
    paymentSummary: { state: 'Succeeded', compensationRequired: true },
  })
  expect(supportOrder.body.paymentSummary?.confirmedCollectedTwd).toBeGreaterThan(0)
})
