import { expect, test } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'
import { randomUUID } from 'node:crypto'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

type SessionArtifact = { sessionCookie: string; browserCookie: string; userId: string }

test('Issue08 Catalog save failure is sanitized and leaves no partial Product', async ({ page }) => {
  const artifactPath = process.env.KOHAMI_ISSUE08_SESSION_ARTIFACT
  const evidencePath = process.env.KOHAMI_ISSUE08_FAILURE_EVIDENCE_PATH
  if (!artifactPath || !evidencePath) throw new Error('ISSUE08_FAILURE_ENVIRONMENT_INCOMPLETE')
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as SessionArtifact
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
  ])
  await page.goto('/admin/catalog')
  const api = page.context().request
  const apiBase = 'https://localhost:3000/api/v1'
  const csrfResponse = await api.get(`${apiBase}/auth/csrf`)
  expect(csrfResponse.status()).toBe(200)
  const csrf = (await csrfResponse.json() as { requestToken: string }).requestToken
  const suffix = randomUUID().replaceAll('-', '')
  const slug = `issue08-fail-${suffix}`
  const skuId = `${slug}-demo-black-m`
  const key = `issue08-fail-${suffix}`
  const body = {
    slug,
    translations: [
      { locale: 'zh-TW', name: '故障注入合成商品', description: '合成驗收資料。', material: '合成材質' },
      { locale: 'ja', name: '故障注入デモ商品', description: '合成驗收資料。', material: '合成素材' },
      { locale: 'en', name: 'Failure injection demo product', description: 'Synthetic acceptance data.', material: 'Synthetic fabric' },
    ],
    skus: [{ id: skuId, colorId: 'demo-black', garmentSize: 'M', unitPriceTwd: 1200, active: true }],
    media: [{ media: { id: 'neutral-demo-image', url: '/demo-assets/neutral.svg', alt: { value: '合成展示占位圖', requestedLocale: 'zh-TW', sourceLocale: 'zh-TW', revision: 'demo-v1', fallback: false }, synthetic: true, revision: 'demo-v1' }, sourceRightsRef: 'synthetic_owned_demo' }],
    measurements: [{ name: 'demo-length', valueMm: 500, synthetic: true }],
  }
  const response = await api.post(`${apiBase}/admin/products`, {
    headers: { origin: 'https://localhost:3000', 'x-csrf-token': csrf, 'idempotency-key': key },
    data: body,
  })
  expect(response.status()).toBe(503)
  const payload = await response.json() as { code?: string; status?: number }
  expect(payload).toMatchObject({ code: 'SERVICE_UNAVAILABLE', status: 503 })
  expect(JSON.stringify(payload)).not.toMatch(/SqlException|SQL Server|ISSUE08_INJECTED|CatalogProducts|CatalogProductSkus/i)
  writeFileSync(evidencePath, JSON.stringify({ userId: artifact.userId, slug, skuId, key }) + '\n', { mode: 0o600 })
})

test('Issue08 Catalog public read failure is sanitized and returns no partial product', async ({ request }) => {
  const evidencePath = process.env.KOHAMI_ISSUE08_EVIDENCE_PATH
  if (!evidencePath) throw new Error('ISSUE08_EVIDENCE_PATH_REQUIRED')
  const evidence = JSON.parse(readFileSync(evidencePath, 'utf8')) as { slug: string }
  const response = await request.get(`https://localhost:3000/api/v1/products/${evidence.slug}`)
  expect(response.status()).toBe(503)
  const text = await response.text()
  const payload = JSON.parse(text) as { code?: string; status?: number }
  expect(payload).toMatchObject({ code: 'SERVICE_UNAVAILABLE', status: 503 })
  expect(text).not.toContain(evidence.slug)
  expect(text).not.toMatch(/SqlException|SQL Server|permission denied|CatalogProductSkus/i)
})

