import { test, expect } from '@playwright/test'

const publicBuild = process.env.DATA_SOURCE_PUBLIC_TEST === 'true'
test('default mock catalog works without backend network requests', async ({ page }) => {
  const apiRequests: string[] = []
  page.on('request', request => { if (request.url().includes('/api/')) apiRequests.push(request.url()) })
  await page.goto('/zh-TW/')
  await expect(page.locator('.product-card')).toHaveCount(15)
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText(publicBuild ? '靜態前端展示' : '前端互動模擬')
  if (publicBuild) {
    await expect(page.locator('.store-header__quick-links')).not.toContainText('購物車')
    await expect(page.locator('.product-card__add').first()).toBeDisabled()
  } else {
    await expect(page.locator('.store-header__quick-links')).toContainText('購物車')
    await page.locator('.product-card__sku select').first().selectOption({ index: 1 })
    await expect(page.locator('.product-card__add').first()).toBeEnabled()
  }
  await page.locator('.filters input[name=q]').fill('棉麻')
  await page.locator('.filters').getByRole('button', { name: '搜尋商品', exact: true }).click()
  await expect(page.locator('.product-card')).toHaveCount(2)
  await page.locator('.product-card__image-link').first().click()
  await expect(page.locator('.product-info h1')).toBeVisible()
  expect(apiRequests).toEqual([])
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: `${test.info().outputDir}/data-source.png`, fullPage: true })
})

test('footer switches to backend, survives reload and clears old source state', async ({ page }) => {
  test.skip(publicBuild, 'Public deployment has no backend configured')
  await page.goto('/zh-TW/')
  await page.getByRole('button', { name: '切換至後端資料', exact: true }).click()
  await expect(page.getByRole('region', { name: '資料模式' }).getByRole('status')).toHaveText('後端資料')
  await expect(page.locator('.product-card')).toHaveCount(1)
  await expect(page.locator('.product-card')).toContainText('後端來源驗收商品')
  await page.reload()
  await expect(page.locator('.product-card')).toContainText('後端來源驗收商品')
  await page.getByRole('button', { name: '切換至前端模擬', exact: true }).click()
  await expect(page.locator('.product-card')).toHaveCount(15)
  await expect(page.locator('.product-grid')).not.toContainText('後端來源驗收商品')
})

test('public build marks backend unavailable and excludes private routes', async ({ page, request }) => {
  test.skip(!publicBuild, 'Only applies to static Cloudflare artifact')
  await page.goto('/zh-TW/')
  await expect(page.getByRole('button', { name: '切換至後端資料', exact: true })).toBeDisabled()
  await expect(page.getByText('後端尚未設定', { exact: true })).toBeVisible()
  for (const path of ['/api/v1/products', '/admin/login/', '/zh-TW/cart/', '/zh-TW/account/']) {
    expect((await request.get(path)).status()).toBe(404)
  }
  for (const locale of ['ja', 'en']) {
    await page.goto(`/${locale}/products/demo-linen-shirt/`)
    await expect(page.locator('.product-info h1')).toBeVisible()
    await expect(page.locator('html')).toHaveAttribute('lang', locale)
  }
})
