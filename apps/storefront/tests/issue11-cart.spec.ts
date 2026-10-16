import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { appendFileSync } from 'node:fs'

type ProductSku = {
  slug: string
  translations: Array<{ locale: string; name: string }>
  skuId: string
  colorId: string
  garmentSize: string
  priceTwd: number
  availability: string
}
type Cart = { id: string; version: string; items: Array<{ skuId: string; quantity: number; unitPriceTwd: number }> }

function changeFixtureInventory(skuId: string, change: 'price' | 'stock') {
  const dotnet = process.env.KOHAMI_ISSUE11_DOTNET
  const commerce = process.env.KOHAMI_ISSUE11_COMMERCE_DLL
  const config = process.env.KOHAMI_ISSUE11_CONFIG_PATH
  const state = process.env.KOHAMI_ISSUE11_INVENTORY_STATE_PATH
  if (!dotnet || !commerce || !config || !state) throw new Error('ISSUE11_FIXTURE_MUTATION_CONFIG_MISSING')
  execFileSync(dotnet, [commerce, 'issue11-inventory-change', config, skuId, change, state], { stdio: 'ignore' })
}

function restoreFixtureInventory() {
  const dotnet = process.env.KOHAMI_ISSUE11_DOTNET
  const commerce = process.env.KOHAMI_ISSUE11_COMMERCE_DLL
  const config = process.env.KOHAMI_ISSUE11_CONFIG_PATH
  const state = process.env.KOHAMI_ISSUE11_INVENTORY_STATE_PATH
  if (dotnet && commerce && config && state) {
    execFileSync(dotnet, [commerce, 'issue11-inventory-restore', config, state], { stdio: 'ignore' })
  }
}

test.afterEach(async ({ page }) => {
  const evidencePath = process.env.KOHAMI_ISSUE11_CART_EVIDENCE_PATH
  try {
    if (evidencePath) {
      const response = await page.context().request.get('https://localhost:3000/api/v1/carts/current')
      if (response.ok()) {
        const cart = await response.json() as Cart
        appendFileSync(evidencePath, `${JSON.stringify({ cartId: cart.id })}\n`, { mode: 0o600 })
      }
    }
  } finally {
    restoreFixtureInventory()
  }
})