test('Issue08 Owner publishes, updates and unpublishes a synthetic Catalog product', async ({ page, browser }) => {
  const artifactPath = process.env.KOHAMI_ISSUE08_SESSION_ARTIFACT
  const evidencePath = process.env.KOHAMI_ISSUE08_EVIDENCE_PATH
  if (!artifactPath || !evidencePath) throw new Error('ISSUE08_BROWSER_ENVIRONMENT_INCOMPLETE')
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as SessionArtifact
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
  ])

  await page.goto('/admin/catalog')
  await expect(page.getByRole('heading', { name: '商品目錄管理' })).toBeVisible()
  const api = page.context().request
  const apiBase = 'https://localhost:3000/api/v1'
  const csrfResponse = await api.get(`${apiBase}/auth/csrf`)
  expect(csrfResponse.status()).toBe(200)
  const csrf = (await csrfResponse.json() as { requestToken: string }).requestToken
  const suffix = randomUUID().replaceAll('-', '')
  const slug = `issue08-${suffix}`
  const skuId = `${slug}-demo-black-m`
  const body = {
    slug,
    translations: [
      { locale: 'zh-TW', name: 'Issue 08 合成上衣', description: '合成驗收資料。', material: '合成棉料' },
      { locale: 'ja', name: 'Issue 08 デモトップス', description: '合成驗收資料。', material: '合成素材' },
      { locale: 'en', name: 'Issue 08 synthetic top', description: 'Synthetic acceptance data.', material: 'Synthetic fabric' },
    ],
    skus: [{ id: skuId, colorId: 'demo-black', garmentSize: 'M', unitPriceTwd: 1200, active: true }],
    media: [{
      media: {
        id: 'neutral-demo-image', url: '/demo-assets/neutral.svg',
        alt: { value: '合成展示占位圖，非實物照片', requestedLocale: 'zh-TW', sourceLocale: 'zh-TW', revision: 'demo-v1', fallback: false },
        synthetic: true, revision: 'demo-v1',
      },
      sourceRightsRef: 'synthetic_owned_demo',
    }],
    measurements: [{ name: 'demo-length', valueMm: 500, synthetic: true }],
  }
  const writeHeaders = (key: string, etag?: string) => ({
    origin: 'https://localhost:3000', 'x-csrf-token': csrf,
    'idempotency-key': key, ...(etag ? { 'if-match': etag } : {}),
  })

  const anonymous = await browser.newContext({ ignoreHTTPSErrors: true })
  try {
    const denied = await anonymous.request.post(`${apiBase}/admin/products`, {
      headers: { origin: 'https://localhost:3000', 'content-type': 'application/json', 'idempotency-key': `issue08-anon-${suffix}` },
      data: body,
    })
    expect([401, 403]).toContain(denied.status())
  } finally {
    await anonymous.close()
  }

  await page.getByLabel('繁體中文商品名稱').fill('Issue 08 合成上衣')
  await page.getByLabel('商品網址代稱').fill(slug)
  await page.getByLabel('SKU 顏色代碼').fill('demo-black')
  await page.getByLabel('服裝尺寸').fill('M')
  await page.getByLabel('新台幣單價').fill('1200')
  const createResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v1/admin/products')
  await page.getByRole('button', { name: '建立商品草稿' }).click()
  const created = await createResponse
  const draft = await created.json() as { id: string; state: string }
  expect(created.status(), `Owner create returned HTTP ${created.status()} (${(draft as { code?: string }).code || 'no-code'})`).toBe(201)
  expect(draft.state).toBe('Draft')
  writeFileSync(evidencePath, JSON.stringify({ userId: artifact.userId, productId: draft.id, skuId, slug, finalPriceTwd: 1550 }) + '\n', { mode: 0o600 })

  const duplicate = await api.post(`${apiBase}/admin/products`, {
    headers: writeHeaders(`issue08-duplicate-${suffix}`), data: { ...body, skus: [{ ...body.skus[0], id: `${skuId}-duplicate` }] },
  })
  expect(duplicate.status()).toBe(409)

  const hiddenDraft = await api.get(`${apiBase}/products/${slug}`)
  expect(hiddenDraft.status()).toBe(404)
  const publishResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === `/api/v1/admin/products/${draft.id}/publication-commands`)
  await page.getByRole('button', { name: '發布', exact: true }).click()
  const publish = await publishResponse
  expect(publish.status()).toBe(200)

  const firstPublic = await api.get(`${apiBase}/products/${slug}`)
  expect(firstPublic.status()).toBe(200)
  const firstBody = await firstPublic.json()
  expect(firstBody.skus[0]).toMatchObject({ skuId, priceTwd: 1200 })
  expect(JSON.stringify(firstBody)).not.toContain('sourceRightsRef')
  expect(JSON.stringify(firstBody)).not.toContain('synthetic_owned_demo')

  await page.goto(`/products/${slug}`)
  await expect(page.getByRole('heading', { name: 'Issue 08 合成上衣' })).toBeVisible()
  await expect(page.getByText('TWD 1200')).toBeVisible()
  await expect(page.getByText('合成材質資料')).toBeVisible()

  await page.goto('/admin/catalog')
  await page.getByLabel('已授權商品 ID').fill(draft.id)
  await page.getByRole('button', { name: '載入草稿' }).click()
  await expect(page.getByText(`已載入 ${draft.id}（Published）。`)).toBeVisible()
  await page.getByLabel('新台幣單價').fill('1550')
  const updateResponse = page.waitForResponse(response => response.request().method() === 'PUT' && new URL(response.url()).pathname === `/api/v1/admin/products/${draft.id}`)
  await page.getByRole('button', { name: '儲存變更' }).click()
  const update = await updateResponse
  expect(update.status()).toBe(200)
  await expect(page.getByRole('status')).toContainText('草稿已儲存')
  const unchangedPublic = await (await api.get(`${apiBase}/products/${slug}`)).json()
  expect(unchangedPublic.skus[0].priceTwd).toBe(1200)

  const republishResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === `/api/v1/admin/products/${draft.id}/publication-commands`)
  await page.getByRole('button', { name: '發布', exact: true }).click()
  const republish = await republishResponse
  expect(republish.status()).toBe(200)
  const changedPublic = await (await api.get(`${apiBase}/products/${slug}`)).json()
  expect(changedPublic.skus[0]).toMatchObject({ skuId, priceTwd: 1550 })
})


