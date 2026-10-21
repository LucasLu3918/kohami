import { expect, test, type Page, type Route } from '@playwright/test'

const csrfToken = 'synthetic-demo-admin-csrf'
const adminSession = {
  state: 'AdminAuthenticated',
  mode: 'mock',
  userId: 'demo-merchandiser-1',
  permissions: ['catalog.read'],
  expiresAt: '2026-10-22T00:00:00Z',
}

const products = [
  {
    id: 'demo-linen-shirt',
    slug: 'linen-shirt',
    state: 'Published',
    translations: [
      { locale: 'zh-TW', name: '日系亞麻襯衫', description: '輕柔透氣的日常襯衫。', material: '亞麻混紡' },
      { locale: 'ja', name: 'リネンシャツ', description: '軽やかな日常のシャツ。', material: 'リネン混' },
      { locale: 'en', name: 'Linen shirt', description: 'An airy everyday shirt.', material: 'Linen blend' },
    ],
    media: [{ media: { url: '/demo-assets/products/demo-linen-shirt.png', alt: { value: 'Linen shirt' } } }],
    skus: [{ skuId: 'linen-shirt-natural-s', colorId: 'natural', garmentSize: 'S', priceTwd: 1280, stock: 4, availability: 'InStock' }],
  },
  {
    id: 'demo-pleated-skirt',
    slug: 'pleated-skirt',
    state: 'Published',
    translations: [{ locale: 'zh-TW', name: '柔軟百褶裙', description: '輕盈垂墜。', material: '聚酯纖維' }],
    media: [{ media: { url: '/demo-assets/products/demo-pleated-midi-skirt.png', alt: { value: 'Pleated skirt' } } }],
    skus: [{ skuId: 'pleated-skirt-black-m', colorId: 'black', garmentSize: 'M', priceTwd: 1480, stock: 0, availability: 'OutOfStock' }],
  },
]

type MockState = { session: typeof adminSession | null; loginAttempts: string[]; logoutCount: number; mutations: string[] }

async function installAdminApi(page: Page, initialSession: MockState['session'] = null) {
  const state: MockState = { session: initialSession, loginAttempts: [], logoutCount: 0, mutations: [] }
  await page.route('**/api/v1/**', async (route: Route) => {
    const url = new URL(route.request().url())
    const path = url.pathname
    const method = route.request().method()
    if (path === '/api/v1/auth/csrf') return route.fulfill({ json: { requestToken: csrfToken } })
    if (path === '/api/v1/auth/session' && method === 'GET') return route.fulfill({ json: state.session ?? { state: 'Anonymous', mode: 'mock', permissions: [] } })
    if (path === '/api/v1/auth/sessions' && method === 'POST') {
      const body = route.request().postDataJSON() as { email: string; password: string }
      state.loginAttempts.push(body.email)
      if (body.email !== 'merchandiser@example.test' || body.password !== 'demo-password') {
        return route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ status: 401, code: 'AUTHENTICATION_REQUIRED' }) })
      }
      state.session = adminSession
      return route.fulfill({ json: adminSession })
    }
    if (path === '/api/v1/auth/session' && method === 'DELETE') {
      state.logoutCount += 1
      state.session = null
      return route.fulfill({ status: 204, body: '' })
    }
    if (path === '/api/v1/admin/products' && method === 'GET') {
      if (!state.session) return route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ status: 401, code: 'AUTHENTICATION_REQUIRED' }) })
      return route.fulfill({ json: { mode: 'mock', items: products } })
    }
    if (path.startsWith('/api/v1/admin/products/') && method === 'GET') {
      if (!state.session) return route.fulfill({ status: 401, contentType: 'application/problem+json', body: JSON.stringify({ status: 401, code: 'AUTHENTICATION_REQUIRED' }) })
      const product = products.find(item => item.id === decodeURIComponent(path.split('/').at(-1) || ''))
      return product
        ? route.fulfill({ json: product })
        : route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'RESOURCE_NOT_FOUND' }) })
    }
    if (path.startsWith('/api/v1/admin/products') && method !== 'GET') {
      state.mutations.push(`${method} ${path}`)
      return route.fulfill({ status: 403, contentType: 'application/problem+json', body: JSON.stringify({ status: 403, code: 'PERMISSION_CHANGED' }) })
    }
    return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'RESOURCE_NOT_FOUND' }) })
  })
  return state
}