test('Issue11 shopper selects a SKU, reviews a server quote, updates and removes the cart line', async ({ page, request }) => {
  test.setTimeout(20_000)
  page.on('pageerror', error => console.log(`BROWSER_PAGE_ERROR ${error.name}`))
  page.on('response', async response => {
    if (new URL(response.url()).pathname.startsWith('/api/v1/')) {
      let code = ''
      if (response.status() >= 400) {
        try { code = String((await response.json()).code || '') } catch { code = 'non-json' }
      }
      console.log(`BROWSER_API ${response.status()} ${response.request().method()} ${new URL(response.url()).pathname} ${code}`)
    }
  })
  const response = await request.get('/api/v1/products?availability=in_stock&limit=100&locale=zh-TW')
  expect(response.ok()).toBeTruthy()
  const products = (await response.json() as { items: ProductSku[] }).items
  const sku = products.find(item => item.availability === 'InStock')
  expect(sku).toBeTruthy()

  await page.setViewportSize({ width: 390, height: 844 })
  const cartCreated = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/carts' && response.status() === 201)
  await page.goto(`/products/${sku!.slug}?lang=zh-TW`)
  await cartCreated
  const color = page.getByRole('combobox', { name: '顏色' })
  const size = page.getByRole('combobox', { name: '服裝尺寸' })
  const add = page.getByRole('button', { name: '加入購物車' })
  await expect(color).toHaveValue('')
  await expect(size).toHaveValue('')
  await expect(add).toBeDisabled()
  await color.selectOption(sku!.colorId)
  await expect(color).toHaveValue(sku!.colorId)
  await expect(size).toBeEnabled()
  await size.selectOption(sku!.garmentSize)
  await expect(add).toBeEnabled()
  await add.click()
  await expect(page.getByRole('status')).toContainText('已加入購物車')
  await page.getByRole('status').getByRole('link', { name: '查看購物車' }).click()

  await expect(page.locator('h1')).toHaveText('購物車明細')
  const line = page.getByRole('listitem').filter({ hasText: sku!.skuId })
  await expect(line).toContainText(sku!.translations.find(item => item.locale === 'zh-TW')?.name || sku!.skuId)
  await expect(line).toContainText(sku!.colorId)
  await expect(line).toContainText(sku!.garmentSize)
  await expect(line).toContainText(`TWD ${sku!.priceTwd}`)
  await page.getByRole('textbox', { name: '優惠碼' }).fill('DEMO15')
  await page.getByRole('button', { name: '套用優惠碼' }).click()
  await expect(page.getByRole('status')).toContainText('優惠已套用')

  const quantity = line.getByRole('spinbutton', { name: '數量' })
  await quantity.fill('2')
  await quantity.press('Tab')
  await expect(line).toContainText(`TWD ${sku!.priceTwd * 2}`)
  const quoteResponsePromise = page.waitForResponse(item =>
    item.request().method() === 'POST' && new URL(item.url()).pathname === '/api/v1/checkout-quotes',
  )
  await page.getByRole('button', { name: '重新試算費用' }).click()
  const quoteResponse = await quoteResponsePromise
  expect(quoteResponse.status()).toBe(201)
  const quote = await quoteResponse.json() as { snapshot: { currency: string; merchandiseTwd: number; discountTwd: number; shippingTwd: number; totalTwd: number } }
  const summary = page.getByRole('region', { name: '費用明細' })
  await expect(summary).toContainText(`TWD ${quote.snapshot.merchandiseTwd}`)
  await expect(summary).toContainText(`TWD ${quote.snapshot.discountTwd}`)
  await expect(summary).toContainText(`TWD ${quote.snapshot.shippingTwd}`)
  await expect(summary).toContainText(`TWD ${quote.snapshot.totalTwd}`)
  expect(quote.snapshot.currency).toBe('TWD')
  expect(quote.snapshot.discountTwd).toBe(Math.floor(quote.snapshot.merchandiseTwd * 0.15))
  expect(quote.snapshot.totalTwd).toBe(quote.snapshot.merchandiseTwd - quote.snapshot.discountTwd + quote.snapshot.shippingTwd)
  await expect(summary.getByText('購物車尚未保留庫存')).toBeVisible()

  await line.getByRole('button', { name: '移除商品' }).click()
  await expect(page.getByText('購物車目前是空的。')).toBeVisible()
})

test('Issue11 cart removal uses the current ETag and replays an identical idempotent request', async ({ page, request }) => {
  test.setTimeout(20_000)
  const productsResponse = await request.get('/api/v1/products?availability=in_stock&limit=100&locale=zh-TW')
  const sku = (await productsResponse.json() as { items: ProductSku[] }).items.find(item => item.availability === 'InStock')!
  const cartCreated = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/carts' && response.status() === 201)
  await page.goto(`/products/${sku.slug}?lang=zh-TW`)
  await cartCreated
  await page.getByRole('combobox', { name: '顏色' }).selectOption(sku.colorId)
  await page.getByRole('combobox', { name: '服裝尺寸' }).selectOption(sku.garmentSize)
  await page.getByRole('button', { name: '加入購物車' }).click()
  await expect(page.getByRole('status')).toContainText('已加入購物車')

  const api = page.context().request
  const cartResponse = await api.get('https://localhost:3000/api/v1/carts/current')
  expect(cartResponse.ok()).toBeTruthy()
  const cart = await cartResponse.json() as Cart
  expect(cart.items.some(item => item.skuId === sku.skuId)).toBeTruthy()
  const csrf = await (await api.get('https://localhost:3000/api/v1/auth/csrf')).json() as { requestToken: string }
  const key = `issue11-${randomUUID()}`
  const headers = { origin: 'https://localhost:3000', 'x-csrf-token': csrf.requestToken, 'idempotency-key': key, 'if-match': `"${cart.version}"` }
  const path = `https://localhost:3000/api/v1/carts/current/items/${encodeURIComponent(sku.skuId)}`
  const removed = await api.delete(path, { headers })
  expect(removed.status()).toBe(200)
  const result = await removed.json() as Cart
  expect(removed.headers().etag).toBe(`"${result.version}"`)
  expect(result.items.some(item => item.skuId === sku.skuId)).toBeFalsy()

  const replay = await api.delete(path, { headers })
  expect(replay.status()).toBe(200)
  expect(await replay.json()).toEqual(result)

  const stale = await api.delete(path, { headers: { ...headers, 'idempotency-key': `issue11-${randomUUID()}` } })
  expect(stale.status()).toBe(412)
  await page.goto('/cart?lang=zh-TW')
  await expect(page.getByText('購物車目前是空的。')).toBeVisible()
})

