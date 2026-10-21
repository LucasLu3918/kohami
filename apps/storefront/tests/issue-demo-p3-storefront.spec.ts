import { expect, test } from '@playwright/test'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

const productTemplates = [
  {
    productId: 'product-linen-shirt', slug: 'linen-shirt', translations: [
      { locale: 'zh-TW', name: '亞麻荷葉袖襯衫', description: '輕柔亞麻混紡。' },
      { locale: 'ja', name: 'リネンフリルブラウス', description: '軽やかなリネンブレンド。' },
      { locale: 'en', name: 'Linen ruffle blouse', description: 'A soft linen blend.' },
    ], media: [{ media: { url: '/demo-assets/products/demo-linen-shirt.png', alt: { value: '淡米色亞麻荷葉袖襯衫' } } }],
    skuId: 'linen-shirt-oatmeal-s', colorId: 'oatmeal', garmentSize: 'S', priceTwd: 1280, availability: 'InStock', mode: 'mock',
  },
  {
    productId: 'product-pleated-skirt', slug: 'pleated-skirt', translations: [
      { locale: 'zh-TW', name: '霧玫瑰百褶長裙', description: '柔和垂墜剪裁。' },
      { locale: 'ja', name: 'ダスティローズプリーツスカート', description: '柔らかなドレープ。' },
      { locale: 'en', name: 'Dusty rose pleated skirt', description: 'A soft draped silhouette.' },
    ], media: [{ media: { url: '/demo-assets/products/demo-pleated-midi-skirt.png', alt: { value: '霧玫瑰百褶長裙' } } }],
    skuId: 'pleated-skirt-rose-m', colorId: 'dusty_rose', garmentSize: 'M', priceTwd: 1480, availability: 'OutOfStock', mode: 'mock',
  },
]
const products = Array.from({ length: 15 }, (_, index) => {
  const source = productTemplates[index % productTemplates.length]!
  if (index < productTemplates.length) return source
  return {
    ...source,
    productId: `${source.productId}-${index + 1}`,
    slug: `japanese-style-${index + 1}`,
    skuId: `${source.skuId}-${index + 1}`,
    translations: source.translations.map(translation => ({ ...translation, name: `${translation.name} ${index + 1}` })),
  }
})

const productDetail = {
  id: 'product-linen-shirt', slug: 'linen-shirt', mode: 'mock',
  translations: [
    { locale: 'zh-TW', name: '亞麻荷葉袖襯衫', description: '輕柔亞麻混紡。', material: '棉麻混紡' },
    { locale: 'ja', name: 'リネンフリルブラウス', description: '軽やかなリネンブレンド。', material: 'コットンリネン' },
    { locale: 'en', name: 'Linen ruffle blouse', description: 'A soft linen blend.', material: 'Cotton linen' },
  ],
  media: [{ media: { url: '/demo-assets/products/demo-linen-shirt.png', alt: { value: '淡米色亞麻荷葉袖襯衫' } } }],
  measurements: [],
  skus: [
    { skuId: 'linen-shirt-oatmeal-s', priceTwd: 1280, colorId: 'oatmeal', garmentSize: 'S', stock: 3, availability: 'InStock' },
    { skuId: 'linen-shirt-oatmeal-m', priceTwd: 1280, colorId: 'oatmeal', garmentSize: 'M', stock: 0, availability: 'OutOfStock' },
  ],
}

function contrastRatio(foreground: string, background: string) {
  const luminance = (color: string) => {
    const channels = color.match(/[\d.]+/g)?.slice(0, 3).map(Number)
    if (!channels || channels.length !== 3) throw new Error(`Cannot parse computed color: ${color}`)
    const [red, green, blue] = channels.map((channel) => {
      const srgb = channel / 255
      return srgb <= 0.04045 ? srgb / 12.92 : ((srgb + 0.055) / 1.055) ** 2.4
    })
    return 0.2126 * red! + 0.7152 * green! + 0.0722 * blue!
  }
  const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a)
  return (values[0]! + 0.05) / (values[1]! + 0.05)
}

