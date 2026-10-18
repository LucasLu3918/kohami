import { expect, test } from '@playwright/test'

test('cart quantity and quote controls stay keyboard-operable at 200% and 400% reflow equivalents', async ({ page }) => {
  let quantity = 2
  let cartVersion = 1
  const skuId = 'sku_demo_zoom_interaction'
  const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path === '/api/v1/auth/session') {
      return route.fulfill({ json: { state: 'Anonymous', mode: 'mock', userId: null, permissions: [], expiresAt: null } })
    }
    if (path === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: 'synthetic-zoom-csrf' } })
    if (path === '/api/v1/carts/current') {
      return route.fulfill({ json: { id: 'cart-issue24-zoom-interaction', version: `cart-v${cartVersion}`, items: [{ skuId, quantity, unitPriceTwd: 1000, availability: 'InStock' }] } })
    }
    if (path === `/api/v1/carts/current/items/${skuId}` && request.method() === 'PUT') {
      quantity = Number(request.postDataJSON().quantity)
      cartVersion += 1
      return route.fulfill({ json: { id: 'cart-issue24-zoom-interaction', version: `cart-v${cartVersion}`, items: [{ skuId, quantity, unitPriceTwd: 1000, availability: 'InStock' }] } })
    }
    if (path === '/api/v1/products') {
      return route.fulfill({ json: { items: [{
        slug: 'zoom-interaction-shirt', skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1000, availability: 'InStock',
        translations: [{ locale: 'zh-TW', name: '縮放鍵盤操作示意商品' }],
        media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖' } } }],
      }], nextCursor: null } })
    }
    if (path === '/api/v1/checkout-quotes' && request.method() === 'POST') {
      const merchandiseTwd = quantity * 1000
      return route.fulfill({ json: {
        id: 'quote-issue24-zoom-interaction', mode: 'mock', expiresAt,
        snapshot: { currency: 'TWD', merchandiseTwd, discountTwd: 0, shippingTwd: 60, totalTwd: merchandiseTwd + 60 },
      } })
    }
    return route.fulfill({ status: 404, json: { code: 'UNEXPECTED_TEST_REQUEST', path } })
  })

  for (const viewportWidth of [640, 320]) {
    await page.setViewportSize({ width: viewportWidth, height: 900 })
    await page.goto('/zh-TW/cart')

    const quantityInput = page.getByRole('spinbutton', { name: '數量' })
    await expect(quantityInput).toBeVisible()
    await quantityInput.focus()
    await quantityInput.press('ControlOrMeta+A')
    await quantityInput.pressSequentially('3')
    await quantityInput.press('Tab')
    await expect(quantityInput).toHaveValue('3')
    await expect(page.getByText('小計: TWD 3000')).toBeVisible()

    await quantityInput.focus()
    for (const locator of [page.getByRole('button', { name: '移除商品' }), page.getByLabel('優惠碼'), page.getByRole('button', { name: '套用優惠碼' }), page.getByLabel('模擬交付方式')]) {
      await page.keyboard.press('Tab')
      await expect(locator).toBeFocused()
    }
    const quoteButton = page.getByRole('button', { name: '重新試算費用' })
    await page.keyboard.press('Tab')
    await expect(quoteButton).toBeFocused()
    const quoteResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/checkout-quotes')
    await page.keyboard.press('Enter')
    await expect(quoteResponse).resolves.toBeTruthy()
    await expect(page.getByRole('region', { name: '費用明細' })).toBeVisible()
    await expect(page.getByText('TWD 3060')).toBeVisible()

    const dimensions = await page.evaluate(() => ({ document: document.documentElement.scrollWidth, body: document.body.scrollWidth, viewport: document.documentElement.clientWidth }))
    expect(dimensions.document, `document overflow at ${viewportWidth}px`).toBeLessThanOrEqual(dimensions.viewport)
    expect(dimensions.body, `body overflow at ${viewportWidth}px`).toBeLessThanOrEqual(dimensions.viewport)
  }
})
