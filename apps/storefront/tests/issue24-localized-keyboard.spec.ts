import { expect, test } from '@playwright/test'
import fixture from './fixtures/issue24-api-fixture.json' with { type: 'json' }

const locales = [
  { path: 'ja', search: '商品名、SKU、説明を検索', colors: '色', size: '色と衣服サイズ', stock: '在庫', searchAction: '商品を検索', skip: 'メインコンテンツへスキップ' },
  { path: 'en', search: 'Search product name, SKU or description', colors: 'Color', size: 'Color and garment size', stock: 'Stock', searchAction: 'Search products', skip: 'Skip to main content' },
] as const

test('ja/en keyboard search and SKU selection use the same SSR catalog fixture', async ({ page }) => {
  for (const locale of locales) {
    const productName = fixture.product.translations.find(item => item.locale === locale.path)!.name
    const search = page.getByRole('searchbox', { name: locale.search })
    const sku = page.getByRole('combobox', { name: `${locale.size}: ${productName}` })

    await page.goto(`/${locale.path}`)
    await page.keyboard.press('Tab')
    await expect(page.getByRole('link', { name: locale.skip })).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()

    let reachedSearch = false
    for (let index = 0; index < 40; index += 1) {
      await page.keyboard.press('Tab')
      if (await search.evaluate(element => element === document.activeElement)) {
        reachedSearch = true
        break
      }
    }
    expect(reachedSearch, `${locale.path} search field is keyboard reachable`).toBe(true)
    await page.keyboard.type(fixture.product.skuId)
    for (const label of [locale.colors, locale.size, locale.stock]) {
      await page.keyboard.press('Tab')
      await expect(page.getByRole('combobox', { name: label, exact: true })).toBeFocused()
    }
    await page.keyboard.press('Tab')
    await expect(page.getByRole('search').getByRole('button', { name: locale.searchAction })).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('heading', { name: productName })).toBeVisible()

    let reachedSku = false
    for (let index = 0; index < 40; index += 1) {
      await page.keyboard.press('Tab')
      if (await sku.evaluate(element => element === document.activeElement)) {
        reachedSku = true
        break
      }
    }
    expect(reachedSku, `${locale.path} SKU selector is keyboard reachable`).toBe(true)
    await page.keyboard.press('o')
    await expect(sku).toHaveValue(fixture.product.skuId)
    await page.keyboard.press('Tab')
    await expect(page.getByRole('button', { name: locale.path === 'ja' ? 'カートに追加' : 'Add to cart' })).toBeFocused()
    const addResponse = page.waitForResponse(response =>
      response.request().method() === 'PUT'
      && new URL(response.url()).pathname === `/api/v1/carts/current/items/${fixture.product.skuId}`,
    )
    await page.keyboard.press('Enter')
    expect((await addResponse).status()).toBe(200)
  }
})
