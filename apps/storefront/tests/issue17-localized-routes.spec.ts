import { expect, test } from '@playwright/test'

test('legacy language query redirects to a locale-prefixed storefront URL', async ({ page }) => {
  await page.goto('/?lang=en')
  await expect(page).toHaveURL(/\/en$/)
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
})

test('localized product route retains SKU selection while switching language', async ({ page }) => {
  await page.goto('/ja/products/demo-product?color=oatmeal&size=M')
  await expect(page.getByRole('heading', { name: 'デモ商品' })).toBeVisible()
  await expect(page.locator('html')).toHaveAttribute('lang', 'ja')
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'index,follow')
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', 'https://shop.example.test/ja/products/demo-product')
  await expect(page.locator('link[rel="alternate"][hreflang="en"]')).toHaveAttribute('href', 'https://shop.example.test/en/products/demo-product')
  await page.getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\/products\/demo-product\?color=oatmeal&size=M$/)
  await expect(page.getByRole('heading', { name: 'Demo product' })).toBeVisible()
  await expect(page.getByLabel('Color')).toHaveValue('oatmeal')
  await expect(page.getByLabel('Garment size', { exact: true })).toHaveValue('M')
})

test('missing product translation keeps the selected language and labels the fallback', async ({ page }) => {
  await page.goto('/ja/products/missing-translation')
  await expect(page.locator('html')).toHaveAttribute('lang', 'ja')
  await expect(page.getByRole('heading', { name: '示意商品' })).toBeVisible()
  await expect(page.getByText('一部の内容は繁体字中国語で表示されます。')).toBeVisible()
})

test('cart contents and TWD amounts remain the same while switching language', async ({ page }) => {
  await page.goto('/ja/cart')
  await expect(page.locator('h1')).toHaveText('カート明細')
  await expect(page.getByText('デモ商品')).toBeVisible()
  await expect(page.getByText(/TWD 1200/).first()).toBeVisible()
  await page.getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\/cart$/)
  await expect(page.locator('h1')).toHaveText('Cart details')
  await expect(page.getByText('Demo product')).toBeVisible()
  await expect(page.getByText(/TWD 1200/).first()).toBeVisible()
})

test('unconfirmed payment status stays attached to the same checkout in each locale', async ({ page }) => {
  await page.goto('/ja/checkout/checkout-synthetic')
  await expect(page.getByText(/支払結果は未確認/)).toBeVisible()
  await expect(page.getByText('checkout-synthetic')).toBeVisible()
  await expect(page.getByText(/TWD 1260/)).toBeVisible()
  await page.getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\/checkout\/checkout-synthetic$/)
  await expect(page.getByText(/The payment result is unconfirmed/)).toBeVisible()
  await expect(page.getByText('checkout-synthetic')).toBeVisible()
  await expect(page.getByText(/TWD 1260/)).toBeVisible()
})

test('search no-results state and query survive language switching', async ({ page }) => {
  await page.goto('/ja?q=none')
  await expect(page.getByText('該当する商品がありません。検索条件を変更してください。')).toBeVisible()
  await page.getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\?q=none$/)
  await expect(page.getByText('No products match these filters. Try changing your search.')).toBeVisible()
})

test('declined and successful payment states stay on the same checkout in both locales', async ({ page }) => {
  for (const scenario of [
    { id: 'checkout-failed', ja: '支払いを確認できませんでした。実際の支払はありません。', en: 'Payment was not approved. No real payment was made.' },
    { id: 'checkout-success', ja: '確認済み', en: 'Confirmed' },
  ]) {
    await page.goto(`/ja/checkout/${scenario.id}`)
    await expect(page.getByText(scenario.ja)).toBeVisible()
    await page.getByRole('link', { name: 'en' }).click()
    await expect(page).toHaveURL(new RegExp(`/en/checkout/${scenario.id}$`))
    await expect(page.getByText(scenario.en)).toBeVisible()
    await expect(page.getByText(scenario.id)).toBeVisible()
  }
})

test('private member and checkout pages remain excluded from search indexing', async ({ page }) => {
  await page.goto('/en/account')
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/)
})

test('member order status is localized without changing the order or TWD total', async ({ page }) => {
  await page.goto('/ja/account')
  await expect(page.getByText('支払待ち')).toBeVisible()
  await expect(page.getByText('order-synthetic')).toBeVisible()
  await page.getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\/account$/)
  await expect(page.getByText('Awaiting payment')).toBeVisible()
  await expect(page.getByText('order-synthetic')).toBeVisible()
  await expect(page.getByText(/TWD 1,260/)).toBeVisible()
})
