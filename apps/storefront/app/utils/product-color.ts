import type { StorefrontLocale } from './storefront-locale'

const colorLabels: Record<string, Record<StorefrontLocale, string>> = {
  oatmeal: { 'zh-TW': '燕麥奶油', ja: 'オートミール', en: 'Oatmeal' },
  dusty_rose: { 'zh-TW': '霧玫瑰', ja: 'ダスティローズ', en: 'Dusty rose' },
  burgundy: { 'zh-TW': '酒紅', ja: 'バーガンディ', en: 'Burgundy' },
  slate_navy: { 'zh-TW': '石板藍', ja: 'スレートネイビー', en: 'Slate navy' },
  sand: { 'zh-TW': '沙色', ja: 'サンド', en: 'Sand' },
  muted_olive: { 'zh-TW': '柔和橄欖', ja: 'オリーブ', en: 'Muted olive' },
  charcoal: { 'zh-TW': '炭灰', ja: 'チャコール', en: 'Charcoal' },
  warm_beige: { 'zh-TW': '暖米色', ja: 'ウォームベージュ', en: 'Warm beige' },
  warm_gray: { 'zh-TW': '暖灰', ja: 'ウォームグレー', en: 'Warm gray' },
  powder_blue: { 'zh-TW': '粉霧藍', ja: 'パウダーブルー', en: 'Powder blue' },
  indigo: { 'zh-TW': '靛藍', ja: 'インディゴ', en: 'Indigo' },
  muted_sage: { 'zh-TW': '鼠尾草綠', ja: 'セージ', en: 'Muted sage' },
  clay_rose: { 'zh-TW': '陶土玫瑰', ja: 'クレイローズ', en: 'Clay rose' },
  deep_taupe: { 'zh-TW': '深灰褐', ja: 'ディープトープ', en: 'Deep taupe' },
  cream: { 'zh-TW': '奶油白', ja: 'クリーム', en: 'Cream' },
  gray: { 'zh-TW': '灰色', ja: 'グレー', en: 'Gray' },
  blue: { 'zh-TW': '藍色', ja: 'ブルー', en: 'Blue' },
  beige: { 'zh-TW': '米色', ja: 'ベージュ', en: 'Beige' },
  black: { 'zh-TW': '黑色', ja: 'ブラック', en: 'Black' },
  demo_black: { 'zh-TW': '黑色', ja: 'ブラック', en: 'Black' },
}

export function localizedProductColor(colorId: string | null | undefined, locale: StorefrontLocale): string {
  if (!colorId || colorId === 'color_demo') return ''
  const key = colorId.toLowerCase().replaceAll('-', '_')
  return colorLabels[key]?.[locale] || colorId.replace(/[_-]+/g, ' ')
}