test('Issue08 Owner unpublishes after public read failure recovery probe', async ({ page }) => {
  const artifactPath = process.env.KOHAMI_ISSUE08_SESSION_ARTIFACT
  const evidencePath = process.env.KOHAMI_ISSUE08_EVIDENCE_PATH
  if (!artifactPath || !evidencePath) throw new Error('ISSUE08_BROWSER_ENVIRONMENT_INCOMPLETE')
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as SessionArtifact
  const evidence = JSON.parse(readFileSync(evidencePath, 'utf8')) as { productId: string; skuId: string; slug: string }
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
  ])
  await page.goto('/admin/catalog')
  await page.getByLabel('已授權商品 ID').fill(evidence.productId)
  await page.getByRole('button', { name: '載入草稿' }).click()
  await expect(page.getByText(`已載入 ${evidence.productId}（Published）。`)).toBeVisible()
  const recovered = await page.request.get(`https://localhost:3000/api/v1/products/${evidence.slug}`)
  expect(recovered.status()).toBe(200)
  expect((await recovered.json()).skus[0]).toMatchObject({ skuId: evidence.skuId, priceTwd: 1550 })
  const unpublishResponse = page.waitForResponse(response => response.request().method() === 'POST' && new URL(response.url()).pathname === `/api/v1/admin/products/${evidence.productId}/publication-commands`)
  await page.getByRole('button', { name: '下架', exact: true }).click()
  const unpublish = await unpublishResponse
  expect(unpublish.status()).toBe(200)
  expect((await page.request.get(`https://localhost:3000/api/v1/products/${evidence.slug}`)).status()).toBe(404)
})
