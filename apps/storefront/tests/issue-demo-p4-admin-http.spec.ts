import { expect, test } from '@playwright/test'
import { readFileSync, lstatSync } from 'node:fs'

type AdminCredential = { mode: 'local-demo'; datasetId: 'demo_m0'; email: string; password: string }

function readCredential(): AdminCredential {
  const path = process.env.KOHAMI_ADMIN_CREDENTIALS_PATH
  if (!path) throw new Error('ADMIN_CREDENTIAL_PATH_REQUIRED')
  const stat = lstatSync(path)
  if (stat.isSymbolicLink() || !stat.isFile() || (stat.mode & 0o077) !== 0) throw new Error('ADMIN_CREDENTIAL_FILE_SCOPE_INVALID')
  const credential = JSON.parse(readFileSync(path, 'utf8')) as AdminCredential
  if (credential.mode !== 'local-demo' || credential.datasetId !== 'demo_m0' || !credential.email || !credential.password) {
    throw new Error('ADMIN_CREDENTIAL_SCOPE_INVALID')
  }
  return credential
}

test('real local-demo admin HTTP login, published product browsing, write denial, and session revocation', async ({ page }) => {
  const credential = readCredential()
  const anonymousRead = await page.request.get('/api/v1/admin/products')
  expect(anonymousRead.status()).toBe(401)

  await page.goto('/admin/catalog')
  await expect(page).toHaveURL(/\/admin\/login\?next=(%2Fadmin%2Fcatalog|\/admin\/catalog)/)
  await expect(page.locator('form[data-admin-hydrated="true"]')).toBeVisible()
  await page.getByLabel('電子郵件').fill(credential.email)
  await page.getByLabel('密碼').fill(credential.password)
  await page.getByRole('button', { name: '登入管理後台' }).click()

  await expect(page).toHaveURL(/\/admin\/catalog$/)
  await expect(page.getByRole('heading', { name: '商品目錄' })).toBeVisible()
  await expect(page.getByText('15 款商品')).toBeVisible()
  const productLink = page.getByRole('link', { name: /^查看商品 / }).first()
  await expect(productLink).toBeVisible()
  const productName = (await productLink.getAttribute('aria-label'))?.replace(/^查看商品 /, '')
  expect(productName).toBeTruthy()
  await expect(page.getByRole('button', { name: /建立|新增|儲存|發布|下架|調整庫存/ })).toHaveCount(0)
  await productLink.click()
  await expect(page).toHaveURL(/\/admin\/catalog\/[^/]+$/)
  await expect(page.getByRole('heading', { name: productName! })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'SKU 與庫存' })).toBeVisible()
  await expect(page.getByRole('button', { name: /建立|新增|儲存|發布|下架|調整庫存/ })).toHaveCount(0)

  const csrf = await page.request.get('/api/v1/auth/csrf').then(response => response.json()) as { requestToken: string }
  const writeAttempt = await page.request.post('/api/v1/admin/products', {
    data: {},
    headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': 'demo-admin-browser-write-denial' },
  })
  expect(writeAttempt.status()).toBe(403)
  const crossOriginLogout = await page.request.delete('/api/v1/auth/session', {
    headers: { Origin: 'https://localhost:3018', 'X-CSRF-TOKEN': csrf.requestToken },
  })
  expect(crossOriginLogout.status()).toBe(403)

  await page.getByRole('button', { name: '登出' }).click()
  await expect(page).toHaveURL(/\/admin\/login$/)
  expect((await page.request.get('/api/v1/admin/products')).status()).toBe(401)
})
