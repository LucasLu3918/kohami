import { shallowRef } from 'vue'

export type MemberOrder = {
  id: string
  mode: 'mock'
  version: string
  placedAt: string
  orderState: string
  paymentSummary?: { state: string; confirmedAt?: string | null; confirmedCollectedTwd?: number; compensationRequired?: boolean }
  checkoutId?: string
  snapshot: { currency: string; totalTwd: number; merchandiseTwd: number; discountTwd: number; shippingTwd: number; lines: Array<{ skuId: string; productSlug?: string; name?: { value?: string }; colorId?: string; garmentSize?: string; quantity: number; unitPriceTwd: number; paidTwd: number }> }
}

export function useMemberOrders() {
  const $fetch = useStorefrontApi()
  const orders = shallowRef<MemberOrder[]>([])
  const loading = shallowRef(false)
  const error = shallowRef('')
  let requestVersion = 0

  async function load() {
    const version = ++requestVersion
    loading.value = true
    error.value = ''
    try {
      const result = await $fetch<{ items: MemberOrder[] }>('/api/v1/orders')
      if (version === requestVersion) orders.value = result.items
    } catch (cause: any) {
      if (version === requestVersion) {
        orders.value = []
        error.value = cause?.data?.code === 'AUTHENTICATION_REQUIRED' ? 'AUTHENTICATION_REQUIRED' : 'SERVICE_UNAVAILABLE'
      }
    } finally {
      if (version === requestVersion) loading.value = false
    }
  }

  return { orders, loading, error, load }
}
