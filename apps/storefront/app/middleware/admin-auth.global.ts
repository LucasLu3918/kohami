const canReadCatalog = (session: { state?: string; permissions?: string[] }) =>
  session.state === 'AdminAuthenticated' && session.permissions?.includes('catalog.read') === true

export default defineNuxtRouteMiddleware(async (to) => {
  if (!to.path.startsWith('/admin') || to.path === '/admin/login') return
  if (to.path !== '/admin/catalog' && !to.path.startsWith('/admin/catalog/')) {
    return navigateTo('/admin/catalog', { replace: true })
  }

  const requestFetch = useStorefrontApi()
  let session: { state?: string; permissions?: string[] }
  try {
    session = await requestFetch('/api/v1/auth/session')
  } catch {
    session = { state: 'Anonymous', permissions: [] }
  }

  if (!canReadCatalog(session)) {
    return navigateTo({ path: '/admin/login', query: { next: to.fullPath } }, { replace: true })
  }

  const { session: currentSession } = useAdminAuth()
  currentSession.value = session as typeof currentSession.value
})
