// Repository-owned synthetic catalog. Never imported from a database or private fixture.
const seeds = [
  ['linen-shirt', '棉麻襯衫', 'リネンシャツ', 'Linen shirt', 980],
  ['cotton-tee', '純棉上衣', 'コットンTシャツ', 'Cotton tee', 680],
  ['poplin-blouse', '柔棉襯衫', 'ポプリンブラウス', 'Poplin blouse', 1080],
  ['textured-top', '織紋上衣', '表情素材トップス', 'Textured top', 880],
  ['knit-vest', '針織背心', 'ニットベスト', 'Knit vest', 980],
  ['rib-knit-cardigan', '羅紋針織外套', 'リブカーディガン', 'Rib knit cardigan', 1280],
  ['mock-neck-knit', '高領針織上衣', 'ハイネックニット', 'Mock neck knit', 1180],
  ['shirt-dress', '襯衫洋裝', 'シャツワンピース', 'Shirt dress', 1580],
  ['wrap-midi-dress', '繫帶洋裝', 'ラップワンピース', 'Wrap midi dress', 1680],
  ['pleated-midi-skirt', '百褶裙', 'プリーツスカート', 'Pleated midi skirt', 1380],
  ['wide-leg-trousers', '寬版長褲', 'ワイドパンツ', 'Wide leg trousers', 1280],
  ['linen-shorts', '棉麻短褲', 'リネンショートパンツ', 'Linen shorts', 880],
  ['straight-denim', '直筒丹寧褲', 'ストレートデニム', 'Straight denim', 1480],
  ['cropped-jacket', '短版外套', 'ショートジャケット', 'Cropped jacket', 1880],
  ['wool-coat', '毛呢大衣', 'ウールコート', 'Wool coat', 2880],
] as const

const products = seeds.map(([key, zh, ja, en, price]) => ({
  id: `synthetic-${key}`, slug: `demo-${key}`, mode: 'mock' as const,
  translations: [
    { locale: 'zh-TW', name: zh, description: '前端合成展示商品，不提供購買。', material: '展示用合成材質說明' },
    { locale: 'ja', name: ja, description: 'フロントエンドのサンプル商品です。購入できません。', material: 'サンプル素材情報' },
    { locale: 'en', name: en, description: 'Synthetic frontend sample. Not available for purchase.', material: 'Sample material information' },
  ],
  media: [{ media: { url: `/demo-assets/products/demo-${key}.png`, alt: { value: zh } } }],
  measurements: ['S', 'M', 'L'].map((size, index) => ({ name: `${size}-body-length`, valueMm: 600 + index * 20, synthetic: true })),
  skus: ['S', 'M', 'L'].map(size => ({ skuId: `synthetic-${key}-${size}`, colorId: 'oatmeal', garmentSize: size, priceTwd: price, stock: 10, availability: 'InStock' })),
}))

export const demoProductSlugs = products.map(product => product.slug)

export function demoProblem(status: number, code: string) {
  return Response.json({ type: 'about:blank', title: '展示模式無法執行此操作', status, code }, { status })
}

export function mockApiResponse(request: string, method = 'GET'): Response {
  const url = new URL(request, 'https://frontend.invalid')
  if (method !== 'GET') return demoProblem(403, 'DEMO_READ_ONLY')
  if (url.pathname === '/api/v1/auth/session') return Response.json({ state: 'Anonymous', mode: 'mock', permissions: [] })
  if (url.pathname === '/api/v1/products') {
    const requestedLocale = url.searchParams.get('locale') || 'zh-TW'
    const locale = ['zh-TW', 'ja', 'en'].includes(requestedLocale) ? requestedLocale : 'zh-TW'
    const query = (url.searchParams.get('q') || '').toLocaleLowerCase()
    const rows = products.flatMap(product => product.skus.map(sku => ({
      ...sku, productId: product.id, slug: product.slug, mode: product.mode, translations: product.translations, media: product.media,
    }))).filter(row => {
      const translation = row.translations.find(item => item.locale === locale) || row.translations[0]!
      return `${translation.name} ${translation.description}`.toLocaleLowerCase().includes(query)
        && (!url.searchParams.get('colorId') || row.colorId === url.searchParams.get('colorId'))
        && (!url.searchParams.get('garmentSize') || row.garmentSize === url.searchParams.get('garmentSize'))
        && (url.searchParams.get('availability') !== 'in_stock' || row.availability === 'InStock')
    }).sort((a, b) => a.translations.find(item => item.locale === locale)!.name.localeCompare(b.translations.find(item => item.locale === locale)!.name, locale))
    const limit = Math.max(1, Math.min(100, Number(url.searchParams.get('limit')) || 20))
    const offset = Math.max(0, Number(url.searchParams.get('cursor')) || 0)
    return Response.json({ items: rows.slice(offset, offset + limit), nextCursor: offset + limit < rows.length ? String(offset + limit) : null })
  }
  if (url.pathname.startsWith('/api/v1/products/')) {
    const product = products.find(item => item.slug === decodeURIComponent(url.pathname.slice('/api/v1/products/'.length)))
    return product ? Response.json(product) : demoProblem(404, 'RESOURCE_NOT_FOUND')
  }
  return demoProblem(403, 'DEMO_READ_ONLY')
}
