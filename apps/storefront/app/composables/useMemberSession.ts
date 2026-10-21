import { readonly, shallowRef } from 'vue'

export type MemberSessionState = 'Unavailable' | 'Anonymous' | 'MemberAuthenticated'
type MemberSession = { state: MemberSessionState; userId?: string | null; email?: string | null }
type MemberSessionFailure = 'INVALID_CREDENTIALS' | 'SERVICE_UNAVAILABLE'

function problemCode(cause: unknown): string | undefined {
  if (typeof cause !== 'object' || cause === null || !('data' in cause)) return undefined
  const data = cause.data
  return typeof data === 'object' && data !== null && 'code' in data && typeof data.code === 'string'
    ? data.code
    : undefined
}

export function useMemberSession() {
  const session = useState<MemberSession>('storefront-member-session', () => ({ state: 'Unavailable' }))
  const pending = shallowRef(false)
  const error = shallowRef<MemberSessionFailure | null>(null)

  async function load() {
    try {
      session.value = await $fetch<MemberSession>('/api/v1/auth/session')
    } catch {
      session.value = { state: 'Unavailable' }
    }
    return session.value
  }

  async function login(email: string, password: string) {
    if (pending.value) return false
    pending.value = true
    error.value = null
    try {
      const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
      await $fetch('/api/v1/auth/sessions', {
        method: 'POST',
        body: { email: email.trim(), password },
        headers: { 'X-CSRF-TOKEN': csrf.requestToken },
      })
      const current = await load()
      if (current.state !== 'MemberAuthenticated') {
        error.value = 'INVALID_CREDENTIALS'
        return false
      }
      return true
    } catch (cause) {
      error.value = problemCode(cause) === 'AUTHENTICATION_REQUIRED' || problemCode(cause) === 'INVALID_CREDENTIALS'
        ? 'INVALID_CREDENTIALS'
        : 'SERVICE_UNAVAILABLE'
      return false
    } finally {
      pending.value = false
    }
  }

  async function logout() {
    pending.value = true
    error.value = null
    try {
      const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
      await $fetch('/api/v1/auth/session', { method: 'DELETE', headers: { 'X-CSRF-TOKEN': csrf.requestToken } })
      session.value = { state: 'Anonymous' }
      return true
    } catch {
      session.value = { state: 'Unavailable' }
      error.value = 'SERVICE_UNAVAILABLE'
      return false
    } finally {
      pending.value = false
    }
  }

  return { session: readonly(session), pending: readonly(pending), error: readonly(error), load, login, logout }
}
