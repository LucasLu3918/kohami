import { expect, test, type Page } from '@playwright/test'
import { chmod, writeFile } from 'node:fs/promises'

type Evidence = {
  userId: string
  email: string
  skuId: string
  cartId: string
  quoteId: string
  checkoutId: string
  orderId: string
  totalTwd: number
}

const required = (name: string) => {
  const value = process.env[name]
  if (!value) throw new Error(`P5_ACCEPTANCE_ENV_REQUIRED:${name}`)
  return value
}

test('synthetic member signs in, pays from wallet, and receives the confirmed real API order', async ({ page }) => {
  const email = required('KOHAMI_P5_MEMBER_EMAIL')
  const password = required('KOHAMI_P5_MEMBER_PASSWORD')
  const userId = required('KOHAMI_P5_MEMBER_ID')
  const skuId = required('KOHAMI_P5_SKU')
  const evidencePath = required('KOHAMI_P5_EVIDENCE_PATH')

  let cartId = ''
  let quoteId = ''
  let checkoutId = ''
  let orderId = ''
  let totalTwd = 0
  page.on('response', async response => {
    if (!response.url().includes('/api/v1/')) return
    try {
      const body = await response.json() as Record<string, unknown>
      if (response.url().endsWith('/api/v1/carts/current') && response.request().method() === 'GET') {
        cartId = String(body.id || '')
      }
      if (response.url().endsWith('/api/v1/checkout-quotes') && response.request().method() === 'POST') {
        quoteId = String(body.id || '')
        totalTwd = Number((body.snapshot as { totalTwd?: number } | undefined)?.totalTwd || 0)
      }
      if (response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST' && response.status() === 201) {
        checkoutId = String(body.checkoutId || '')
        orderId = String(body.orderId || '')
      }
    } catch {
      // Non-JSON responses do not contribute evidence.
    }
  })

  const initialSession = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/session') && response.request().method() === 'GET')
  await page.goto('/en/account?returnTo=%2Fen%2Fcart')
  await initialSession
  await page.waitForLoadState('networkidle')
  const emailInput = page.getByLabel('Email', { exact: true })
  const passwordInput = page.getByLabel('Password', { exact: true })
  const signIn = page.getByRole('button', { name: 'Sign in' })
  await emailInput.fill(email)
  await passwordInput.fill(password)
  await expect(signIn).toBeEnabled()
  await signIn.click()
  await expect(page).toHaveURL(/\/en\/cart$/)
  await expect(page.locator('main h1')).toHaveText('Cart details')

  await page.goto('/en')
  const catalog = page.getByRole('region', { name: 'New arrivals' })
  await catalog.getByRole('searchbox', { name: 'Search product name, SKU or description' }).fill(skuId)
  await catalog.getByRole('button', { name: 'Search products', exact: true }).click()
  const product = catalog.getByRole('article').filter({ has: page.locator(`option[value="${skuId}"]`) })
  await expect(product).toHaveCount(1)
  await product.getByRole('combobox').selectOption(skuId)
  const cartUpdate = page.waitForResponse(response => response.url().includes(`/api/v1/carts/current/items/${encodeURIComponent(skuId)}`) && response.request().method() === 'PUT')
  await product.getByRole('button', { name: 'Add to cart' }).click()
  const updatedCart = await cartUpdate
  expect(updatedCart.status()).toBe(200)
  const updatedCartBody = await updatedCart.json() as { items?: Array<{ skuId?: string }> }
  expect(updatedCartBody.items?.some(item => item.skuId === skuId)).toBe(true)

  await page.goto('/en/cart')
  await expect(page.locator('.cart-line').filter({ hasText: skuId })).toHaveCount(1)
  await page.getByRole('button', { name: 'Recalculate fees' }).click()
  await expect(page.getByRole('region', { name: 'Fee summary' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Member wallet payment' })).toBeVisible()
  await expect(page.getByText(/Available member balance: TWD/)).toBeVisible()
  await page.getByLabel('Contact email').fill(email)
  await page.getByRole('checkbox', { name: /I confirm this quote and authorize the synthetic member wallet/ }).check()
  const checkoutResponse = page.waitForResponse(response => response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Create wallet order and pay' }).click()
  expect((await checkoutResponse).status()).toBe(201)

  await expect(page).toHaveURL(/\/en\/checkout\/[^/]+$/)
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  await expect(page.getByText('Mock success', { exact: true })).toBeVisible()
  expect([cartId, quoteId, checkoutId, orderId].every(Boolean)).toBe(true)
  expect(totalTwd).toBeGreaterThan(0)

  const evidence: Evidence = { userId, email, skuId, cartId, quoteId, checkoutId, orderId, totalTwd }
  await writeFile(evidencePath, `${JSON.stringify(evidence)}\n`, { mode: 0o600 })
  await chmod(evidencePath, 0o600)
})