test('DEMO-P3 storefront presents localized fashion catalog and real product/cart behavior on mobile', async ({ page }) => {
  let cart = { id: 'cart-p3', version: 'cart-v1', items: [] as Array<{ skuId: string; quantity: number; unitPriceTwd: number }> }
  let addedSku = ''

  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } }))
  await page.route('**/api/v1/auth/csrf', route => route.fulfill({ json: { requestToken: 'synthetic-csrf' } }))
  await page.route('**/api/v1/products?**', route => route.fulfill({ json: { items: products, nextCursor: null } }))
  await page.route('**/api/v1/products/linen-shirt', route => route.fulfill({ json: productDetail }))
  await page.route('**/api/v1/carts/current', route => route.fulfill({ json: cart }))
  await page.route('**/api/v1/carts/current/items/**', async route => {
    addedSku = decodeURIComponent(new URL(route.request().url()).pathname.split('/').at(-1) || '')
    cart = { ...cart, version: 'cart-v2', items: [{ skuId: addedSku, quantity: 1, unitPriceTwd: 1280 }] }
    await route.fulfill({ json: cart })
  })

  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/ja')
  await page.waitForLoadState('networkidle')
  await expect(page.locator('html')).toHaveAttribute('lang', 'ja')
  await expect(page.locator('.store-header__brand')).toBeVisible()
  await expect(page.locator('.store-header__quick-links a[href="/ja/cart"]')).toBeVisible()
  await expect(page.getByRole('region', { name: 'お知らせ' })).toContainText('ローカルデモ')
  await expect(page.getByRole('heading', { name: '毎日に、やさしい装いを。' })).toBeVisible()
  await expect(page.locator('.storefront-hero img')).toHaveAttribute('src', '/demo-assets/products/demo-shirt-dress.png')
  await expect(page.getByRole('navigation', { name: '商品カテゴリー' }).getByRole('link')).toHaveCount(4)
  await expect(page.getByRole('heading', { name: 'ログイン' })).toHaveCount(0)
  await expect(page.getByRole('heading', { name: '訂單配送與售後（模擬）' })).toHaveCount(0)
  await expect(page.getByRole('heading', { name: '產生唯讀 mock 對帳' })).toHaveCount(0)
  await expect(page.getByRole('heading', { name: '新着アイテム' })).toBeVisible()
  await expect(page.locator('.product-card')).toHaveCount(15)
  await expect(page.locator('.product-card img')).toHaveCount(15)
  await expect.poll(() => page.locator('.product-grid').evaluate(element => getComputedStyle(element).gridTemplateColumns.split(' ').length)).toBe(3)
  const mobileBrand = await page.locator('.store-header__brand').boundingBox()
  expect(mobileBrand).not.toBeNull()
  expect(Math.abs((mobileBrand!.x + mobileBrand!.width / 2) - 195)).toBeLessThan(2)
  await page.setViewportSize({ width: 1440, height: 1000 })
  await expect.poll(() => page.locator('.product-grid').evaluate(element => getComputedStyle(element).gridTemplateColumns.split(' ').length)).toBe(5)
  const desktopBrand = await page.locator('.store-header__brand').boundingBox()
  expect(desktopBrand).not.toBeNull()
  expect(Math.abs((desktopBrand!.x + desktopBrand!.width / 2) - 720)).toBeLessThan(2)
  await page.setViewportSize({ width: 390, height: 844 })
  if (process.env.KOHAMI_P3_VISUAL_REVIEW_PATH) {
    await page.screenshot({ path: process.env.KOHAMI_P3_VISUAL_REVIEW_PATH, fullPage: true })
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.screenshot({ path: process.env.KOHAMI_P3_VISUAL_REVIEW_PATH.replace(/\.png$/, '-desktop.png') })
    await page.setViewportSize({ width: 390, height: 844 })
  }
  await page.getByRole('navigation', { name: '商品カテゴリー' }).getByRole('link').first().click()
  await expect(page).toHaveURL(/\/ja\?q=/)
  await page.goto('/ja')
  await page.waitForLoadState('networkidle')
  await expect(page.getByRole('link', { name: 'リネンフリルブラウス' }).first()).toHaveAttribute('href', '/ja/products/linen-shirt')
  await expect(page.locator('.product-card').filter({ hasText: 'ダスティローズプリーツスカート' }).getByRole('button').first()).toBeDisabled()
  const firstCard = page.locator('.product-card').first()
  await expect(firstCard.getByRole('link').first()).toHaveAccessibleName(/商品とサイズ表を見る.*リネンフリルブラウス/)
  await firstCard.getByLabel('色と衣服サイズ: リネンフリルブラウス').selectOption('linen-shirt-oatmeal-s')
  await expect(page).toHaveURL(/sku\.linen-shirt=linen-shirt-oatmeal-s/)
  await expect(firstCard.getByRole('button', { name: 'カートに追加' })).toBeEnabled()
  const cardAddResponse = page.waitForResponse(response => response.request().method() === 'PUT' && new URL(response.url()).pathname === '/api/v1/carts/current/items/linen-shirt-oatmeal-s')
  await firstCard.getByRole('button', { name: 'カートに追加' }).click()
  await cardAddResponse
  expect(addedSku).toBe('linen-shirt-oatmeal-s')

  await page.locator('.store-header').getByRole('link', { name: 'カート' }).click()
  await expect(page).toHaveURL('/ja/cart')
  await expect(page.getByRole('heading', { name: 'カート明細', level: 1 })).toBeVisible()
  await page.goto('/ja')
  await page.waitForLoadState('networkidle')

  await page.getByRole('link', { name: 'リネンフリルブラウス' }).first().click()
  await expect(page).toHaveURL(/\/ja\/products\/linen-shirt$/)
  await expect(page.locator('.product-page h1')).toHaveText('リネンフリルブラウス')
  await expect(page.locator('.product-layout > img')).toHaveAttribute('src', productDetail.media[0].media.url)
  await expect(page.getByText(/販売可能在庫 3/)).toBeVisible()
  await expect(page.locator('.sku-list li')).toHaveCount(2)
  await page.setViewportSize({ width: 1280, height: 900 })
  expect(await page.locator('.product-layout').evaluate(element => getComputedStyle(element).gridTemplateColumns.split(' ').length)).toBe(2)
  await page.setViewportSize({ width: 390, height: 844 })
  expect(await page.locator('.product-layout').evaluate(element => getComputedStyle(element).gridTemplateColumns.split(' ').length)).toBe(1)
  await page.getByLabel('色').selectOption('oatmeal')
  await page.getByLabel('服のサイズ').selectOption('S')
  await page.getByRole('button', { name: 'カートに追加' }).click()
  await expect(page.getByRole('status')).toContainText('カートに追加しました')
  expect(addedSku).toBe('linen-shirt-oatmeal-s')
  await expect(page.locator('.store-header__brand')).toBeVisible()
  await page.locator('.store-header').getByRole('link', { name: 'en' }).click()
  await expect(page).toHaveURL(/\/en\/products\/linen-shirt\?color=oatmeal&size=S$/)
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
  await expect(page.locator('.product-page h1')).toHaveText('Linen ruffle blouse')
  await page.locator('.store-header').getByRole('link', { name: 'zh-TW' }).click()
  await expect(page).toHaveURL(/\/zh-TW\/products\/linen-shirt\?color=oatmeal&size=S$/)
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-TW')
  await expect(page.locator('.product-page h1')).toHaveText('亞麻荷葉袖襯衫')

  const dimensions = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    document: document.documentElement.scrollWidth,
  }))
  expect(dimensions.document).toBeLessThanOrEqual(dimensions.viewport)

  for (const selector of ['.store-footer p', '.store-footer small']) {
    const text = page.locator(selector)
    await text.scrollIntoViewIfNeeded()
    const colors = await text.evaluate((element) => ({
      foreground: getComputedStyle(element).color,
      background: getComputedStyle(element.closest('.store-footer')!).backgroundColor,
    }))
    expect(contrastRatio(colors.foreground, colors.background), `${selector} must meet WCAG AA normal-text contrast`).toBeGreaterThanOrEqual(4.5)
  }
})