test('Issue11 quote rejects a stale SKU price and tells the shopper to review a new quote', async ({ page, request }) => {
  test.setTimeout(30_000)
  const response = await request.get('/api/v1/products?availability=in_stock&limit=100&locale=zh-TW')
  const sku = (await response.json() as { items: ProductSku[] }).items.find(item => item.availability === 'InStock')!
  const cartCreated = page.waitForResponse(item => item.request().method() === 'POST' && new URL(item.url()).pathname === '/api/v1/carts' && item.status() === 201)
  await page.goto(`/products/${sku.slug}?lang=zh-TW`)
  await cartCreated
  await page.getByRole('combobox', { name: '顏色' }).selectOption(sku.colorId)
  await page.getByRole('combobox', { name: '服裝尺寸' }).selectOption(sku.garmentSize)
  await page.getByRole('button', { name: '加入購物車' }).click()
  await expect(page.getByRole('status')).toContainText('已加入購物車')
  await page.goto('/cart?lang=zh-TW')
  changeFixtureInventory(sku.skuId, 'price')
  const quoteResponse = page.waitForResponse(item => item.request().method() === 'POST' && new URL(item.url()).pathname === '/api/v1/checkout-quotes')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  const staleQuote = await quoteResponse
  expect(staleQuote.status()).toBe(409)
  expect((await staleQuote.json()).code).toBe('QUOTE_CHANGED')
  await expect(page.getByRole('alert')).toContainText('商品價格已更新，請重新試算並確認新金額。')
})

test('Issue11 quote rejects a quantity above newly reduced stock without reserving inventory', async ({ page, request }) => {
  test.setTimeout(30_000)
  const response = await request.get('/api/v1/products?availability=in_stock&limit=100&locale=zh-TW')
  const sku = (await response.json() as { items: ProductSku[] }).items.find(item => item.availability === 'InStock' && item.priceTwd < 100_000)!
  const cartCreated = page.waitForResponse(item => item.request().method() === 'POST' && new URL(item.url()).pathname === '/api/v1/carts' && item.status() === 201)
  await page.goto(`/products/${sku.slug}?lang=zh-TW`)
  await cartCreated
  await page.getByRole('combobox', { name: '顏色' }).selectOption(sku.colorId)
  await page.getByRole('combobox', { name: '服裝尺寸' }).selectOption(sku.garmentSize)
  await page.getByRole('button', { name: '加入購物車' }).click()
  await expect(page.getByRole('status')).toContainText('已加入購物車')
  await page.goto('/cart?lang=zh-TW')
  const line = page.getByRole('listitem').filter({ hasText: sku.skuId })
  await line.getByRole('spinbutton', { name: '數量' }).fill('2')
  await line.getByRole('spinbutton', { name: '數量' }).press('Tab')
  await expect(line).toContainText(`TWD ${sku.priceTwd * 2}`)
  changeFixtureInventory(sku.skuId, 'stock')
  const quoteResponse = page.waitForResponse(item => item.request().method() === 'POST' && new URL(item.url()).pathname === '/api/v1/checkout-quotes')
  await page.getByRole('button', { name: '重新試算費用' }).click()
  const staleQuote = await quoteResponse
  expect(staleQuote.status()).toBe(409)
  expect((await staleQuote.json()).code).toBe('STOCK_CHANGED')
  await expect(page.getByRole('alert')).toContainText('可售庫存不足，請確認商品數量後重試。')
})
