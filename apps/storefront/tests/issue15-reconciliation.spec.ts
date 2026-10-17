import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'

type SessionArtifact = {
  userId: string
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

  const workbench = page.locator('.reconciliation-workbench')
  await expect(workbench).toBeVisible()
  await expect(workbench).toContainText('Daily schedule is disabled')
  await expect(workbench).toContainText('DifferencesFound')
  await expect(workbench).toContainText('NON_PRIMARY_COLLECTION_REQUIRES_REVIEW')
  await expect(workbench).toContainText(/compensationRequiredOrderCount\s*[1-9]\d*/)
  await expect(workbench).not.toContainText('@example.test')

  const latePaymentCase = page.locator('.reconciliation-case').filter({ hasText: 'NON_PRIMARY_COLLECTION_REQUIRES_REVIEW' }).first()
  await latePaymentCase.getByLabel('Assignee account email').fill(`${session.userId}@example.test`)
  await latePaymentCase.getByLabel('Assignment reason').fill('Verify assigned-owner in-app notification')
  const assignmentResponsePromise = page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/reconciliation-cases/') && response.url().endsWith('/assignments'))
  const notificationResponsePromise = page.waitForResponse(response => response.request().method() === 'GET' && response.url().endsWith('/reconciliation-notifications'))
  await latePaymentCase.getByRole('button', { name: 'Assign case' }).click()
  const assignmentResponse = await assignmentResponsePromise
  expect(assignmentResponse.status(), await assignmentResponse.text()).toBe(200)
  const notificationResponse = await notificationResponsePromise
  expect(notificationResponse.status()).toBe(200)
  const notificationPayload = await notificationResponse.json() as { items: Array<{ findingCode: string }> }
  expect(notificationPayload.items.map(item => item.findingCode)).toContain('NON_PRIMARY_COLLECTION_REQUIRES_REVIEW')
  await expect(page.getByRole('heading', { name: 'Major exception notifications' })).toBeVisible()
  await expect(page.getByText(/Major exception assigned: NON_PRIMARY_COLLECTION_REQUIRES_REVIEW/)).toBeVisible()
})
