import { computed, readonly, shallowRef } from 'vue'

export type MemberWalletEntry = {
  id: string
  type: string
  amountTwd: number
  currency: string
  sourceId: string
  createdAt: string
}

export type MemberWalletSnapshot = {
  mode: 'mock'
  currency: 'TWD'
  availableTwd: number
  version: number
  entries: MemberWalletEntry[]
  nextCursor: string | null
}

export type MemberWalletError = 'AUTHENTICATION_REQUIRED' | 'SERVICE_UNAVAILABLE'

const PAGE_SIZE = 20

function problemCode(cause: unknown): string | undefined {
  if (typeof cause !== 'object' || cause === null || !('data' in cause)) return undefined
  const data = cause.data
  if (typeof data !== 'object' || data === null || !('code' in data)) return undefined
  return typeof data.code === 'string' ? data.code : undefined
}

export function useMemberWallet() {
  const snapshot = shallowRef<MemberWalletSnapshot | null>(null)
  const loading = shallowRef(false)
  const loadingMore = shallowRef(false)
  const error = shallowRef<MemberWalletError | null>(null)
  const hasMore = computed(() => Boolean(snapshot.value?.nextCursor))
  let requestVersion = 0

  function clear() {
    requestVersion += 1
    snapshot.value = null
    loading.value = false
    loadingMore.value = false
    error.value = null
  }

  async function load() {
    if (loading.value) return
    const currentRequest = ++requestVersion
    snapshot.value = null
    loading.value = true
    error.value = null

    try {
      const query = new URLSearchParams({ limit: String(PAGE_SIZE) })
      const result = await $fetch<MemberWalletSnapshot>(`/api/v1/wallet/current?${query.toString()}`)
      if (currentRequest === requestVersion) snapshot.value = result
    } catch (cause) {
      if (currentRequest !== requestVersion) return
      snapshot.value = null
      error.value = problemCode(cause) === 'AUTHENTICATION_REQUIRED' ? 'AUTHENTICATION_REQUIRED' : 'SERVICE_UNAVAILABLE'
    } finally {
      if (currentRequest === requestVersion) loading.value = false
    }
  }

  async function loadMore() {
    const cursor = snapshot.value?.nextCursor
    if (!cursor || loading.value || loadingMore.value) return

    const currentRequest = ++requestVersion
    loadingMore.value = true
    error.value = null

    try {
      const query = new URLSearchParams({ limit: String(PAGE_SIZE), cursor })
      const result = await $fetch<MemberWalletSnapshot>(`/api/v1/wallet/current?${query.toString()}`)
      if (currentRequest === requestVersion) {
        const current = snapshot.value
        snapshot.value = current
          ? { ...result, entries: [...current.entries, ...result.entries] }
          : result
      }
    } catch (cause) {
      if (currentRequest !== requestVersion) return
      if (problemCode(cause) === 'AUTHENTICATION_REQUIRED') {
        snapshot.value = null
        error.value = 'AUTHENTICATION_REQUIRED'
      } else error.value = 'SERVICE_UNAVAILABLE'
    } finally {
      if (currentRequest === requestVersion) loadingMore.value = false
    }
  }

  return {
    snapshot: readonly(snapshot),
    loading: readonly(loading),
    loadingMore: readonly(loadingMore),
    error: readonly(error),
    hasMore,
    load,
    loadMore,
    clear,
  }
}
