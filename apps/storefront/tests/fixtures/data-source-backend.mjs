import { createServer } from 'node:http'

let catalogReads = 0
const product = { productId: 'test-database-row', slug: 'backend-only-product', translations: [{ locale: 'zh-TW', name: '後端來源驗收商品' }], media: [], skuId: 'backend-sku', colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1234, availability: 'InStock', mode: 'mock' }
createServer((request, response) => {
  response.setHeader('content-type', 'application/json')
  if (request.url === '/__counts') return response.end(JSON.stringify({ catalogReads }))
  if (request.url.startsWith('/api/v1/products?')) {
    catalogReads++
    return response.end(JSON.stringify({ items: [product], nextCursor: null }))
  }
  response.statusCode = 403
  response.end(JSON.stringify({ code: 'PERMISSION_CHANGED' }))
}).listen(5197, '127.0.0.1')
