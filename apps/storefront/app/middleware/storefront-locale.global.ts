import { normalizeStorefrontLocale, localizedPath } from '../utils/storefront-locale'

const localizedPathPattern = /^\/(?:zh-TW|ja|en)(?:\/|$)/

export default defineNuxtRouteMiddleware((to) => {
  if (to.path.startsWith('/admin') || localizedPathPattern.test(to.path)) return

  const query = { ...to.query }
  const language = normalizeStorefrontLocale(query.lang)
  delete query.lang

  return navigateTo({ path: localizedPath(language, to.path), query }, { redirectCode: 301, replace: true })
})
