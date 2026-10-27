import { test, expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const publicBuild = process.env.DATA_SOURCE_PUBLIC_TEST === 'true'
const syntheticCatalogSize = 15

// The first page holds one batch of SKUs, so the remaining products load on demand.
async function loadFullCatalog(page: Page) {
  await page.getByRole('button', { name: '載入更多', exact: true }).click()
  await expect(page.locator('.product-card')).toHaveCount(syntheticCatalogSize)
}

test('every catalog color shows and loads its matching product-only image', async ({ page }) => {
  const manifestPath = fileURLToPath(new URL('../public/demo-assets/products/color-variants/ASSET_MANIFEST.json', import.meta.url))
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) as { assets: Array<{ product: string; colorId: string; url: string; concept: boolean }> }
  const products = [...new Set(manifest.assets.map(asset => asset.product))]

  for (const product of products) {
    const colorMedia = manifest.assets.filter(asset => asset.product === product)
    await page.goto(`/zh-TW/products/demo-${product}`)
    await expect(page.locator('.product-info h1')).toBeVisible()

    for (const [index, media] of colorMedia.entries()) {
      await page.locator('.color-options .color-option').nth(index).click()
      const image = page.locator('.product-gallery__main')
      await expect(image).toHaveAttribute('src', media.url)
      await expect.poll(() => image.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBe(1024)
      if (media.concept) await expect(image).toHaveAttribute('alt', /概念色示意，非實品照片$/)
      else await expect(image).toHaveAttribute('alt', /單品平拍$/)
    }
  }
})

test('default mock catalog works without backend network requests', async ({ page }) => {
  const apiRequests: string[] = []
  page.on('request', request => { if (request.url().includes('/api/')) apiRequests.push(request.url()) })
  await page.goto('/zh-TW/')
  await loadFullCatalog(page)
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText('互動式 mock')
  await expect(page.locator('.store-header__quick-links')).toContainText('會員中心')
  await expect(page.locator('.store-header__quick-links')).toContainText('購物車')
  const firstSize = page.locator('.product-card__sizes button:not(:disabled)').first()
  await firstSize.scrollIntoViewIfNeeded()
  const scrollYBeforeSelection = await page.evaluate(() => window.scrollY)
  await firstSize.click()
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollYBeforeSelection)
  await expect(page.locator('.product-card__add').first()).toBeEnabled()
  await page.locator('.filters input[name=q]').fill('棉麻')
  await page.locator('.filters').getByRole('button', { name: '搜尋商品', exact: true }).click()
  await expect(page.locator('.product-card')).toHaveCount(2)
  await page.locator('.product-card__image-link').first().click()
  await expect(page.locator('.product-info h1')).toBeVisible()
  expect(apiRequests).toEqual([])
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: `${test.info().outputDir}/data-source.png`, fullPage: true })
})

test('catalog card photos keep their intended aspect ratio', async ({ page }) => {
  await page.goto('/zh-TW/')
  const photo = page.locator('.product-card__image-link img').first()
  await expect(photo).toBeVisible()
  const box = await photo.boundingBox()
  expect(box).not.toBeNull()
  expect(box!.height / box!.width).toBeCloseTo(1.08, 1)
})

test('hero banner controls meet the 24 by 24 pixel target size', async ({ page }) => {
  await page.goto('/zh-TW/')
  const dots = page.locator('.storefront-hero__dot')
  await expect(dots.first()).toBeVisible()
  const count = await dots.count()
  for (let index = 0; index < count; index++) {
    const box = await dots.nth(index).boundingBox()
    expect(box!.width).toBeGreaterThanOrEqual(24)
    expect(box!.height).toBeGreaterThanOrEqual(24)
  }
})
test('phone header keeps brand, cart and language in one compact row', async ({ page }) => {
  for (const width of [375, 320]) {
    await page.setViewportSize({ width, height: 812 })
    await page.goto('/zh-TW')
    const header = await page.locator('header.store-header').boundingBox()
    expect(header!.height).toBeLessThanOrEqual(130)
    const brand = await page.locator('.store-header__brand').boundingBox()
    const cart = await page.locator('.store-header__cart-link').boundingBox()
    const language = await page.locator('.locale-switcher__trigger').boundingBox()
    expect(Math.abs(cart!.y - brand!.y)).toBeLessThan(12)
    expect(Math.abs(language!.y - brand!.y)).toBeLessThan(12)
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  }
})

test('phone catalog filters sit in compact rows', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 })
  await page.goto('/zh-TW/')
  const color = await page.locator('select[name=colorId]').boundingBox()
  const size = await page.locator('select[name=garmentSize]').boundingBox()
  const stock = await page.locator('select[name=availability]').boundingBox()
  expect(Math.abs(color!.y - size!.y)).toBeLessThan(2)
  expect(Math.abs(size!.y - stock!.y)).toBeLessThan(2)
})

