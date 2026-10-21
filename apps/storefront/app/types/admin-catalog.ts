export type AdminProduct = {
  id: string
  slug: string
  state: 'Published'
  translations: Array<{ locale: 'zh-TW' | 'ja' | 'en'; name: string; description: string; material?: string }>
  media: Array<{ media: { url: string; alt: { value: string } } }>
  skus: Array<{ skuId: string; colorId: string; garmentSize: string; priceTwd: number; stock: number; availability: 'InStock' | 'OutOfStock' }>
}

export type AdminProductList = { mode: 'mock'; items: AdminProduct[] }
