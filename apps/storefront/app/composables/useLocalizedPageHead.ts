import type { ComputedRef } from 'vue'
import type { StorefrontLocale } from '../utils/storefront-locale'
import { localizedPath, storefrontLocales } from '../utils/storefront-locale'

export function useLocalizedPageHead(options: {
  title: ComputedRef<string> | string
  path: ComputedRef<string> | string
  locale: ComputedRef<StorefrontLocale> | StorefrontLocale
  publicPage: ComputedRef<boolean> | boolean
}) {
  const config = useRuntimeConfig()
  const siteUrl = typeof config.public.siteUrl === 'string' ? config.public.siteUrl : ''

  useHead(() => {
    const title = unref(options.title)
    const locale = unref(options.locale)
    const path = unref(options.path)
    const origin = validSiteOrigin(siteUrl)
    const canonicalPath = localizedPath(locale, path)
    const links = origin
      ? [
          { rel: 'canonical' as const, href: new URL(canonicalPath, origin).toString() },
          ...storefrontLocales.map(language => ({
            rel: 'alternate' as const, hreflang: language,
            href: new URL(localizedPath(language, path), origin).toString(),
          })),
          { rel: 'alternate' as const, hreflang: 'x-default', href: new URL(localizedPath('zh-TW', path), origin).toString() },
        ]
      : []

    return {
      title,
      htmlAttrs: { lang: locale },
      meta: [{ name: 'robots', content: unref(options.publicPage) && origin ? 'index,follow' : 'noindex,nofollow' }],
      link: links,
    }
  })
}

function validSiteOrigin(candidate: string): string | null {
  try {
    const parsed = new URL(candidate)
    if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.pathname !== '/' || parsed.search || parsed.hash) return null
    return parsed.origin
  } catch {
    return null
  }
}
