import { expect, test } from '@playwright/test'
import net from 'node:net'
import { writeFileSync } from 'node:fs'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

type AcceptanceStage =
  | 'login_page' | 'login_session_response' | 'login_http_status' | 'login_challenge_present'
  | 'checkout' | 'payment' | 'scenario_authorization'
  | 'shipment' | 'return_inspection' | 'refund' | 'complete' | 'login_submit'
  | 'login_csrf_request' | 'login_csrf_response' | 'login_csrf_failed'
  | 'login_session_request' | 'login_session_failed'

function markStage(stage: AcceptanceStage): void {
  const path = process.env.KOHAMI_ISSUE02_STAGE_FILE
  if (!path) return
  try {
    writeFileSync(path, `${stage}\n`, { mode: 0o600 })
  } catch {
    // Diagnostics must never change the acceptance journey's behavior.
  }
}

function observeLoginTransport(page: import('@playwright/test').Page): void {
  page.on('request', request => {
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/auth/csrf') markStage('login_csrf_request')
    if (path === '/api/v1/auth/sessions') markStage('login_session_request')
  })
  page.on('response', response => {
    const path = new URL(response.url()).pathname
    if (path === '/api/v1/auth/csrf') markStage('login_csrf_response')
    if (path === '/api/v1/auth/sessions') markStage('login_session_response')
  })
  page.on('requestfailed', request => {
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/auth/csrf') markStage('login_csrf_failed')
    if (path === '/api/v1/auth/sessions') markStage('login_session_failed')
  })
}

async function readFixturePassword(): Promise<string> {
  const socketPath = process.env.KOHAMI_ACCEPTANCE_PRIVATE_VALUE_SOCKET
  if (!socketPath) throw new Error('PRIVATE_FIXTURE_VALUE_CHANNEL_MISSING')
  return await new Promise((resolve, reject) => {
    const socket = net.createConnection(socketPath)
    let response = ''
    let settled = false
    const fail = () => {
      if (settled) return
      settled = true
      socket.destroy()
      reject(new Error('PRIVATE_FIXTURE_VALUE_CHANNEL_FAILED'))
    }
    socket.setEncoding('utf8')
    socket.once('connect', () => socket.write(`${JSON.stringify({ name: 'issue02OwnerPassword' })}\n`))
    socket.on('data', chunk => {
      response += chunk
      if (response.length > 4096) return fail()
      const end = response.indexOf('\n')
      if (end < 0 || settled) return
      try {
        const message = JSON.parse(response.slice(0, end)) as { ok?: boolean; value?: string }
        if (!message.ok || typeof message.value !== 'string') return fail()
        settled = true
        socket.end()
        resolve(message.value)
      } catch {
        fail()
      }
    })
    socket.once('error', fail)
  })
}

async function postInPage(page: import('@playwright/test').Page, path: string, body: unknown, ifMatch?: string) {
  return await page.evaluate(async ({ path, body, ifMatch }) => {
    const csrf = await fetch('/api/v1/auth/csrf', { credentials: 'same-origin' }).then(r => r.json()) as { requestToken: string }
    const headers: Record<string, string> = { 'content-type': 'application/json', 'x-csrf-token': csrf.requestToken }
    if (ifMatch) headers['if-match'] = `"${ifMatch}"`
    headers['idempotency-key'] = crypto.randomUUID()
    const response = await fetch(`/api/v1${path}`, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) })
    return { status: response.status, body: await response.json().catch(() => null) }
  }, { path, body, ifMatch })
}

