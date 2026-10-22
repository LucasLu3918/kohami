import { shallowRef } from 'vue'

export type MemberOrderDetail = {
  id: string
  version: string
  orderState: string
  snapshot: { currency: 'TWD'; lines: Array<{ orderLineId: string; skuId: string; name?: { value?: string }; colorId?: string; garmentSize?: string; quantity: number }> }
  actions: Array<{ action: 'Cancel' | 'RequestReturn'; allowed: boolean; reasonCodes: string[]; returnableLines?: Array<{ orderLineId: string; availableQuantity: number }> }>
  paymentSummary: { state: string; confirmedAt?: string | null; confirmedCollectedTwd: number; compensationRequired: boolean }
  shipmentSummary: { state: string; shipments: Array<{ id: string; state: string; createdAt: string; history: Array<{ state: string; at: string }>; timing: { paymentToShipmentSeconds: number | null; shipmentToShippedSeconds: number | null; shippedToDeliveredSeconds: number | null } }> }
  refundSummary: { requestedCount: number; processingCount: number; unknownCount: number; succeededCount: number; failedCount: number; succeededTwd: number; pendingTwd: number }
  receiptSummary: { state: string; pendingAdjustmentCount: number }
  returns: Array<{ id: string; state: string; caseSummary: { classification: string; openedAt: string | null; firstResponseAt: string | null; firstResponseElapsedSeconds: number | null; escalatedAt: string | null; escalationElapsedSeconds: number | null }; timeline: Array<{ state: string; at: string | null; message?: string }>; inspectionSummary: { resellableQuantity: number; quarantinedQuantity: number } | null }>
}

export type MemberOrderReceipts = {
  receipts: Array<{ id: string; state: string; amountTwd: number; displayNotice: string; createdAt: string }>
  adjustments: Array<{ id: string; state: string; amountTwd: number; displayNotice: string; createdAt: string }>
  taxEffect: 'none'
}

export function useMemberOrderDetails() {
  const details = shallowRef<Record<string, MemberOrderDetail>>({})
  const receipts = shallowRef<Record<string, MemberOrderReceipts>>({})
  const loading = shallowRef<Record<string, boolean>>({})
  const errors = shallowRef<Record<string, string>>({})

  async function load(orderId: string, force = false) {
    if (loading.value[orderId] || (!force && details.value[orderId])) return
    if (force) {
      const nextDetails = { ...details.value }
      const nextReceipts = { ...receipts.value }
      delete nextDetails[orderId]
      delete nextReceipts[orderId]
      details.value = nextDetails
      receipts.value = nextReceipts
    }
    loading.value = { ...loading.value, [orderId]: true }
    errors.value = { ...errors.value, [orderId]: '' }
    const path = `/api/v1/orders/${encodeURIComponent(orderId)}`
    const [orderResult, receiptResult] = await Promise.allSettled([
      $fetch<MemberOrderDetail>(path),
      $fetch<MemberOrderReceipts>(`${path}/receipts`),
    ])
    if (orderResult.status === 'fulfilled') details.value = { ...details.value, [orderId]: orderResult.value }
    else errors.value = { ...errors.value, [orderId]: 'ORDER_UNAVAILABLE' }
    if (receiptResult.status === 'fulfilled') receipts.value = { ...receipts.value, [orderId]: receiptResult.value }
    else if (orderResult.status === 'fulfilled') errors.value = { ...errors.value, [orderId]: 'RECEIPTS_UNAVAILABLE' }
    loading.value = { ...loading.value, [orderId]: false }
  }

  function clear() {
    details.value = {}
    receipts.value = {}
    loading.value = {}
    errors.value = {}
  }

  return { details, receipts, loading, errors, load, clear }
}
