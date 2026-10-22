import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'

type CheckoutCase = { checkoutId: string; expectedState: 'Failed' | 'Unknown'; checkoutReadCookie: string }
type SessionArtifact = { sessionCookie: string; browserCookie: string; commandCookie: string }

test('Issue13 live payment result shows persisted outcomes and survives a service outage', async ({ page }) => {
  const cases = JSON.parse(process.env.KOHAMI_ISSUE13_CHECKOUTS || '[]') as CheckoutCase[]
  const artifactPath = process.env.KOHAMI_ISSUE13_SESSION_ARTIFACT
  expect(cases.map(item => item.expectedState)).toEqual(['Failed', 'Unknown'])
  expect(artifactPath).toBeTruthy()
  const session = JSON.parse(readFileSync(artifactPath!, 'utf8')) as SessionArtifact
  await page.context().addCookies([
    { name: 'kohami.session', value: session.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: session.browserCookie, url: 'https://localhost:3000' },
    { name: 'kohami.command', value: session.commandCookie, url: 'https://localhost:3000' },
  ])
  await page.goto('/')
  const browserSession = await page.evaluate(async () => {
    const response = await fetch('/api/v1/auth/session')
    const body = await response.json() as { state?: string }
    return { status: response.status, state: body.state }
  })
  expect(browserSession).toEqual({ status: 200, state: 'AdminAuthenticated' })
  for (const item of cases) {
    await page.context().addCookies([
      { name: 'kohami.checkout.read', value: item.checkoutReadCookie, url: 'https://localhost:3000' },
    ])
    const apiResult = await page.evaluate(async id => {
      const response = await fetch(`/api/v1/checkouts/${encodeURIComponent(id)}/status`)
      return { status: response.status, body: await response.json() as { checkoutId?: string; paymentState?: string } }
    }, item.checkoutId)
    expect(apiResult.status).toBe(200)
    expect(apiResult.body).toMatchObject({ checkoutId: item.checkoutId, paymentState: item.expectedState })
    await page.goto(`/checkout/${encodeURIComponent(item.checkoutId)}?lang=en`)
    await expect(page.getByText(item.checkoutId)).toBeVisible()
    const result = page.getByRole('heading', { name: 'Checkout result' }).locator('..')
    await expect(result.locator('dl dd').nth(5)).toHaveText(item.expectedState === 'Failed' ? 'Not approved' : 'Unconfirmed')
    if (item.expectedState === 'Failed') {
      await expect(result.getByRole('status')).toContainText('Payment was not approved')
    } else {
      await expect(result.getByRole('status')).toContainText('Do not start payment again')
      await page.route(`**/api/v1/checkouts/${encodeURIComponent(item.checkoutId)}/status`, route =>
        route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'SERVICE_UNAVAILABLE' }) }))
      await result.getByRole('button', { name: 'Refresh status' }).click()
      await expect(result.getByRole('alert')).toHaveText('The status could not be refreshed. Showing the last server result.')
      await expect(result.locator('dl dd').nth(5)).toHaveText('Unconfirmed')
      await page.unroute(`**/api/v1/checkouts/${encodeURIComponent(item.checkoutId)}/status`)
      await result.getByRole('button', { name: 'Refresh status' }).click()
      await expect(result.locator('dl dd').nth(5)).toHaveText('Unconfirmed')
    }
  }
})
