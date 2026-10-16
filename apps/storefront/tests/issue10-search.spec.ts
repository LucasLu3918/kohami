import { expect, test } from '@playwright/test'

type ProductSku = {
  productId: string
  slug: string
  translations: Array<{ locale: string; name: string }>
  skuId: string
  colorId: string
  garmentSize: string
  availability: string
}

test('Issue10 public search applies color and size to the same active SKU', async ({ page, request }) => {
  const catalogResponse = await request.get('/api/v1/products?limit=100&locale=zh-TW')
  expect(catalogResponse.ok()).toBeTruthy()
  const catalog = await catalogResponse.json() as { items: ProductSku[] }
  expect(catalog.items.length).toBeGreaterThan(0)
  const unavailableSku = process.env.KOHAMI_ISSUE10_OUT_OF_STOCK_SKU
  expect(unavailableSku).toBeTruthy()
  const unavailableResult = await request.get(`/api/v1/products?q=${encodeURIComponent(unavailableSku!)}&limit=100`)
  expect((await unavailableResult.json() as { items: ProductSku[] }).items.some(item => item.skuId === unavailableSku && item.availability === 'OutOfStock')).toBeTruthy()
  const availableOnly = await request.get(`/api/v1/products?q=${encodeURIComponent(unavailableSku!)}&availability=in_stock&limit=100`)
  expect((await availableOnly.json() as { items: ProductSku[] }).items.some(item => item.skuId === unavailableSku)).toBeFalsy()

  const first = catalog.items[0]!
  const inStock = await request.get('/api/v1/products?availability=in_stock&limit=100')
  expect((await inStock.json() as { items: ProductSku[] }).items.every(item => item.availability === 'InStock')).toBeTruthy()
  const firstPage = await request.get('/api/v1/products?limit=1')
  const firstPageBody = await firstPage.json() as { items: ProductSku[]; nextCursor: string | null }
  expect(firstPageBody.nextCursor).toBeTruthy()
  const secondPage = await request.get(`/api/v1/products?limit=1&cursor=${encodeURIComponent(firstPageBody.nextCursor!)}`)
  expect(secondPage.ok()).toBeTruthy()
  expect((await secondPage.json() as { items: ProductSku[] }).items[0]?.slug).not.toBe(firstPageBody.items[0]?.slug)
  const exactSku = await request.get(`/api/v1/products?q=${encodeURIComponent(first.skuId)}&limit=100`)
  expect(exactSku.ok()).toBeTruthy()
  const exactItems = (await exactSku.json() as { items: ProductSku[] }).items
  expect(exactItems.map(item => item.skuId)).toContain(first.skuId)

  const productSkus = catalog.items.filter(item => item.slug === first.slug)
  const validSku = productSkus.find(item => item.colorId === first.colorId && item.garmentSize === first.garmentSize)!
  const sameSku = await request.get(`/api/v1/products?colorId=${encodeURIComponent(validSku.colorId)}&garmentSize=${encodeURIComponent(validSku.garmentSize)}&limit=100`)
  expect((await sameSku.json() as { items: ProductSku[] }).items.some(item => item.skuId === validSku.skuId)).toBeTruthy()

  const mismatched = productSkus.flatMap(colorSku => productSkus.map(sizeSku => ({ colorSku, sizeSku })))
    .find(({ colorSku, sizeSku }) => colorSku.colorId !== sizeSku.colorId && !productSkus.some(item => item.colorId === colorSku.colorId && item.garmentSize === sizeSku.garmentSize))
  if (mismatched) {
    const mismatchResponse = await request.get(`/api/v1/products?colorId=${encodeURIComponent(mismatched.colorSku.colorId)}&garmentSize=${encodeURIComponent(mismatched.sizeSku.garmentSize)}&q=${encodeURIComponent(first.skuId)}&limit=100`)
    expect(mismatchResponse.status()).toBe(200)
    expect((await mismatchResponse.json() as { items: ProductSku[] }).items.some(item => item.slug === first.slug)).toBeFalsy()
  }

  expect((await request.get('/api/v1/products?unknownFilter=true')).status()).toBe(422)
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/?lang=zh-TW')
  const search = page.getByRole('searchbox', { name: '搜尋名稱、SKU 或商品描述' })
  await search.fill(first.skuId)
  await search.press('Enter')
  const productLink = page.getByRole('link', { name: '查看商品與尺寸表' })
  await expect(productLink.first()).toBeVisible()
  await expect(productLink.first()).toHaveAttribute('href', new RegExp(`/products/${first.slug}`))
  await page.getByRole('combobox', { name: '顏色', exact: true }).selectOption(first.colorId)
  await page.getByRole('combobox', { name: '顏色與衣服尺寸', exact: true }).selectOption(first.garmentSize)
  await expect(page.getByRole('heading', { name: first.translations.find(item => item.locale === 'zh-TW')?.name || first.skuId }).first()).toBeVisible()
})

test('Issue10 homepage communicates no results and recovers from a catalog read failure', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/?lang=zh-TW')
  await expect(page.locator('.product-card').first()).toBeVisible()
  await page.context().setOffline(true)
  const search = page.getByRole('searchbox', { name: '搜尋名稱、SKU 或商品描述' })
  await search.fill('沒有這個商品-issue10')
  await page.getByRole('button', { name: '搜尋商品' }).first().click()
  await expect(page.getByRole('alert')).toContainText('目前無法載入商品')
  await page.context().setOffline(false)
  await page.getByRole('alert').getByRole('button', { name: '搜尋商品' }).click()
  await expect(page.getByRole('status').filter({ hasText: '找不到符合條件的商品' })).toBeVisible()
})
