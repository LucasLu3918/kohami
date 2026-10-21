type CartLine = {
  skuId: string
  quantity: number
  unitPriceTwd: number
  availability?: string
}
export type CommerceCart = { id: string; version: string; items: CartLine[]; promotionCode?: string | null }
export type CommerceQuote = {
  id: string
  mode: 'mock'
  expiresAt: string
  snapshot: { currency: 'TWD'; merchandiseTwd: number; discountTwd: number; shippingTwd: number; totalTwd: number }
}
export type CommerceCheckout = {
  checkoutId: string
  orderId: string
  mode: 'mock'
  expiresAt: string
  snapshot: CommerceQuote['snapshot']
}
export type CartProblem = { code?: string; traceId?: string; status?: number }

type MutationOptions = { method: 'GET' | 'POST' | 'PUT' | 'DELETE'; body?: Record<string, unknown>; ifMatch?: string; idempotencyScope?: string }

export function useCommerceCart() {
  const cart = ref<CommerceCart | null>(null)
  const quote = ref<CommerceQuote | null>(null)
  const loading = ref(false)
  const pending = ref(false)
  const error = ref<CartProblem | null>(null)
  const mutationKeys = new Map<string, { fingerprint: string; key: string }>()

  function stableIdempotencyKey(scope: string, body: Record<string, unknown> | undefined, ifMatch: string | undefined) {
    const fingerprint = JSON.stringify([body || {}, ifMatch || ''])
    const previous = mutationKeys.get(scope)
    if (previous?.fingerprint === fingerprint) return previous.key
    const key = `storefront-${crypto.randomUUID()}`
    mutationKeys.set(scope, { fingerprint, key })
    return key
  }

  function problemOf(cause: any): CartProblem {
    return cause?.data && typeof cause.data === 'object' ? cause.data : { code: 'SERVICE_UNAVAILABLE' }
  }

  async function api<T>(path: string, options: MutationOptions = { method: 'POST' }): Promise<T> {
    const headers: Record<string, string> = {}
    if (options.method !== 'GET') headers['X-CSRF-TOKEN'] = (await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')).requestToken
    if (options.ifMatch) headers['If-Match'] = `"${options.ifMatch}"`
    if (options.idempotencyScope) headers['Idempotency-Key'] = stableIdempotencyKey(options.idempotencyScope, options.body, options.ifMatch)
    const response = await $fetch<T>(`/api/v1${path}`, {
      method: options.method as 'GET' | 'POST' | 'PUT' | 'DELETE',
      body: options.body,
      headers,
    }) as T
    if (options.idempotencyScope) mutationKeys.delete(options.idempotencyScope)
    return response
  }

  async function load() {
    loading.value = true
    error.value = null
    try {
      cart.value = await api<CommerceCart>('/carts/current', { method: 'GET' })
    } catch (cause: any) {
      const problem = problemOf(cause)
      if (problem.code === 'RESOURCE_NOT_FOUND') {
        try {
          cart.value = await api<CommerceCart>('/carts', { method: 'POST', body: {}, idempotencyScope: 'create-cart' })
        } catch (createError: any) {
          error.value = problemOf(createError)
        }
      } else error.value = problem
    } finally {
      loading.value = false
    }
  }

  async function setQuantity(skuId: string, quantity: number) {
    const current = cart.value
    if (pending.value || !current || !Number.isInteger(quantity) || quantity < 1 || quantity > 100) return
    pending.value = true
    error.value = null
    quote.value = null
    try {
      cart.value = await api<CommerceCart>(`/carts/current/items/${encodeURIComponent(skuId)}`, {
        method: 'PUT', body: { quantity }, ifMatch: current.version, idempotencyScope: `cart-item-${skuId}`,
      })
    } catch (cause: any) {
      error.value = problemOf(cause)
      if (['VERSION_CHANGED', 'STOCK_CHANGED'].includes(error.value.code || '')) {
        const problem = error.value
        await load()
        error.value = problem
      }
    } finally {
      pending.value = false
    }
  }

  async function remove(skuId: string) {
    const current = cart.value
    if (!current) return
    pending.value = true
    error.value = null
    quote.value = null
    try {
      cart.value = await api<CommerceCart>(`/carts/current/items/${encodeURIComponent(skuId)}`, {
        method: 'DELETE', ifMatch: current.version, idempotencyScope: `remove-cart-item-${skuId}`,
      })
    } catch (cause: any) {
      error.value = problemOf(cause)
      if (error.value.code === 'VERSION_CHANGED') {
        const problem = error.value
        await load()
        error.value = problem
      }
    } finally {
      pending.value = false
    }
  }

  async function setPromotion(promotionCode: string | null) {
    const current = cart.value
    if (!current) return
    pending.value = true
    error.value = null
    quote.value = null
    try {
      cart.value = await api<CommerceCart>('/carts/current/promotion', {
        method: 'PUT', body: { promotionCode }, ifMatch: current.version, idempotencyScope: 'cart-promotion',
      })
    } catch (cause: any) {
      error.value = problemOf(cause)
      if (error.value.code === 'VERSION_CHANGED') {
        const problem = error.value
        await load()
        error.value = problem
      }
    } finally {
      pending.value = false
    }
  }

  async function createQuote(deliveryMethod: 'mock_home' | 'mock_pickup') {
    const current = cart.value
    if (!current?.items.length) return
    pending.value = true
    error.value = null
    quote.value = null
    try {
      quote.value = await api<CommerceQuote>('/checkout-quotes', {
        method: 'POST',
        body: { cartVersion: current.version, deliveryMethod, region: 'demo_supported' },
        ifMatch: current.version,
        idempotencyScope: 'cart-quote',
      })
    } catch (cause: any) {
      error.value = problemOf(cause)
      if (['VERSION_CHANGED', 'QUOTE_CHANGED', 'STOCK_CHANGED'].includes(error.value.code || '')) {
        const problem = error.value
        await load()
        error.value = problem
      }
    } finally {
      pending.value = false
    }
  }

  async function createCheckout(email: string, deliveryMethod: 'mock_home' | 'mock_pickup', paymentMethod?: 'wallet') {
    const current = cart.value
    const currentQuote = quote.value
    if (!current || !currentQuote || !email.trim()) return null
    pending.value = true
    error.value = null
    try {
      return await api<CommerceCheckout>('/checkouts', {
        method: 'POST',
        body: {
          quoteId: currentQuote.id,
          cartVersion: current.version,
          contact: { email: email.trim() },
          delivery: { method: deliveryMethod, region: 'demo_supported' },
          confirmed: true,
          ...(paymentMethod ? { paymentMethod } : {}),
        },
        ifMatch: current.version,
        idempotencyScope: `cart-checkout-${current.id}`,
      })
    } catch (cause: any) {
      error.value = problemOf(cause)
      if (['VERSION_CHANGED', 'QUOTE_CHANGED', 'QUOTE_EXPIRED', 'STOCK_CHANGED', 'PRODUCT_UNAVAILABLE', 'FEE_UNAVAILABLE'].includes(error.value.code || '')) {
        const problem = error.value
        quote.value = null
        await load()
        error.value = problem
      }
      return null
    } finally {
      pending.value = false
    }
  }

  return { cart, quote, loading, pending, error, load, setQuantity, remove, setPromotion, createQuote, createCheckout }
}
