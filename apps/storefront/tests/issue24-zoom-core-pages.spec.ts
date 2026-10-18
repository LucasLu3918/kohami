import { expect, test } from '@playwright/test'

const checkoutId = 'checkout-issue24-zoom'
const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()

test('cart, checkout result, and member orders reflow at 200%/400% equivalents and a narrow viewport', async ({ page }) => {
  const sku = {
    slug: 'zoom-demo-shirt',
    skuId: 'sku_demo_02',
    colorId: 'oatmeal',
    garmentSize: 'M',
    priceTwd: 1000,
    availability: 'InStock',
    translations: [{ locale: 'zh-TW', name: '縮放驗收示意商品' }],
    media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖' } } }],
  }

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/auth/session') {
      return route.fulfill({ json: { state: 'MemberAuthenticated', mode: 'mock', userId: 'zoom-member', permissions: [], expiresAt: null } })
    }
    if (path === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: 'synthetic-zoom-csrf' } })
    if (path === '/api/v1/carts/current') {
      return route.fulfill({ json: { id: 'cart-issue24-zoom', version: 'cart-zoom-v1', items: [{ skuId: sku.skuId, quantity: 2, unitPriceTwd: sku.priceTwd, availability: 'InStock' }] } })
    }
    if (path === '/api/v1/products') return route.fulfill({ json: { items: [sku], nextCursor: null } })
    if (path === `/api/v1/checkouts/${checkoutId}/status`) {
      return route.fulfill({ json: {
        checkoutId,
        mode: 'mock',
        version: 'checkout-zoom-v1',
        orderState: 'AwaitingPayment',
        paymentState: 'Created',
        compensationRequired: false,
        expiresAt,
        snapshot: {
          merchandiseTwd: 2000,
          discountTwd: 0,
          shippingTwd: 60,
          totalTwd: 2060,
          lines: [{ skuId: sku.skuId, quantity: 2, unitPriceTwd: sku.priceTwd }],
        },
      } })
    }
    if (path === '/api/v1/orders') {
      return route.fulfill({ json: { items: [{
        id: 'order-issue24-zoom',
        mode: 'mock',
        version: 'order-zoom-v1',
        placedAt: '2026-10-18T00:00:00Z',
        orderState: 'Confirmed',
        snapshot: { currency: 'TWD', totalTwd: 2060, lines: [{ skuId: sku.skuId, quantity: 2 }] },
      }] } })
    }
    return route.fulfill({ status: 404, json: { code: 'UNEXPECTED_TEST_REQUEST', path } })
  })

  for (const viewportWidth of [640, 320, 160]) {
    await page.setViewportSize({ width: viewportWidth, height: 900 })

    await page.goto('/zh-TW/cart')
    await expect(page.getByRole('heading', { name: '購物車明細' }).first()).toBeVisible()
    await expect(page.getByRole('heading', { name: '縮放驗收示意商品' })).toBeVisible()
    await expect(page.getByRole('spinbutton', { name: '數量' })).toBeVisible()
    await expect(page.getByRole('button', { name: '重新試算費用' })).toBeVisible()
    await expectNoHorizontalOverflow(page, viewportWidth)

    await page.goto('/zh-TW')
    await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
    const checkoutForm = page.getByRole('region', { name: '模擬結帳' })
    await checkoutForm.getByLabel('模擬結帳 ID').fill(checkoutId)
    await checkoutForm.getByRole('button', { name: '查詢模擬付款狀態' }).click()
    await page.getByRole('link', { name: '查看唯讀結帳摘要' }).click()
    await expect(page.getByRole('heading', { name: '模擬結帳結果' })).toBeVisible()
    await expect(page.getByText(checkoutId)).toBeVisible()
    await expect(page.getByText('TWD 2060')).toBeVisible()
    await expect(page.getByRole('button', { name: '重新查詢狀態' })).toBeVisible()
    await expectNoHorizontalOverflow(page, viewportWidth)

    await page.goto('/zh-TW/account')
    await expect(page.getByRole('heading', { name: '我的訂單' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'order-issue24-zoom' })).toBeVisible()
    await expect(page.getByText('TWD 2,060')).toBeVisible()
    await expectNoHorizontalOverflow(page, viewportWidth)
  }
})

async function expectNoHorizontalOverflow(page: import('@playwright/test').Page, viewportWidth: number) {
  const dimensions = await page.evaluate(() => ({
    document: document.documentElement.scrollWidth,
    body: document.body.scrollWidth,
    viewport: document.documentElement.clientWidth,
  }))
  const overflowingElements = await page.locator('body *').evaluateAll((elements, width) => elements
    .map(element => ({
      tag: element.tagName.toLowerCase(),
      text: (element.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 80),
      right: Math.round(element.getBoundingClientRect().right),
      width: Math.round(element.getBoundingClientRect().width),
    }))
    .filter(element => element.right > width + 1)
    .slice(0, 8), viewportWidth)
  expect(dimensions.viewport).toBe(viewportWidth)
  expect(dimensions.document, `document horizontal overflow at ${viewportWidth}px: ${JSON.stringify(overflowingElements)}`).toBeLessThanOrEqual(viewportWidth)
  expect(dimensions.body, `body horizontal overflow at ${viewportWidth}px`).toBeLessThanOrEqual(viewportWidth)
}
