import { expect, test } from '@playwright/test'

test('localized home pages keep catalog hydration layout shift within the Issue24 budget', async ({ page }) => {
  const product = {
    productId: 'product_issue24_perf',
    slug: 'issue24-performance-demo',
    skuId: 'sku_issue24_perf_m',
    colorId: 'oatmeal',
    garmentSize: 'M',
    priceTwd: 1000,
    availability: 'InStock',
    mode: 'mock',
    translations: [
      { locale: 'zh-TW', name: '效能示意商品' },
      { locale: 'ja', name: '計測デモ商品' },
      { locale: 'en', name: 'Performance demo item' },
    ],
    media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: 'Synthetic placeholder' } } }],
    skus: [{ skuId: 'sku_issue24_perf_m', colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1000, availability: 'InStock' }],
  }

  await page.addInitScript(() => {
    window.__issue24LayoutShifts = 0
    new PerformanceObserver(list => {
      for (const entry of list.getEntries()) {
        if (!entry.hadRecentInput) window.__issue24LayoutShifts += entry.value
      }
    }).observe({ type: 'layout-shift', buffered: true })
  })

  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-cls' } })
    if (path.endsWith('/products')) return route.fulfill({ json: { items: [product], nextCursor: null } })
    if (path.endsWith('/carts/current')) return route.fulfill({ json: { id: 'cart_issue24_perf', version: 'cart-v1', items: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd, availability: product.availability }] } })
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  for (const locale of ['zh-TW', 'ja', 'en']) {
    await page.goto(`/${locale}`, { waitUntil: 'domcontentloaded' })
    const initialCartTop = await page.locator('#cart-heading').evaluate(element => element.parentElement!.getBoundingClientRect().top)
    await expect(page.locator('#catalog-heading')).toBeVisible()
    await expect(page.getByRole('heading', { name: product.translations.find(item => item.locale === locale)!.name })).toBeVisible()
    await page.waitForTimeout(500)
    const finalCartTop = await page.locator('#cart-heading').evaluate(element => element.parentElement!.getBoundingClientRect().top)
    const cls = await page.evaluate(() => window.__issue24LayoutShifts)
    expect(Math.abs(finalCartTop - initialCartTop), `${locale} cart section moved during catalog hydration`).toBeLessThanOrEqual(100)
    expect(cls, `${locale} home CLS`).toBeLessThanOrEqual(0.1)
  }
})

declare global {
  interface Window { __issue24LayoutShifts: number }
}
