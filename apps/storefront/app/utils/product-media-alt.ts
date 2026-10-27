import type { StorefrontLocale } from './storefront-locale'
import { localizedProductColor } from './product-color'

export function localizedProductMediaAlt(alt: string, productName: string, locale: StorefrontLocale, colorId?: string): string {
  if (locale === 'zh-TW') return alt

  const productOnly = alt.match(/・(.+?)單品(平拍|概念色示意，非實品照片)?$/)
  if (productOnly) {
    const color = localizedProductColor(colorId, locale) || productOnly[1]
    const concept = productOnly[2] === '概念色示意，非實品照片'
    if (locale === 'ja') return concept
      ? `${productName}・${color}のコンセプトイメージ（実物写真ではありません）`
      : `${productName}・${color}の商品画像`
    return concept
      ? `${productName} · ${color} concept illustration, not a product photo`
      : `${productName} · ${color} product image`
  }

  const outfitView = alt.match(/・(?:.+?模特穿搭示意|模特穿搭)\s*(\d+)$/)
  if (!outfitView) return alt

  const color = localizedProductColor(colorId, locale)
  if (locale === 'ja') return `${productName}・${color ? `${color} ` : ''}コーディネート ${outfitView[1]}`
  return `${productName} · ${color ? `${color} ` : ''}outfit ${outfitView[1]}`
}
