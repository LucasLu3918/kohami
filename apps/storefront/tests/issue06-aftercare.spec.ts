import { expect, test } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

type SessionArtifact = {
  sessionCookie: string
  browserCookie: string
  commandCookie: string
  guestReadCookie?: string
  guestActionCookie?: string
}

test('Issue06 browser UI aftercare uses a synthetic authenticated Owner session', async ({ page }) => {
  const orderId = process.env.KOHAMI_ISSUE06_ORDER_ID
  const artifactPath = process.env.KOHAMI_ISSUE06_SESSION_ARTIFACT
  const sourceConfig = process.env.KOHAMI_ISSUE06_SOURCE_CONFIG
  const commerceDll = process.env.KOHAMI_ISSUE06_COMMERCE_DLL
  const dotnet = process.env.KOHAMI_ISSUE06_DOTNET
  if (!orderId || !artifactPath || !sourceConfig || !commerceDll || !dotnet) {
    throw new Error('ISSUE06_BROWSER_ENVIRONMENT_INCOMPLETE')
  }
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as SessionArtifact
  if (!artifact.guestReadCookie || !artifact.guestActionCookie) throw new Error('ISSUE06_GUEST_CAPABILITY_MISSING')
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
    { name: 'kohami.command', value: artifact.commandCookie, url: 'https://localhost:3000' },
    { name: 'kohami.guest.read', value: artifact.guestReadCookie, url: 'https://localhost:3000' },
    { name: 'kohami.guest.action', value: artifact.guestActionCookie, url: 'https://localhost:3000' },
  ])

  page.on('response', response => {
    const path = new URL(response.url()).pathname
    if (path.includes('/api/v1/orders/') && response.status() >= 400) {
      console.log(`Issue06 browser order request failed: HTTP ${response.status()} ${path}`)
    }
  })
  await page.goto('/')
  await expect(page.getByText('全流程模擬，沒有實際付款、退款、配送或有效發票。')).toBeVisible()
  const session = await page.evaluate(async () => {
    const response = await fetch('/api/v1/auth/session')
    const body = await response.json() as { state?: string }
    return { status: response.status, state: body.state }
  })
  expect(session).toEqual({ status: 200, state: 'AdminAuthenticated' })
  const aftercare = page.getByRole('region', { name: '訂單配送與售後（模擬）' })
  await aftercare.getByLabel('訂單編號').fill(orderId)
  await aftercare.getByRole('button', { name: '查詢自己的訂單' }).click()
  await expect(aftercare.getByText(new RegExp(orderId))).toBeVisible()
  await expect.poll(async () => page.evaluate(async id => {
    const response = await fetch(`/api/v1/orders/${encodeURIComponent(id)}/receipts`)
    if (!response.ok) return `HTTP_${response.status}`
    const body = await response.json() as { receipts?: Array<unknown> }
    return body.receipts?.length || 0
  }, orderId), { timeout: 30_000 }).toBeGreaterThan(0)
  await aftercare.getByRole('button', { name: '查詢自己的訂單' }).click()
  await expect(aftercare.getByText(/DEMO 憑證僅供展示/)).toBeVisible()
  await expect(aftercare.getByText(/無稅務效果/)).toBeVisible()

  const order = await page.evaluate(async id => fetch(`/api/v1/orders/${encodeURIComponent(id)}`).then(response => response.json()), orderId) as {
    snapshot: { lines: Array<{ orderLineId: string }> }
  }
  const orderLineId = order.snapshot.lines[0]?.orderLineId
  expect(orderLineId).toBeTruthy()

  const shipmentResponse = page.waitForResponse(response => response.url().includes(`/api/v1/admin/orders/${orderId}/shipments`) && response.request().method() === 'POST')
  await aftercare.getByRole('button', { name: '建立模擬配送' }).click()
  const createdShipment = await shipmentResponse
  expect(createdShipment.status(), `shipment creation returned HTTP ${createdShipment.status()}`).toBe(201)
  const shipment = await createdShipment.json() as { id: string }
  const resolvedShipmentId = shipment.id
  await aftercare.getByLabel('配送編號').fill(resolvedShipmentId)
  const state = aftercare.getByLabel('下一狀態')
  for (const next of ['Shipped', 'InTransit', 'Delivered']) {
    await state.selectOption(next)
    const transitionResponse = page.waitForResponse(response => response.url().includes('/api/v1/admin/shipments/') && response.url().endsWith('/transitions') && response.request().method() === 'POST')
    await aftercare.getByRole('button', { name: '更新配送狀態' }).click()
    const response = await transitionResponse
    expect(response.status(), `${next} shipment transition returned HTTP ${response.status()}`).toBe(200)
  }
  await expect(aftercare.getByRole('heading', { name: new RegExp(`已送達 · ${resolvedShipmentId}`) })).toBeVisible()

  await aftercare.getByLabel('訂單明細編號').fill(orderLineId!)
  await aftercare.getByLabel('原因').first().fill('Synthetic Issue06 browser return')
  const returnResponse = page.waitForResponse(response => response.url().includes(`/api/v1/orders/${orderId}/returns`) && response.request().method() === 'POST')
  await aftercare.getByRole('button', { name: '提出退貨申請' }).click()
  const returnCase = await (await returnResponse).json() as { id: string }
  await aftercare.getByLabel('退貨編號').fill(returnCase.id)
  await aftercare.getByLabel('原因').last().fill('Synthetic escalation reason kept internal')
  const escalationResponse = page.waitForResponse(response => response.url().includes(`/api/v1/admin/returns/${returnCase.id}/transitions`) && response.request().method() === 'POST')
  await aftercare.getByRole('button', { name: '已升級處理' }).click()
  expect((await escalationResponse).status()).toBe(200)
  const returnTimeline = aftercare.locator('.return-support-timeline')
  await expect(returnTimeline.getByText('案件分類: Return')).toBeVisible()
  await expect(returnTimeline.getByRole('heading', { name: /已升級處理/ })).toBeVisible()
  await expect(returnTimeline).not.toContainText('Synthetic escalation reason kept internal')
  await expect(returnTimeline.locator('dl')).toContainText('秒')
  await aftercare.getByRole('button', { name: '受理退貨' }).click()
  await expect(returnTimeline.getByRole('heading', { name: /已受理/ })).toBeVisible()
  await aftercare.getByRole('button', { name: '已收件' }).click()
  await aftercare.getByLabel('可再售數量').fill('1')
  await aftercare.getByLabel('原因').last().fill('Synthetic item inspected in browser')
  await aftercare.getByRole('button', { name: '完成驗收' }).click()
  await expect(aftercare.getByText(new RegExp(`${returnCase.id}: 已完成驗收`))).toBeVisible()
  await expect(returnTimeline.getByText(/驗收結果: 可再售 1 · 隔離 0/)).toBeVisible()

  const refundResponse = page.waitForResponse(response => response.url().includes('/api/v1/admin/refund-cases/') && response.url().endsWith('/operations') && response.request().method() === 'POST')
  await aftercare.getByRole('button', { name: '執行模擬退款' }).click()
  const refund = await (await refundResponse).json() as { id: string; amountTwd: number; shippingTwd: number; mode: string; state: string }
  expect(refund.amountTwd).toBeGreaterThan(0)
  expect(refund.shippingTwd).toBe(0)
  expect(refund.mode).toBe('mock')
  expect(['Processing', 'Unknown']).toContain(refund.state)
  expect(refund.id).toMatch(/^refund_/)

  const refundStatus = aftercare.locator('.refund-status-timeline')
  await expect(refundStatus).toContainText(refund.state === 'Unknown' ? '結果待確認' : '處理中')
  await expect(refundStatus).toContainText(`TWD ${new Intl.NumberFormat('zh-TW', { maximumFractionDigits: 0 }).format(refund.amountTwd)}`)
  await expect(refundStatus).toContainText('模擬退款不會移動真實資金。')
  await expect(refundStatus).not.toContainText(refund.id)

  execFileSync(dotnet, [commerceDll, 'issue06-owner-verify', sourceConfig, artifactPath, orderId, refund.id], { stdio: 'inherit' })
  await expect(page.getByRole('status')).toContainText(refund.id)
})
