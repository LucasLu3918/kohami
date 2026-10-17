import { shallowRef } from 'vue'

export type MemberOrder = {
  id: string
  mode: 'mock'
  version: string
  placedAt: string
  orderState: string
  snapshot: { currency: string; totalTwd: number; lines: Array<{ skuId: string; quantity: number }> }
}

export function useMemberOrders() {
  const orders = shallowRef<MemberOrder[]>([])
  const loading = shallowRef(false)
  const pending = shallowRef(false)
  const error = shallowRef('')
  const claimError = shallowRef('')
  const mutationKey = shallowRef('')

  async function csrfHeader() {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    return { 'X-CSRF-TOKEN': csrf.requestToken }
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      const result = await $fetch<{ items: MemberOrder[] }>('/api/v1/orders')
      orders.value = result.items
    } catch (cause: any) {
      orders.value = []
      error.value = cause?.data?.code === 'AUTHENTICATION_REQUIRED' ? 'AUTHENTICATION_REQUIRED' : 'SERVICE_UNAVAILABLE'
    } finally {
      loading.value = false
    }
  }

  async function claim(orderId: string) {
    const id = orderId.trim()
    if (!id) return
    if (!mutationKey.value) mutationKey.value = `member-claim-${crypto.randomUUID()}`
    pending.value = true
    claimError.value = ''
    try {
      await $fetch(`/api/v1/orders/${encodeURIComponent(id)}/member-claims`, {
        method: 'POST', body: {}, headers: { ...(await csrfHeader()), 'Idempotency-Key': mutationKey.value },
      })
      mutationKey.value = ''
      await load()
    } catch (cause: any) {
      claimError.value = cause?.data?.code === 'AUTHENTICATION_REQUIRED' ? 'AUTHENTICATION_REQUIRED' : cause?.data?.code === 'RESOURCE_NOT_FOUND' ? 'RESOURCE_NOT_FOUND' : 'SERVICE_UNAVAILABLE'
    } finally {
      pending.value = false
    }
  }

  return { orders, loading, pending, error, claimError, load, claim }
}
