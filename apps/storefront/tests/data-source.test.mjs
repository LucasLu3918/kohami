import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createDataSourceFetch } from '../app/utils/data-source-fetch.ts'

test('mock catalog, filtering and detail work without any backend request', async () => {
  let calls = 0
  const api = createDataSourceFetch({ source: 'mock', backendAvailable: true, backendFetch: async () => { calls++; throw new Error('must not connect') } })
  const catalog = await api('/api/v1/products?locale=zh-TW&limit=100')
  assert.equal(new Set(catalog.items.map(item => item.slug)).size, 15)
  const filtered = await api('/api/v1/products?locale=en&q=linen&limit=1')
  assert.equal(filtered.items.length, 1)
  assert.ok(filtered.nextCursor)
  const next = await api(`/api/v1/products?locale=en&q=linen&limit=1&cursor=${filtered.nextCursor}`)
  assert.notEqual(next.items[0].skuId, filtered.items[0].skuId)
  const detail = await api(`/api/v1/products/${filtered.items[0].slug}`)
  assert.equal(detail.slug, filtered.items[0].slug)
  assert.equal(detail.translations.length, 3)
  assert.equal(calls, 0)
})

test('mock refuses transactions, private data and unknown products without network', async () => {
  const api = createDataSourceFetch({ source: 'mock', backendAvailable: false, backendFetch: async () => assert.fail('backend called') })
  assert.equal((await api('/api/v1/auth/session')).state, 'Anonymous')
  for (const [path, options, code] of [
    ['/api/v1/carts', { method: 'POST', body: {} }, 'DEMO_READ_ONLY'],
    ['/api/v1/carts/current', {}, 'DEMO_READ_ONLY'],
    ['/api/v1/admin/products', {}, 'DEMO_READ_ONLY'],
    ['/api/v1/products/missing', {}, 'RESOURCE_NOT_FOUND'],
  ]) await assert.rejects(api(path, options), error => error.data.code === code)
})

test('backend delegates unchanged; unavailable or failed backend never fabricates mock data', async () => {
  const seen = []
  const backendFetch = async (...args) => { seen.push(args); return { items: [{ slug: 'database-product' }] } }
  const api = createDataSourceFetch({ source: 'backend', backendAvailable: true, backendFetch })
  assert.equal((await api('/api/v1/products')).items[0].slug, 'database-product')
  assert.equal(seen[0][0], '/api/v1/products')
  const unavailable = createDataSourceFetch({ source: 'backend', backendAvailable: false, backendFetch })
  await assert.rejects(unavailable('/api/v1/products'), error => error.data.code === 'BACKEND_NOT_CONFIGURED')
  assert.equal(seen.length, 1)
  const failed = createDataSourceFetch({ source: 'backend', backendAvailable: true, backendFetch: async () => { throw new Error('offline') } })
  await assert.rejects(failed('/api/v1/products'), /offline/)
})
