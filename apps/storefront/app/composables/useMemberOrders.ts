import { shallowRef } from 'vue'

export type MemberOrder = {
  id: string
  mode: 'mock'
  version: string
  placedAt: string
  orderState: string
  checkoutId?: string
  snapshot: { currency: string; totalTwd: number; merchandiseTwd: number; discountTwd: number; shippingTwd: number; lines: Array<{ skuId: string; productSlug?: string; name?: { value?: string }; colorId?: string; garmentSize?: string; quantity: number; unitPriceTwd: number; paidTwd: number }> }
}

export function useMemberOrders() {
  const $fetch = useStorefrontApi()
  const orders = shallowRef<MemberOrder[]>([])
  const loading = shallowRef(false)
  const error = shallowRef('')

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

  return { orders, loading, error, load }
}
