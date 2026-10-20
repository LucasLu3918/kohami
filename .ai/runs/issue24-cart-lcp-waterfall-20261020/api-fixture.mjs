import { createServer } from 'node:http'
const port = 5199
const skuId = 'sku_issue24_core_lab_m'
const product = {
  id: 'product_issue24_core_lab', productId: 'product_issue24_core_lab', slug: 'issue24-core-lab-shirt', mode: 'mock',
  translations: [
    { locale: 'zh-TW', name: '效能驗收示意商品', description: '本機合成驗收資料。', material: '合成材質估算' },
    { locale: 'ja', name: '性能確認用デモ商品', description: 'ローカル合成データ。', material: '合成素材の推定' },
    { locale: 'en', name: 'Performance demo item', description: 'Synthetic local acceptance data.', material: 'Estimated synthetic material' },
  ],
  media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: 'Synthetic placeholder image' } } }],
  skus: [{ skuId, colorId: 'oatmeal', garmentSize: 'M', priceTwd: 1000, availability: 'InStock', stock: 10 }],
  measurements: [{ name: 'M-chest-width', valueMm: 500, synthetic: true }],
}
const checkout = {
  checkoutId: 'issue24-core-lab-checkout', mode: 'mock', orderState: 'AwaitingPayment', paymentState: 'Created',
  compensationRequired: false, expiresAt: new Date('2026-10-20T00:00:00Z').toISOString(),
  snapshot: { currency: 'TWD', merchandiseTwd: 1000, discountTwd: 0, shippingTwd: 60, totalTwd: 1060,
    lines: [{ skuId, quantity: 1, unitPriceTwd: 1000 }] },
}
function send(res, status, body) {
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' })
  res.end(JSON.stringify(body))
}
const server = createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host || '127.0.0.1'}`)
  if (url.pathname === '/healthz') return send(res, 200, { status: 'ready' })
  if (url.pathname === '/api/v1/auth/session') return send(res, 200, { state: 'Anonymous', mode: 'mock' })
  if (url.pathname === '/api/v1/auth/csrf') return send(res, 200, { requestToken: 'synthetic-issue24-core-lab' })
  if (url.pathname === '/api/v1/products' && req.method === 'GET') {
    await new Promise(resolve => setTimeout(resolve, 120))
    return send(res, 200, { items: [product], nextCursor: null })
  }
  if (url.pathname === `/api/v1/products/${product.slug}` && req.method === 'GET') {
    await new Promise(resolve => setTimeout(resolve, 120))
    return send(res, 200, product)
  }
  if (url.pathname === '/api/v1/carts/current' && req.method === 'GET') {
    await new Promise(resolve => setTimeout(resolve, 120))
    return send(res, 200, { id: 'cart_issue24_core_lab', version: 'cart-v1', items: [{ skuId, quantity: 1, unitPriceTwd: 1000, availability: 'InStock' }] })
  }
  if (url.pathname === `/api/v1/checkouts/${checkout.checkoutId}/status` && req.method === 'GET') {
    await new Promise(resolve => setTimeout(resolve, 120))
    return send(res, 200, checkout)
  }
  return send(res, 404, { code: 'RESOURCE_NOT_FOUND' })
})
server.listen(port, '127.0.0.1')
server.on('error', error => { console.error(`FIXTURE_START_FAILED: ${error.message}`); process.exitCode = 1 })
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close(() => process.exit(0)))
