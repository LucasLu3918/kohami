export const storefrontLocales = ['zh-TW', 'ja', 'en'] as const
export type StorefrontLocale = typeof storefrontLocales[number]

export function normalizeStorefrontLocale(value: unknown): StorefrontLocale {
  const candidate = Array.isArray(value) ? value[0] : value
  return storefrontLocales.includes(candidate as StorefrontLocale) ? candidate as StorefrontLocale : 'zh-TW'
}

export function stripStorefrontLocale(path: string): string {
  const stripped = path.replace(/^\/(?:zh-TW|ja|en)(?=\/|$)/, '')
  return stripped || '/'
}

export function localizedPath(locale: StorefrontLocale, path: string): string {
  const unlocalized = stripStorefrontLocale(path)
  return unlocalized === '/' ? `/${locale}` : `/${locale}${unlocalized}`
}
