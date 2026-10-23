import { test, expect } from '@playwright/test'

const publicBuild = process.env.DATA_SOURCE_PUBLIC_TEST === 'true'
test('default mock catalog works without backend network requests', async ({ page }) => {
  const apiRequests: string[] = []
  page.on('request', request => { if (request.url().includes('/api/')) apiRequests.push(request.url()) })
  await page.goto('/zh-TW/')
  await expect(page.locator('.product-card')).toHaveCount(7)
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
  await expect(page.locator('.product-card')).toHaveCount(7)
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
  await product.getByRole('combobox', { name: /顏色與尺寸/ }).selectOption({ index: 1 })
  await expect(add).toBeEnabled()
  await add.click()

  await page.getByRole('link', { name: '購物車', exact: true }).click()
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
  await page.getByRole('button', { name: '填入範例資料' }).click()
  await page.getByRole('button', { name: '送出付款' }).click()
  await expect(page.locator('dd[aria-live="polite"]').last()).toHaveText('系統已確認')
  const orderId = await page.locator('code').innerText()

  await page.getByRole('link', { name: '會員中心', exact: true }).click()
  await expect(page).toHaveURL(/\/zh-TW\/account$/)
  await expect(page.getByRole('heading', { name: orderId })).toBeVisible()
  expect(apiRequests).toEqual([])
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