async function waitForAdminLoginReady(page: Page) {
  await expect(page.locator('form[data-admin-hydrated="true"]')).toBeVisible()
}

test('anonymous users are redirected to the admin login with their destination preserved', async ({ page }) => {
  await installAdminApi(page)
  await page.goto('/admin/catalog')

  await expect(page).toHaveURL(/\/admin\/login\?next=(%2Fadmin%2Fcatalog|\/admin\/catalog)/)
  await waitForAdminLoginReady(page)
  await expect(page.getByRole('heading', { name: '管理者登入' })).toBeVisible()
  await expect(page.locator('a.skip-link')).toHaveAttribute('href', '#main-content')
  await expect(page.locator('main#main-content')).toHaveCount(1)
})

test('catalog-only admin is redirected away from noncatalog admin routes', async ({ page }) => {
  await installAdminApi(page, adminSession)
  await page.goto('/admin/reports')

  await expect(page).toHaveURL(/\/admin\/catalog$/)
  await expect(page.getByRole('heading', { name: '商品目錄' })).toBeVisible()
  await expect(page.getByRole('heading', { name: '報表' })).toHaveCount(0)
})

test('password-only admin login opens a read-only catalog and logout revokes the session', async ({ page }) => {
  const state = await installAdminApi(page)
  await page.goto('/admin/login?next=%2Fadmin%2Fcatalog')
  await waitForAdminLoginReady(page)
  await page.getByLabel('電子郵件').fill('merchandiser@example.test')
  await page.getByLabel('密碼').fill('demo-password')
  await page.getByRole('button', { name: '登入管理後台' }).click()

  await expect(page).toHaveURL(/\/admin\/catalog$/)
  await expect(page.getByRole('heading', { name: '商品目錄' })).toBeVisible()
  await expect(page.getByText('日系亞麻襯衫')).toBeVisible()
  await expect(page.getByText('NT$ 1,280')).toBeVisible()
  await expect(page.getByRole('link', { name: '查看商品 日系亞麻襯衫' }).first()).toBeVisible()
  await expect(page.getByRole('button', { name: /建立|新增|儲存|發布|下架|調整庫存/ })).toHaveCount(0)

  await page.getByRole('link', { name: '查看商品 日系亞麻襯衫' }).first().click()
  await expect(page).toHaveURL(/\/admin\/catalog\/demo-linen-shirt$/)
  await expect(page.getByRole('heading', { name: '日系亞麻襯衫' })).toBeVisible()
  await expect(page.getByText('linen-shirt-natural-s')).toBeVisible()
  await expect(page.getByRole('row').filter({ hasText: 'linen-shirt-natural-s' })).toContainText('4 件')
  await expect(page.getByRole('button', { name: /建立|新增|儲存|發布|下架|調整庫存/ })).toHaveCount(0)

  await page.getByRole('button', { name: '登出' }).click()
  await expect(page).toHaveURL(/\/admin\/login$/)
  expect(state.logoutCount).toBe(1)
  expect(state.mutations).toEqual([])
})

test('invalid credentials remain on the login page and do not open the protected catalog', async ({ page }) => {
  const state = await installAdminApi(page)
  await page.goto('/admin/login')
  await waitForAdminLoginReady(page)
  await page.getByLabel('電子郵件').fill('merchandiser@example.test')
  await page.getByLabel('密碼').fill('incorrect')
  await page.getByRole('button', { name: '登入管理後台' }).click()

  await expect(page.getByRole('alert')).toContainText('帳號或密碼不正確')
  await expect(page).toHaveURL(/\/admin\/login$/)
  expect(state.loginAttempts).toEqual(['merchandiser@example.test'])
})

test('direct API write attempts are denied for the catalog read demo identity', async ({ page }) => {
  const state = await installAdminApi(page, adminSession)
  await page.goto('/admin/catalog')
  await expect(page.getByRole('heading', { name: '商品目錄' })).toBeVisible()

  const response = await page.evaluate(async () => {
    const csrf = await fetch('/api/v1/auth/csrf').then(result => result.json()) as { requestToken: string }
    return fetch('/api/v1/admin/products', {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-csrf-token': csrf.requestToken, 'idempotency-key': 'synthetic-write-denial' },
      body: JSON.stringify({}),
    }).then(result => result.status)
  })
  expect(response).toBe(403)
  expect(state.mutations).toEqual(['POST /api/v1/admin/products'])
})
