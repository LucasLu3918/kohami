import { expect, test } from '@playwright/test'
import type { Locator, Page } from '@playwright/test'
import fixture from './fixtures/issue24-api-fixture.json' with { type: 'json' }

async function tabTo(page: Page, target: Locator, limit = 100) {
  for (let index = 0; index < limit; index += 1) {
    if (await target.evaluate(element => element === document.activeElement)) return
    await page.keyboard.press('Tab')
  }
  throw new Error('Keyboard navigation did not reach the expected control')
}

for (const item of [
  { locale: 'zh-TW', path: '/zh-TW/policies', label: '略過導覽，前往主要內容' },
  { locale: 'ja', path: '/ja/policies', label: 'メインコンテンツへスキップ' },
  { locale: 'en', path: '/en/policies', label: 'Skip to main content' },
]) {
  test(`Issue24 ${item.locale} skip link is the first keyboard stop and focuses main content`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(item.path)
    await page.keyboard.press('Tab')
    const skipLink = page.getByRole('link', { name: item.label })
    await expect(skipLink).toBeFocused()
    await expect(skipLink).toBeInViewport()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()
    await expect(page).toHaveURL(/#main-content$/)
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  })
}

test('Issue24 localized catalog reflows at equivalent 200% and 400% desktop zoom viewports', async ({ page }) => {
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-reflow' } })
    if (path.endsWith('/carts/current')) return route.fulfill({ json: { id: 'issue24-reflow-cart', version: 'cart-v1', items: [] } })
    if (path.endsWith('/products')) return route.fulfill({ json: { items: [], nextCursor: null } })
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  for (const locale of ['zh-TW', 'ja', 'en']) {
    for (const width of [720, 320]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto(`/${locale}`)
      await expect(page.locator('#main-content')).toBeVisible()
      const documentWidth = await page.evaluate(() => document.documentElement.scrollWidth)
      expect(documentWidth, `${locale} home at ${width}px CSS viewport`).toBeLessThanOrEqual(width)
    }
  }
})

test('Issue24 keyboard search and add-to-cart errors retain a recoverable retry path', async ({ page }) => {
  let queryProductReads = 0
  let cartWrites = 0
  const product = {
    productId: 'issue24-keyboard-errors-product',
    slug: 'issue24-keyboard-errors-shirt',
    translations: [{ locale: 'en', name: 'Keyboard error demo shirt' }],
    media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: 'Synthetic placeholder' } } }],
    skuId: 'sku-issue24-keyboard-errors-m',
    colorId: 'oatmeal',
    garmentSize: 'M',
    priceTwd: 1200,
    availability: 'InStock',
    mode: 'mock',
  }

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (url.pathname.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-keyboard-errors' } })
    if (url.pathname.endsWith('/carts/current') && request.method() === 'GET') return route.fulfill({ json: { id: 'issue24-keyboard-errors-cart', version: 'cart-v1', items: [] } })
    if (url.pathname.endsWith('/products') && request.method() === 'GET') {
      if (url.searchParams.has('q')) {
        queryProductReads += 1
        if (queryProductReads <= 2) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      }
      return route.fulfill({ json: { items: [product], nextCursor: null } })
    }
    if (url.pathname.endsWith(`/carts/current/items/${product.skuId}`) && request.method() === 'PUT') {
      cartWrites += 1
      if (cartWrites === 1) {
        await new Promise(resolve => setTimeout(resolve, 100))
        return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      }
      return route.fulfill({ json: { id: 'issue24-keyboard-errors-cart', version: 'cart-v2', items: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd }] } })
    }
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  await page.goto('/en')
  await page.waitForTimeout(250)
  await page.keyboard.press('Tab')
  await page.keyboard.press('Enter')

  const search = page.getByRole('searchbox', { name: 'Search product name, SKU or description' })
  await tabTo(page, search)
  await page.keyboard.type('Keyboard error')
  const searchButton = page.getByRole('search').getByRole('button', { name: 'Search products' })
  await tabTo(page, searchButton)
  const failedSearch = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/products' && new URL(response.url()).searchParams.has('q'))
  await page.keyboard.press('Enter')
  expect((await failedSearch).status()).toBe(503)

  const searchAlert = page.getByRole('alert').filter({ hasText: 'Products could not be loaded.' })
  await expect(searchAlert).toBeVisible()
  const retrySearch = searchAlert.getByRole('button', { name: 'Search products' })
  await tabTo(page, retrySearch)
  const recoveredSearch = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/products' && new URL(response.url()).searchParams.has('q') && response.status() === 200)
  await page.keyboard.press('Enter')
  expect((await recoveredSearch).status()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Keyboard error demo shirt' })).toBeVisible()

  const sku = page.getByRole('combobox', { name: 'Color and garment size: Keyboard error demo shirt' })
  await tabTo(page, sku)
  await page.keyboard.press('o')
  await page.keyboard.press('Tab')
  await expect(sku).toHaveValue(product.skuId)
  const add = page.getByRole('button', { name: 'Add to cart' })
  await tabTo(page, add)
  const failedAdd = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/carts/current/items/${product.skuId}`)
  await page.keyboard.press('Enter')
  await expect(add).toHaveAttribute('aria-busy', 'true')
  await expect(add).toBeFocused()
  await page.keyboard.press('Enter')
  await page.waitForTimeout(20)
  expect(cartWrites).toBe(1)
  expect((await failedAdd).status()).toBe(503)
  await expect(page.getByRole('alert')).toHaveText('The local demo service is temporarily unavailable.')
  await expect(add).toBeFocused()

  const recoveredAdd = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v1/carts/current/items/${product.skuId}` && response.status() === 200)
  await page.keyboard.press('Enter')
  expect((await recoveredAdd).status()).toBe(200)
  await expect(page.getByText('sku-issue24-keyboard-errors-m', { exact: true })).toBeVisible()
})

