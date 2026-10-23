import { expect, test } from '@playwright/test'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

test('storefront home keeps search with the catalog and preserves discovery controls', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/ja')
  await page.waitForLoadState('networkidle')

  await expect(page.locator('.store-header__search')).toHaveCount(0)
  await expect(page.getByRole('navigation', { name: '商品カテゴリー' })).toHaveCount(0)
  await expect(page.getByRole('search')).toHaveCount(1)
  await expect(page.getByRole('search').getByRole('searchbox', { name: '商品名、説明を検索' })).toBeVisible()
  await expect(page.getByRole('search').getByRole('button', { name: '商品を検索' })).toBeVisible()

  await page.getByRole('search').getByRole('searchbox', { name: '商品名、説明を検索' }).fill('リネン')
  await page.getByRole('search').getByRole('button', { name: '商品を検索' }).click()
  await expect(page).toHaveURL(/\/ja\?q=/)

  const color = await page.locator('select[name="colorId"] option').nth(1).getAttribute('value')
  expect(color).toBeTruthy()
  await page.locator('select[name="colorId"]').selectOption(color!)
  await expect(page).toHaveURL(/colorId=/)
  await expect(page).toHaveURL(/q=/)

  await page.getByRole('button', { name: '条件をクリア' }).click()
  await expect(page).not.toHaveURL(/q=|colorId=|garmentSize=|availability=/)

  await page.locator('.product-card h3 a').first().click()
  await expect(page).toHaveURL(/\/ja\/products\//)
  await expect(page.locator('.store-header__search')).toBeVisible()
  const dimensions = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    document: document.documentElement.scrollWidth,
  }))
  expect(dimensions.document).toBeLessThanOrEqual(dimensions.viewport)
})
