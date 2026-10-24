import { test } from 'node:test'
import assert from 'node:assert/strict'
import worker from '../worker/public-demo.js'

test('public API requests return the normal unavailable problem response', async () => {
  const response = await worker.fetch(new Request('https://kohami.example/api/v1/products'), {
    ASSETS: { fetch: async () => assert.fail('API requests must not receive the SPA shell') },
  })
  assert.equal(response.status, 503)
  assert.match(response.headers.get('content-type'), /application\/problem\+json/)
  assert.equal((await response.json()).code, 'SERVICE_UNAVAILABLE')
})

test('non-API requests are served from static assets', async () => {
  const expected = new Response('<main>shell</main>', { status: 200 })
  const response = await worker.fetch(new Request('https://kohami.example/zh-TW/checkout/mock-order'), {
    ASSETS: { fetch: async request => {
      assert.equal(new URL(request.url).pathname, '/zh-TW/checkout/mock-order')
      return expected
    } },
  })
  assert.equal(response, expected)
})
