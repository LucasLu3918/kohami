import { expect, test } from '@playwright/test'

test('checkout status outcomes and recovery remain usable from the keyboard', async ({ page }) => {
  let paymentState: 'Created' | 'Processing' | 'Succeeded' | 'Failed' | 'Unknown' = 'Created'
  let failStatusReads = false
  const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()

  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url())
    if (url.pathname === '/api/v1/auth/session') {
      return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    }
    if (url.pathname === '/api/v1/carts/current') {
      return route.fulfill({ json: { id: 'cart-issue24', version: 'cart-v1', items: [] } })
    }
    if (url.pathname === '/api/v1/products') {
      return route.fulfill({ json: { items: [], nextCursor: null } })
    }
    if (url.pathname === '/api/v1/checkouts/checkout-issue24/status') {
      if (failStatusReads) {
        return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      }
      const confirmed = paymentState === 'Succeeded'
      return route.fulfill({ json: {
        checkoutId: 'checkout-issue24',
        mode: 'mock',
        version: `checkout-${paymentState}`,
        orderState: confirmed ? 'Confirmed' : 'AwaitingPayment',
        paymentState,
        compensationRequired: false,
        expiresAt,
        snapshot: {
          merchandiseTwd: 1000,
          discountTwd: 100,
          shippingTwd: 60,
          totalTwd: 960,
          lines: [{ skuId: 'sku_demo_02', quantity: 1, unitPriceTwd: 1000 }],
        },
      } })
    }
    return route.fulfill({ json: {} })
  })

  await page.goto('/')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as any)?.__vue_app__))
  const checkoutForm = page.getByRole('region', { name: '模擬結帳' })
  await checkoutForm.getByLabel('模擬結帳 ID').fill('checkout-issue24')
  const initialStatusResponse = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/v1/checkouts/checkout-issue24/status',
  )
  await checkoutForm.getByRole('button', { name: '查詢模擬付款狀態' }).click()
  expect((await initialStatusResponse).status()).toBe(200)
  await expect(page.getByText('尚未啟動')).toBeVisible()
  await page.getByRole('link', { name: '查看唯讀結帳摘要' }).click()

  const result = page.getByRole('region', { name: '結帳結果' })
  const refresh = result.getByRole('button', { name: '重新查詢狀態' })
  await expect(result.getByText('尚未啟動')).toBeVisible()

  let focused = false
  for (let index = 0; index < 8; index += 1) {
    await page.keyboard.press('Tab')
    if (await refresh.evaluate(element => element === document.activeElement)) {
      focused = true
      break
    }
  }
  expect(focused).toBe(true)

  const advanceWithEnter = async (state: typeof paymentState, visibleState: string) => {
    paymentState = state
    await page.keyboard.press('Enter')
    await expect(result.getByText(visibleState)).toBeVisible()
    await expect(refresh).toHaveAttribute('aria-busy', 'false')
    await expect(refresh).toBeFocused()
  }

  await advanceWithEnter('Processing', '處理中')
  await advanceWithEnter('Succeeded', '系統已確認')
  await advanceWithEnter('Failed', '未通過')
  await expect(result.getByRole('status')).toContainText('付款未通過')
  await advanceWithEnter('Unknown', '結果待確認')
  await expect(result.getByRole('status')).toContainText('不要重複啟動付款')
  await expect(result.getByRole('button', { name: '啟動模擬付款' })).toHaveCount(0)

  failStatusReads = true
  const failedRefresh = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/v1/checkouts/checkout-issue24/status' && response.status() === 503,
  )
  await page.keyboard.press('Enter')
  expect((await failedRefresh).status()).toBe(503)
  await expect(result.getByRole('alert')).toContainText('畫面保留上次取得的伺服器結果')
  await expect(result.getByText('結果待確認')).toBeVisible()
  await expect(result.locator('dl dd').nth(3)).toHaveText('TWD 960')
  await expect(refresh).toBeFocused()
})
