import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'

type SessionArtifact = {
  sessionCookie: string
  browserCookie: string
  commandCookie: string
  guestReadCookie: string
}

test('Issue15 manual reconciliation report shows late payment findings without customer data', async ({ page }) => {
  const orderId = process.env.KOHAMI_ISSUE15_ORDER_ID || ''
  const artifactPath = process.env.KOHAMI_ISSUE15_SESSION_ARTIFACT
  expect(orderId).toMatch(/^order_[A-Za-z0-9_-]+$/)
  expect(artifactPath).toBeTruthy()
  const session = JSON.parse(readFileSync(artifactPath!, 'utf8')) as SessionArtifact

  await page.context().addCookies([
    { name: 'kohami.session', value: session.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: session.browserCookie, url: 'https://localhost:3000' },
    { name: 'kohami.command', value: session.commandCookie, url: 'https://localhost:3000' },
    { name: 'kohami.guest.read', value: session.guestReadCookie, url: 'https://localhost:3000' },
  ])
  await page.goto('/?lang=en')
  await expect(page.getByRole('heading', { name: 'Order delivery and aftercare (mock)' })).toBeVisible()
  await page.getByLabel('Order ID').fill(orderId)
  await page.getByRole('button', { name: 'Load my order' }).click()
  const reconciliationButton = page.getByRole('button', { name: 'Show read-only mock reconciliation' })
  await expect(reconciliationButton).toBeVisible()
  await reconciliationButton.click()

  const report = page.locator('pre').first()
  await expect(report).toContainText('"scheduleStatus": "ManualOnly"')
  await expect(report).toContainText('"state": "DifferencesFound"')
  await expect(report).toContainText('NON_PRIMARY_COLLECTION_REQUIRES_REVIEW')
  await expect(report).toContainText(/"compensationRequiredOrderCount": [1-9]\d*/)
  await expect(report).not.toContainText('@example.test')
})
