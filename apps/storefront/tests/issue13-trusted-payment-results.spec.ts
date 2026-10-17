import { expect, test } from '@playwright/test'

test('checkout result refreshes the server-confirmed payment state without changing its snapshot', async ({ page }) => {
  let statusReads = 0
  const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()

  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url())
    if (url.pathname === '/api/v1/auth/session') {
      return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    }
    if (url.pathname === '/api/v1/carts/current') {
      return route.fulfill({ json: { id: 'cart-issue13', version: 'cart-v1', items: [] } })
    }
    if (url.pathname === '/api/v1/products') {
      return route.fulfill({ json: {
        items: [{
          productId: 'product-issue13', slug: 'issue13-demo',
          translations: [{ locale: 'zh-TW', name: '合成示意商品 B' }], media: [],
          skuId: 'sku_demo_02', colorId: 'demo', garmentSize: 'M', priceTwd: 1200,
          availability: 'InStock', mode: 'mock',
        }],
        nextCursor: null,
      } })
    }
    if (url.pathname === '/api/v1/checkouts/checkout-issue13/status') {
      statusReads += 1
      const confirmed = statusReads >= 3
      return route.fulfill({
        json: {
          checkoutId: 'checkout-issue13',
          mode: 'mock',
          version: confirmed ? 'checkout-v3' : `checkout-v${statusReads}`,
          orderState: confirmed ? 'Confirmed' : 'AwaitingPayment',
          paymentState: confirmed ? 'Succeeded' : 'Created',
          compensationRequired: false,
          expiresAt,
          snapshot: {
            merchandiseTwd: 1000,
            discountTwd: 100,
            shippingTwd: 60,
            totalTwd: 960,
            lines: [{ skuId: 'sku_demo_02', quantity: 1, unitPriceTwd: 1000 }],
          },
        },
      })
    }
    return route.fulfill({ json: {} })
  })

  await page.goto('/')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as any)?.__vue_app__))
  await expect(page.getByText('TWD 1200')).toBeVisible()
  const checkoutForm = page.getByRole('region', { name: '模擬結帳' })
  await checkoutForm.getByLabel('模擬結帳 ID').fill('checkout-issue13')
  const initialStatusResponse = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/v1/checkouts/checkout-issue13/status',
    { timeout: 5000 },
  )
  await checkoutForm.getByRole('button', { name: '查詢模擬付款狀態' }).click()
  expect((await initialStatusResponse).status()).toBe(200)
  await expect(page.getByText('等待付款')).toBeVisible()
  await page.getByRole('link', { name: '查看唯讀結帳摘要' }).click()

  const result = page.getByRole('region', { name: '模擬結帳結果' })
  await expect(result.getByText('等待付款')).toBeVisible()
  await expect(result.getByText('尚未啟動')).toBeVisible()
  await expect(result.locator('dl dd').nth(0)).toHaveText('TWD 1000')
  await expect(result.locator('dl dd').nth(1)).toHaveText('TWD 100')
  await expect(result.locator('dl dd').nth(2)).toHaveText('TWD 60')
  await expect(result.locator('dl dd').nth(3)).toHaveText('TWD 960')

  await result.getByRole('button', { name: '重新查詢狀態' }).click()
  await expect(result.getByText('已確認')).toBeVisible()
  await expect(result.getByText('模擬成功')).toBeVisible()
  await expect(result.locator('dl dd').nth(0)).toHaveText('TWD 1000')
  await expect(result.locator('dl dd').nth(1)).toHaveText('TWD 100')
  await expect(result.locator('dl dd').nth(2)).toHaveText('TWD 60')
  await expect(result.locator('dl dd').nth(3)).toHaveText('TWD 960')
  expect(statusReads).toBe(3)
})
