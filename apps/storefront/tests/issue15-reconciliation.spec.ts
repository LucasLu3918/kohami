import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { readFileSync } from 'node:fs'

type SessionArtifact = {
  userId: string
  sessionCookie: string
  browserCookie: string
  commandCookie: string
  guestReadCookie: string
}

async function openIssue15Workbench(page: Page) {
  const orderId = process.env.KOHAMI_ISSUE15_ORDER_ID || ''
  const sourceId = process.env.KOHAMI_ISSUE15_SOURCE_ID || ''
  const artifactPath = process.env.KOHAMI_ISSUE15_SESSION_ARTIFACT
  expect(orderId).toMatch(/^order_[A-Za-z0-9_-]+$/)
  expect(sourceId).toMatch(/^collection_issue15_browser_[0-9a-f]{32}$/)
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
  return { session, sourceId, workbench }
}

test('Issue15 manual reconciliation queues original check through Storefront', async ({ page }) => {
  const { session, sourceId, workbench } = await openIssue15Workbench(page)
  const latePaymentCase = page.locator('.reconciliation-case').filter({ hasText: sourceId }).first()
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

  await latePaymentCase.getByLabel('Action').selectOption('QueueOriginalCheck')
  await latePaymentCase.getByLabel('Reason', { exact: true }).fill('Reviewed the source ledger discrepancy')
  await latePaymentCase.getByLabel('Evidence reference ID').fill(`issue15_check_${session.userId.slice(-12)}`)
  const resolutionResponsePromise = page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/reconciliation-cases/') && response.url().endsWith('/resolutions'))
  await latePaymentCase.getByRole('button', { name: 'Submit resolution' }).click()
  const resolutionResponse = await resolutionResponsePromise
  expect(resolutionResponse.status(), await resolutionResponse.text()).toBe(200)
  await expect(workbench.locator('.reconciliation-case').filter({ hasText: 'NON_PRIMARY_COLLECTION_REQUIRES_REVIEW' }).first()).toContainText('CheckRequested')
})

test('Issue15 manual reconciliation closes cleared finding through Storefront', async ({ page }) => {
  const { sourceId } = await openIssue15Workbench(page)
  const ready = page.locator('.reconciliation-workbench').getByRole('heading', { name: 'Finding cleared; close after reviewing the evidence' }).locator('..')
  await expect(ready).toBeVisible()
  const caseCard = ready.locator('.reconciliation-case').filter({ hasText: sourceId }).first()
  await expect(caseCard).toContainText('CheckRequested')
  await caseCard.getByLabel('Reason', { exact: true }).fill('Source ledger correction reviewed with supporting evidence')
  await caseCard.getByLabel('Evidence reference ID').fill('issue15_close_verified')
  const resolutionResponsePromise = page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/reconciliation-cases/') && response.url().endsWith('/resolutions'))
  await caseCard.getByRole('button', { name: 'Submit resolution' }).click()
  const resolutionResponse = await resolutionResponsePromise
  expect(resolutionResponse.status(), await resolutionResponse.text()).toBe(200)
  await expect(page.locator('.reconciliation-workbench .reconciliation-case').filter({ hasText: sourceId })).toHaveCount(0)
})