test('real API aftercare journey uses password login, SQL authorization and mock-only receipt policy', async ({ page }) => {
  markStage('login_page')
  observeLoginTransport(page)
  const account = page.getByRole('region', { name: '登入' })
  const forms = account.locator('form')
  await page.goto('/')
  await expect(page.getByText('商品與訂單資料會保存於資料庫，沒有實際付款、退款、配送或有效發票。')).toBeVisible()

  await account.getByLabel('測試 Email').fill('issue02-owner@example.test')
  let password = await readFixturePassword()
  await forms.nth(0).getByLabel('密碼').fill(password)
  password = ''
  const sessionResponsePromise = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/sessions') && response.request().method() === 'POST')
  markStage('login_submit')
  await forms.nth(0).getByRole('button', { name: '送出' }).click()
  const sessionResponse = await sessionResponsePromise
  markStage('login_http_status')
  expect(sessionResponse.status()).toBe(200)
  const identity = page.getByRole('region', { name: '目前身份' })
  await expect(identity_access.locator('pre')).toContainText('AdminAuthenticated')

  markStage('checkout')
  await page.getByRole('button', { name: '加入購物車' }).first().click()
  const cart = page.getByRole('region', { name: '購物車' })
  await cart.getByRole('button', { name: '重新試算' }).click()
  const quote = page.getByRole('region', { name: '結帳報價' })
  await expect(quote).toBeVisible()
  await quote.getByLabel('測試 Email').fill('issue02-buyer@example.test')
  await quote.getByRole('checkbox').check()
  const checkoutResponse = page.waitForResponse(response => response.url().endsWith('/checkouts') && response.request().method() === 'POST')
  await quote.getByRole('button', { name: '建立訂單並保留庫存' }).click()
  const order = await (await checkoutResponse).json() as { checkoutId: string; orderId: string }
  const checkout = page.getByRole('region', { name: '結帳' })
  await expect(checkout.getByText(/TWD 560/)).toBeVisible()

  markStage('payment')
  const paymentResponse = page.waitForResponse(response => response.url().includes('/payment-attempts') && response.request().method() === 'POST')
  await checkout.getByRole('button', { name: '啟動模擬付款' }).click()
  const payment = await (await paymentResponse).json() as { id: string; version: string }
  expect(payment.id).toMatch(/^pay_/)
  await expect.poll(async () => (await page.evaluate(async id => fetch(`/api/v1/checkouts/${id}/status`).then(r => r.json()), order.checkoutId)).paymentState).toMatch(/Processing|Unknown/)

  markStage('scenario_authorization')
  const scenario = await postInPage(page, '/admin/demo/scenarios', {
    targetKind: 'PaymentAttempt', targetId: payment.id, scenario: 'success',
    expectedVersion: payment.version,
  }, payment.version)
  expect(scenario.status).toBe(202)
  await expect.poll(async () => (await page.evaluate(async id => fetch(`/api/v1/checkouts/${id}/status`).then(r => r.json()), order.checkoutId)).orderState).toBe('Confirmed')

  const orderId = order.orderId
  expect(orderId).toMatch(/^order_/)
  const aftercare = page.getByRole('region', { name: '訂單配送與售後（模擬）' })
  await aftercare.getByLabel('訂單編號').fill(orderId!)
  await aftercare.getByRole('button', { name: '查詢自己的訂單' }).click()
  await expect(aftercare.getByText(/DEMO 憑證僅供展示/)).toBeVisible()
  await expect(aftercare.getByText(/無稅務效果/)).toBeVisible()

  const shipmentResponse = page.waitForResponse(response => response.url().includes(`/admin/orders/${orderId}/shipments`) && response.request().method() === 'POST')
  markStage('shipment')
  await aftercare.getByRole('button', { name: '建立模擬配送' }).click()
  const shipment = await (await shipmentResponse).json() as { id: string }
  await aftercare.getByLabel('配送編號').fill(shipment.id)
  const nextState = aftercare.getByLabel('下一狀態')
  for (const state of ['Shipped', 'InTransit', 'Delivered']) {
    await nextState.selectOption(state)
    await aftercare.getByRole('button', { name: '更新配送狀態' }).click()
  }
  await expect(aftercare.getByText(new RegExp(`${shipment.id}: 已送達`))).toBeVisible()

  const line = await page.evaluate(async id => {
    const order = await fetch(`/api/v1/orders/${id}`).then(r => r.json())
    return order.snapshot.lines[0].orderLineId as string
  }, orderId!)
  await aftercare.getByLabel('訂單明細編號').fill(line)
  await aftercare.getByLabel('原因').first().fill('Synthetic Issue 02 API acceptance')
  const returnResponse = page.waitForResponse(response => response.url().includes(`/orders/${orderId}/returns`) && response.request().method() === 'POST')
  markStage('return_inspection')
  await aftercare.getByRole('button', { name: '提出退貨申請' }).click()
  const returnCase = await (await returnResponse).json() as { id: string }
  await aftercare.getByLabel('退貨編號').fill(returnCase.id)
  await aftercare.getByRole('button', { name: '受理退貨' }).click()
  await aftercare.getByRole('button', { name: '已收件' }).click()
  await aftercare.getByLabel('可再售數量').fill('1')
  await aftercare.getByLabel('原因').last().fill('Synthetic item inspected')
  const inspectionResponse = page.waitForResponse(response => response.url().includes(`/admin/returns/${returnCase.id}/inspections`) && response.request().method() === 'POST')
  await aftercare.getByRole('button', { name: '完成驗收' }).click()
  const inspection = await (await inspectionResponse).json() as { inspection: { refundCaseId: string; refundUnits: Array<{ paidTwd: number }> } }
  expect(inspection.inspection.refundUnits.reduce((total, unit) => total + unit.paidTwd, 0)).toBe(500)

  const refundResponse = page.waitForResponse(response => response.url().includes(`/admin/refund-cases/${inspection.inspection.refundCaseId}/operations`) && response.request().method() === 'POST')
  markStage('refund')
  await aftercare.getByRole('button', { name: '執行模擬退款' }).click()
  const refundResult = await (await refundResponse).json() as { amountTwd: number; shippingTwd: number; state: string; mode: string }
  expect(refundResult.amountTwd).toBe(500)
  expect(refundResult.shippingTwd).toBe(0)
  expect(refundResult.mode).toBe('mock')
  expect(['Processing', 'Unknown']).toContain(refundResult.state)
  await expect(aftercare.getByText(/退款|refund/i)).toBeVisible()
  markStage('complete')
})
