import { expect, test } from '@playwright/test'
import { createServer, type Server } from 'node:http'

let mockApi: Server

test.beforeAll(async () => {
  mockApi = createServer((request, response) => {
    const match = request.url?.match(/^\/api\/v1\/checkouts\/(checkout-issue14-(cancelled|expired))\/status$/)
    if (!match) {
      response.writeHead(404).end()
      return
    }
    const checkoutId = match[1]!
    const orderState = match[2] === 'cancelled' ? 'Cancelled' : 'Expired'
    response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({
      checkoutId,
      mode: 'mock',
      version: 'checkout-v1',
      orderState,
      paymentState: 'Succeeded',
      compensationRequired: true,
      expiresAt: '2026-10-17T00:15:00Z',
      snapshot: {
        merchandiseTwd: 650,
        discountTwd: 0,
        shippingTwd: 60,
        totalTwd: 710,
        lines: [{ skuId: 'sku_demo_03', quantity: 1, unitPriceTwd: 650 }],
      },
    }))
  })
  await new Promise<void>((resolve, reject) => {
    mockApi.once('error', reject)
    mockApi.listen(5080, '127.0.0.1', resolve)
  })
})

test.afterAll(async () => {
  await new Promise<void>((resolve, reject) => mockApi.close(error => error ? reject(error) : resolve()))
})

const locales = {
  'zh-TW': { title: '模擬結帳結果', payment: '模擬成功', cancelled: '已取消', expired: '已到期', message: '原訂單維持取消或到期。晚到款項的模擬退款待處理；訂單不會重新開啟。' },
  ja: { title: '模擬チェックアウト結果', payment: '模擬成功', cancelled: 'キャンセル済み', expired: '期限切れ', message: '注文はキャンセルまたは期限切れのままです。遅れて届いた支払の模擬返金は未処理で、注文は再開されません。' },
  en: { title: 'Mock checkout result', payment: 'Mock success', cancelled: 'Cancelled', expired: 'Expired', message: 'The order remains cancelled or expired. A mock refund for the late payment is pending; the order was not reopened.' },
} as const

for (const orderState of ['Cancelled', 'Expired'] as const) {
  for (const [locale, copy] of Object.entries(locales)) {
  const checkoutId = `checkout-issue14-${orderState.toLowerCase()}`
  test(`Issue14 late payment after ${orderState.toLowerCase()} explains compensation is pending (${locale})`, async ({ page }) => {
    await page.goto(`/checkout/${checkoutId}?lang=${locale}`)

    await expect(page.getByRole('heading', { name: copy.title })).toBeVisible()
    await expect(page.getByText(orderState === 'Cancelled' ? copy.cancelled : copy.expired, { exact: true })).toBeVisible()
    await expect(page.getByText(copy.message, { exact: true })).toBeVisible()
    await expect(page.getByText(copy.payment, { exact: true })).toBeVisible()
  })
  }
}