test('catalog color filter shows localized color names', async ({ page }) => {
  await page.goto('/zh-TW/')
  const colorFilter = page.locator('select[name=colorId]')
  await expect(colorFilter).toContainText('炭灰')
  await expect(colorFilter).not.toContainText('charcoal')
})

test('footer switches to backend, survives reload and clears old source state', async ({ page }) => {
  test.skip(publicBuild, 'Public deployment has no backend configured')
  await page.goto('/zh-TW/')
  await page.getByRole('button', { name: '切換至 API 模式', exact: true }).click()
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText('API 模式')
  if (publicBuild) {
    await expect(page.getByText('目前無法載入商品。')).toBeVisible()
    await expect(page.locator('.product-card')).toHaveCount(0)
  } else {
    await expect(page.locator('.product-card')).toHaveCount(1)
    await expect(page.locator('.product-card')).toContainText('後端來源驗收商品')
  }
  await page.reload()
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText('API 模式')
  if (publicBuild) await expect(page.getByText('目前無法載入商品。')).toBeVisible()
  else await expect(page.locator('.product-card')).toContainText('後端來源驗收商品')
  await page.getByRole('button', { name: '切換至 mock 模式', exact: true }).click()
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText('互動式 mock')
  await loadFullCatalog(page)
  await expect(page.locator('.product-grid')).not.toContainText('後端來源驗收商品')
})

test('interactive mock completes member sign-in, cart, checkout, payment and order lookup', async ({ page }) => {
  const apiRequests: string[] = []
  page.on('request', request => { if (request.url().includes('/api/')) apiRequests.push(request.url()) })
  await page.goto('/zh-TW/')
  const product = page.locator('.product-card').first()
  const add = product.getByRole('button', { name: '加入購物車' })
  await expect(product.locator('.product-card__hint')).toHaveText('加入購物車前，請先選擇顏色與尺寸。')
  await expect(add).toBeDisabled()
  await product.getByRole('group', { name: /尺寸:/ }).getByRole('button', { name: 'M' }).click()
  await expect(add).toBeEnabled()
  await add.click()

  await page.getByRole('link', { name: /購物車/ }).click()
  await expect(page).toHaveURL(/\/zh-TW\/cart$/)
  const cartLine = page.getByRole('listitem').first()
  await expect(cartLine).toBeVisible()
  await page.getByRole('button', { name: '重新試算費用' }).click()
  await page.getByRole('link', { name: '會員登入' }).click()
  await expect(page).toHaveURL(/\/zh-TW\/account\?returnTo=/)
  await page.getByRole('button', { name: '一鍵測試登入' }).click()

  await expect(page).toHaveURL(/\/zh-TW\/cart/)
  await expect(page.getByRole('checkbox', { name: /我確認這份報價/ })).toBeVisible()
  await page.getByRole('checkbox', { name: /我確認這份報價/ }).check()
  await page.getByRole('button', { name: '建立訂單並保留庫存' }).click()
  await expect(page).toHaveURL(/\/zh-TW\/checkout\//)
  await page.getByRole('button', { name: '送出付款' }).click()
  await expect(page.locator('dd[aria-live="polite"]').last()).toHaveText('系統已確認')
  const orderId = await page.locator('code').innerText()

  await page.getByRole('link', { name: '會員中心', exact: true }).click()
  await expect(page).toHaveURL(/\/zh-TW\/account$/)
  await expect(page.getByRole('heading', { name: orderId })).toBeVisible()
  expect(apiRequests).toEqual([])
})

test('public build switches to API errors and serves mock commerce routes', async ({ page, request }) => {
  test.skip(!publicBuild, 'Only applies to static Cloudflare artifact')
  const apiStatuses: number[] = []
  page.on('response', response => {
    if (new URL(response.url()).pathname.startsWith('/api/v1')) apiStatuses.push(response.status())
  })
  await page.goto('/zh-TW/')
  await expect(page.getByRole('button', { name: '切換至 API 模式', exact: true })).toBeEnabled()
  await page.getByRole('button', { name: '切換至 API 模式', exact: true }).click()
  await expect(page.getByText('目前無法載入商品。')).toBeVisible()
  await expect.poll(() => apiStatuses).toContain(503)
  await expect(page.locator('.product-card')).toHaveCount(0)
  await page.getByRole('button', { name: '切換至 mock 模式', exact: true }).click()
  await loadFullCatalog(page)
  expect((await request.get('/api/v1/products')).status()).toBe(503)
  expect((await request.get('/zh-TW/cart/')).status()).toBe(200)
  expect((await request.get('/zh-TW/account/')).status()).toBe(200)
  for (const locale of ['ja', 'en']) {
    await page.goto(`/${locale}/products/demo-linen-shirt/`)
    await expect(page.locator('.product-info h1')).toBeVisible()
    await expect(page.locator('html')).toHaveAttribute('lang', locale)
  }
})
