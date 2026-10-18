import { createServer } from 'node:http'
import fixture from './issue24-api-fixture.json' with { type: 'json' }

const product = fixture.product
const port = Number(process.env.ISSUE24_API_PORT || 5080)

function send(response, status, body) {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' })
  response.end(JSON.stringify(body))
}

const server = createServer((request, response) => {
  const url = new URL(request.url || '/', `http://${request.headers.host || '127.0.0.1'}`)
  if (request.method === 'GET' && url.pathname === '/healthz') return send(response, 200, { status: 'ready' })
  if (request.method === 'GET' && url.pathname === '/api/v1/auth/session') return send(response, 200, { state: 'Anonymous', mode: 'mock' })
  if (request.method === 'GET' && url.pathname === '/api/v1/auth/csrf') return send(response, 200, { requestToken: 'issue24-synthetic-csrf' })
  if (request.method === 'GET' && url.pathname === '/api/v1/products') {
    const query = (url.searchParams.get('q') || '').toLocaleLowerCase()
    const searchable = [product.slug, ...product.skus.map(sku => sku.skuId), ...product.translations.map(item => item.name)]
      .some(value => value.toLocaleLowerCase().includes(query))
    return send(response, 200, { items: !query || searchable ? [product] : [], nextCursor: null })
  }
  if (request.method === 'GET' && url.pathname === '/api/v1/carts/current') {
    return send(response, 200, { id: 'cart_issue24_shared', version: 'cart-v1', items: [] })
  }
  if (request.method === 'PUT' && url.pathname === `/api/v1/carts/current/items/${product.skuId}`) {
    return send(response, 200, {
      id: 'cart_issue24_shared', version: 'cart-v2',
      items: [{ skuId: product.skuId, quantity: 1, unitPriceTwd: product.priceTwd, availability: product.availability }],
    })
  }
  return send(response, 404, { code: 'RESOURCE_NOT_FOUND' })
})

server.listen(port, '127.0.0.1')
server.on('error', error => {
  console.error(`ISSUE24_API_FIXTURE_START_FAILED: ${error.message}`)
  process.exitCode = 1
})

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.close(() => process.exit(0)))
}
