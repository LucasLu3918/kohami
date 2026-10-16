import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { randomUUID } from 'node:crypto'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

type SessionArtifact = { sessionCookie: string; browserCookie: string; userId: string }
type StockView = { skuId: string; onHand: number; reserved: number; sellableStock: number; availability: string }

 test('Issue09 Owner adjusts sellable stock and the public product reflects out of stock', async ({ page }) => {
  const artifactPath = process.env.KOHAMI_ISSUE09_SESSION_ARTIFACT
  if (!artifactPath) throw new Error('ISSUE09_SESSION_ARTIFACT_REQUIRED')
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as SessionArtifact
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
  ])

  const api = page.context().request
  const apiBase = 'https://localhost:3000/api/v1'
  const skuId = 'demo-linen-shirt-oatmeal-s'
  const slug = 'demo-linen-shirt'
  const stockPath = `${apiBase}/admin/stock/${encodeURIComponent(skuId)}`
  const stockResponse = await api.get(stockPath)
  const stockPayload = await stockResponse.json() as StockView & { code?: string }
  expect(stockResponse.status(), `stock query returned ${stockPayload.code || 'no-code'}`).toBe(200)
  const original = stockPayload
  expect(original.sellableStock).toBeGreaterThan(0)

  const csrfResponse = await api.get(`${apiBase}/auth/csrf`)
  expect(csrfResponse.status()).toBe(200)
  const csrf = (await csrfResponse.json() as { requestToken: string }).requestToken
  const adjust = async (quantityDelta: number, reason: string) => api.post(`${apiBase}/admin/stock-adjustments`, {
    headers: {
      origin: 'https://localhost:3000',
      'x-csrf-token': csrf,
      'idempotency-key': `issue09-${randomUUID()}`,
    },
    data: { skuId, quantityDelta, reason },
  })

  await page.goto('/admin/catalog')
  await expect(page.getByRole('heading', { name: '商品目錄管理' })).toBeVisible()
  const products = await (await api.get(`${apiBase}/products`)).json() as { items: Array<{ productId: string; skuId: string }> }
  const productId = products.items.find(item => item.skuId === skuId)?.productId
  expect(productId).toBeTruthy()
  await page.getByLabel('已授權商品 ID').fill(productId!)
  await page.getByRole('button', { name: '載入草稿' }).click()
  await expect(page.getByText(new RegExp(`已載入 ${productId}（Published）。`))).toBeVisible()
  await page.getByRole('button', { name: '讀取庫存與異動' }).click()
  await expect(page.locator('dt').filter({ hasText: '可售' }).locator('xpath=following-sibling::dd')).toHaveText(`${original.sellableStock}（有庫存）`)

  try {
    await page.getByLabel('異動數量（正數增加，負數減少）').fill(String(-original.sellableStock))
    await page.getByLabel('異動原因').fill('Issue09 browser acceptance: mark showcase SKU out of stock')
    await page.getByRole('button', { name: '記錄庫存異動' }).click()
    await expect(page.getByRole('status').first()).toContainText('庫存異動')

    const unavailable = await (await api.get(stockPath)).json() as StockView
    expect(unavailable).toMatchObject({ sellableStock: 0, availability: 'OutOfStock', reserved: original.reserved })
    await page.goto(`/products/${slug}`)
    await expect(page.locator('.sku-list li').filter({ hasText: '缺貨' })).toContainText('可售庫存 0')
  } finally {
    const current = await api.get(stockPath)
    if (current.ok()) {
      const actual = await current.json() as StockView
      const delta = original.sellableStock - actual.sellableStock
      if (delta !== 0) {
        const restored = await adjust(delta, 'Issue09 browser acceptance cleanup: restore original sellable stock')
        expect(restored.status()).toBe(201)
      }
    }
  }
})
