import type { StorefrontLocale } from './storefront-locale'
import { localizedProductColor } from './product-color'

export function localizedProductMediaAlt(alt: string, productName: string, locale: StorefrontLocale, colorId?: string): string {
  if (locale === 'zh-TW') return alt

  if (/・單品平拍$/.test(alt)) {
    return locale === 'ja'
      ? `${productName}・商品画像（平置き）`
      : `${productName} · flat-lay product image`
  }

  if (/・.+單品概念色示意，非實品照片$/.test(alt)) {
    const color = localizedProductColor(colorId, locale)
    return locale === 'ja'
      ? `${productName}・${color ? `${color} ` : ''}カラーイメージ（実物の商品写真ではありません）`
      : `${productName} · ${color ? `${color} ` : ''}color concept (not a photograph of the actual item)`
  }

  const outfitView = alt.match(/・(?:.+?模特穿搭示意|模特穿搭)\s*(\d+)$/)
  if (!outfitView) return alt

  const color = localizedProductColor(colorId, locale)
  if (locale === 'ja') return `${productName}・${color ? `${color} ` : ''}着用イメージ（生成）${outfitView[1]}`
  return `${productName} · ${color ? `${color} ` : ''}generated outfit concept ${outfitView[1]}`
}
