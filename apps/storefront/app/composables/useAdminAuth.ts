export type AdminSession = {
  state: 'AdminAuthenticated' | 'MemberAuthenticated' | 'Anonymous'
  mode: 'mock'
  userId?: string | null
  permissions: string[]
  expiresAt?: string | null
}

const anonymousSession: AdminSession = { state: 'Anonymous', mode: 'mock', permissions: [] }

export function useAdminAuth() {
  const $fetch = useStorefrontApi()
  const session = useState<AdminSession>('admin-session', () => anonymousSession)
  const pending = useState<boolean>('admin-session-pending', () => false)

  async function refresh() {
    pending.value = true
    try {
      session.value = await $fetch<AdminSession>('/api/v1/auth/session')
      return session.value
    } catch {
      session.value = anonymousSession
      return session.value
    } finally {
      pending.value = false
    }
  }

  async function login(email: string, password: string) {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    const result = await $fetch<AdminSession>('/api/v1/auth/sessions', {
      method: 'POST',
      body: { email, password },
      headers: { 'X-CSRF-TOKEN': csrf.requestToken },
    })
    session.value = result
    return result
  }

  async function loginDemo() {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    const result = await $fetch<AdminSession>('/api/v1/auth/sessions', { method: 'POST', body: { demoMode: 'admin' }, headers: { 'X-CSRF-TOKEN': csrf.requestToken } })
    session.value = result
    return result
  }

  async function logout() {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch('/api/v1/auth/session', {
      method: 'DELETE',
      headers: { 'X-CSRF-TOKEN': csrf.requestToken },
    })
    session.value = anonymousSession
  }

  return { session, pending, refresh, login, loginDemo, logout }
}
