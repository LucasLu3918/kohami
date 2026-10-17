import type { StorefrontLocale } from '../utils/storefront-locale'
import { normalizeStorefrontLocale } from '../utils/storefront-locale'

export function useStorefrontLocale() {
  const route = useRoute()
  const locale = computed<StorefrontLocale>(() => normalizeStorefrontLocale(route.meta.locale || route.query.lang))
  return { locale }
}
