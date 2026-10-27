import type { StorefrontLocale } from './storefront-locale'
import { localizedProductColor } from './product-color'

export function localizedProductMediaAlt(alt: string, productName: string, locale: StorefrontLocale, colorId?: string): string {
  if (locale === 'zh-TW') return alt

  const productColor = alt.match(/・(.+?)單品$/)
  if (productColor) {
    const color = localizedProductColor(colorId, locale)
    return locale === 'ja'
      ? `${productName}・${color || productColor[1]}の商品画像`
      : `${productName} · ${color || productColor[1]} product image`
  }

  const outfitView = alt.match(/・(?:.+?模特穿搭示意|模特穿搭)\s*(\d+)$/)
  if (!outfitView) return alt

  const color = localizedProductColor(colorId, locale)
  if (locale === 'ja') return `${productName}・${color ? `${color} ` : ''}コーディネート ${outfitView[1]}`
  return `${productName} · ${color ? `${color} ` : ''}outfit ${outfitView[1]}`
}