for (const locale of [
  { path: 'zh-TW', cart: '購物車', quantity: '數量', alert: '本機展示服務暫時無法使用。' },
  { path: 'ja', cart: 'カート', quantity: '数量', alert: 'ローカルデモサービスを利用できません。' },
  { path: 'en', cart: 'Cart', quantity: 'Quantity', alert: 'The local demo service is temporarily unavailable.' },
]) {
  test(`Issue24 ${locale.path} desktop cart errors do not move cart contents`, async ({ page }) => {
    let cartWrites = 0
    await page.route('**/api/v1/**', async route => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
      if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-layout-stability' } })
      if (path.endsWith('/carts/current') && request.method() === 'GET') return route.fulfill({ json: { id: `cart-${locale.path}`, version: 'cart-v1', items: [{ skuId: fixture.product.skuId, quantity: 1, unitPriceTwd: fixture.product.priceTwd, availability: 'InStock' }] } })
      if (path.endsWith('/products') && request.method() === 'GET') return route.fulfill({ json: { items: [fixture.product], nextCursor: null } })
      if (path.endsWith(`/carts/current/items/${fixture.product.skuId}`) && request.method() === 'PUT') {
        cartWrites += 1
        if (cartWrites === 1) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
        return route.fulfill({ json: { id: `cart-${locale.path}`, version: 'cart-v2', items: [{ skuId: fixture.product.skuId, quantity: 3, unitPriceTwd: fixture.product.priceTwd }] } })
      }
      return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
    })

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto(`/${locale.path}`)
    const cartList = page.locator('.cart-list')
    await expect(cartList).toBeVisible()
    await expect(page.locator('section[aria-labelledby="cart-heading"] [role="status"]')).toHaveCount(0)
    await page.evaluate(() => document.fonts.ready.then(() => true))
    await page.waitForTimeout(250)
    const cartSection = page.getByRole('region', { name: locale.cart })
    const documentTop = () => cartList.evaluate(element => element.getBoundingClientRect().top + window.scrollY)
    const initialTop = await documentTop()
    const quantity = page.getByRole('spinbutton', { name: locale.quantity })
    const failedUpdate = page.waitForResponse(response => new URL(response.url()).pathname.endsWith(`/carts/current/items/${fixture.product.skuId}`) && response.status() === 503)
    await quantity.fill('2')
    await quantity.press('Tab')
    expect((await failedUpdate).status()).toBe(503)
    await expect(cartSection.getByRole('alert')).toContainText(locale.alert)
    expect(await documentTop()).toBe(initialTop)

    const successfulRetry = page.waitForResponse(response => new URL(response.url()).pathname.endsWith(`/carts/current/items/${fixture.product.skuId}`) && response.status() === 200)
    await quantity.fill('3')
    await quantity.press('Tab')
    expect((await successfulRetry).status()).toBe(200)
    await expect(quantity).toHaveValue('3')
    expect(await documentTop()).toBe(initialTop)
    expect(cartWrites).toBe(2)
  })
}
