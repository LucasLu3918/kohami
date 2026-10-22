<script setup lang="ts">
const $fetch = useStorefrontApi()
import type { MemberOrder } from '../../composables/useMemberOrders'
import { onBeforeUnmount } from 'vue'
import MemberOrderAftercare from './MemberOrderAftercare.vue'
import { useMemberOrderDetails } from '../../composables/useMemberOrderDetails'
import { formatProductNumber } from '../../utils/product-number'
import { localizedProductColor } from '../../utils/product-color'
import { localizedPath } from '../../utils/storefront-locale'

defineProps<{ items: MemberOrder[]; locale: 'zh-TW' | 'ja' | 'en'; catalogBySku: Record<string, { name: string; image: string; alt: string; colorId: string }> }>()
const emit = defineEmits<{ refreshOrders: [] }>()
const { details, receipts, loading, errors, load, clear } = useMemberOrderDetails()
const expanded = shallowRef<Record<string, boolean>>({})
const cancellationPending = shallowRef<Record<string, boolean>>({})
const cancellationErrors = shallowRef<Record<string, boolean>>({})
const cancellationOperations = shallowRef<Record<string, { version: string; key: string }>>({})
const returnPending = shallowRef<Record<string, boolean>>({})
const returnErrors = shallowRef<Record<string, 'stale' | 'invalid' | 'unavailable' | null>>({})
const returnOperations = shallowRef<Record<string, { fingerprint: string; key: string }>>({})

const orderStateLabels: Record<string, Record<string, string>> = {
  'zh-TW': { AwaitingPayment: '等待付款', Confirmed: '已確認', Cancelled: '已取消', Expired: '已到期' },
  ja: { AwaitingPayment: '支払待ち', Confirmed: '確認済み', Cancelled: 'キャンセル済み', Expired: '期限切れ' },
  en: { AwaitingPayment: 'Awaiting payment', Confirmed: 'Confirmed', Cancelled: 'Cancelled', Expired: 'Expired' },
}
function orderStateLabel(state: string, locale: string) {
  return orderStateLabels[locale]?.[state] || state
}
const productNumberLabel = (locale: string) => locale === 'en' ? 'Product no.' : locale === 'ja' ? '商品番号' : '商品編號'
const colorLabel = (locale: string) => locale === 'en' ? 'Color' : locale === 'ja' ? '色' : '顏色'
const sizeLabel = (locale: string) => locale === 'en' ? 'Size' : locale === 'ja' ? 'サイズ' : '尺寸'
const payLabel = (locale: string) => locale === 'en' ? 'Pay now' : locale === 'ja' ? '支払う' : '前往付款'
const toggleLabel = (locale: string, open: boolean) => locale === 'en'
  ? (open ? 'Hide order details' : 'View order details')
  : locale === 'ja'
    ? (open ? '注文詳細を閉じる' : '注文詳細を表示')
    : (open ? '收合訂單明細' : '查看訂單明細')
async function toggleDetails(orderId: string) {
  const open = !expanded.value[orderId]
  expanded.value = { ...expanded.value, [orderId]: open }
  if (open) await load(orderId)
}
async function cancelOrder(orderId: string) {
  const detail = details.value[orderId]
  if (!detail || detail.orderState !== 'AwaitingPayment' || !detail.actions.some(action => action.action === 'Cancel' && action.allowed) || cancellationPending.value[orderId]) return
  cancellationPending.value = { ...cancellationPending.value, [orderId]: true }
  cancellationErrors.value = { ...cancellationErrors.value, [orderId]: false }
  let operation = cancellationOperations.value[orderId]
  if (!operation || operation.version !== detail.version) {
    operation = { version: detail.version, key: `storefront-cancel-${crypto.randomUUID()}` }
    cancellationOperations.value = { ...cancellationOperations.value, [orderId]: operation }
  }
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch(`/api/v1/orders/${encodeURIComponent(orderId)}/cancellations`, {
      method: 'POST',
      body: { expectedVersion: detail.version },
      headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'If-Match': `"${detail.version}"`, 'Idempotency-Key': operation.key },
    })
    const nextOperations = { ...cancellationOperations.value }
    delete nextOperations[orderId]
    cancellationOperations.value = nextOperations
    await Promise.all([load(orderId, true), emit('refreshOrders')])
  } catch (cause: any) {
    const status = cause?.statusCode ?? cause?.response?.status
    if (status === 409 || status === 412) {
      await Promise.all([load(orderId, true), emit('refreshOrders')])
    }
    cancellationErrors.value = { ...cancellationErrors.value, [orderId]: true }
  } finally {
    cancellationPending.value = { ...cancellationPending.value, [orderId]: false }
  }
}
async function requestReturn(orderId: string, request: { orderLineId: string; quantity: number; reason: string }) {
  const detail = details.value[orderId]
  if (!detail || !detail.actions.some(action => action.action === 'RequestReturn' && action.allowed) || returnPending.value[orderId]) return
  const fingerprint = JSON.stringify(request)
  returnPending.value = { ...returnPending.value, [orderId]: true }
  returnErrors.value = { ...returnErrors.value, [orderId]: null }
  let operation = returnOperations.value[orderId]
  if (!operation || operation.fingerprint !== fingerprint) {
    operation = { fingerprint, key: `storefront-return-${crypto.randomUUID()}` }
    returnOperations.value = { ...returnOperations.value, [orderId]: operation }
  }
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf', { credentials: 'same-origin' })
    await $fetch(`/api/v1/orders/${encodeURIComponent(orderId)}/returns`, {
      method: 'POST',
      body: request,
      credentials: 'same-origin',
      headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': operation.key },
    })
    const nextOperations = { ...returnOperations.value }
    delete nextOperations[orderId]
    returnOperations.value = nextOperations
    await Promise.all([load(orderId, true), emit('refreshOrders')])
  } catch (cause: any) {
    const status = cause?.statusCode ?? cause?.response?.status
    if (status === 409 || status === 412) {
      returnErrors.value = { ...returnErrors.value, [orderId]: 'stale' }
      const nextOperations = { ...returnOperations.value }
      delete nextOperations[orderId]
      returnOperations.value = nextOperations
      await Promise.all([load(orderId, true), emit('refreshOrders')])
    } else if (status === 422) {
      returnErrors.value = { ...returnErrors.value, [orderId]: 'invalid' }
      const nextOperations = { ...returnOperations.value }
      delete nextOperations[orderId]
      returnOperations.value = nextOperations
    } else returnErrors.value = { ...returnErrors.value, [orderId]: 'unavailable' }
  } finally {
    returnPending.value = { ...returnPending.value, [orderId]: false }
  }
}
onBeforeUnmount(clear)
</script>

<template>
  <p v-if="items.length === 0" class="empty">{{ locale === 'en' ? 'No member orders yet.' : locale === 'ja' ? '会員注文はまだありません。' : '目前沒有會員訂單。' }}</p>
  <ul v-else class="orders">
    <li v-for="order in items" :key="order.id" class="order" role="group" :aria-label="order.id">
      <h3>{{ order.id }}</h3>
      <p>{{ locale === 'en' ? 'Placed' : locale === 'ja' ? '注文日時' : '下單時間' }}: <time :datetime="order.placedAt">{{ new Date(order.placedAt).toLocaleString(locale) }}</time></p>
      <p>{{ locale === 'en' ? 'Status' : locale === 'ja' ? '状態' : '狀態' }}: {{ orderStateLabel(order.orderState, locale) }}</p>
      <NuxtLink v-if="order.orderState === 'AwaitingPayment' && order.checkoutId" class="pay-order" :to="localizedPath(locale, `/checkout/${encodeURIComponent(order.checkoutId)}`)">{{ payLabel(locale) }}</NuxtLink>
      <p>{{ order.snapshot.currency }} {{ order.snapshot.totalTwd.toLocaleString(locale) }} · {{ order.snapshot.lines.reduce((total, line) => total + line.quantity, 0) }} {{ locale === 'en' ? 'items' : locale === 'ja' ? '点' : '件商品' }}</p>
      <button class="details-toggle" type="button" :aria-expanded="Boolean(expanded[order.id])" @click="toggleDetails(order.id)">{{ toggleLabel(locale, Boolean(expanded[order.id])) }}</button>
      <div v-if="expanded[order.id]" class="details" aria-live="polite">
        <p v-if="loading[order.id]" role="status">{{ locale === 'en' ? 'Loading order details…' : locale === 'ja' ? '注文詳細を読み込み中…' : '正在載入訂單明細…' }}</p>
        <p v-else-if="errors[order.id] === 'ORDER_UNAVAILABLE'" role="alert">{{ locale === 'en' ? 'Order details are temporarily unavailable.' : locale === 'ja' ? '注文詳細を一時的に利用できません。' : '訂單明細暫時無法讀取。' }}</p>
        <MemberOrderAftercare
          v-else-if="details[order.id]"
          :detail="details[order.id]!"
          :receipts="receipts[order.id] || null"
          :locale="locale"
          :cancellation-pending="Boolean(cancellationPending[order.id])"
          :cancellation-error="Boolean(cancellationErrors[order.id])"
          :return-pending="Boolean(returnPending[order.id])"
          :return-error="returnErrors[order.id] || null"
          @cancel="cancelOrder(order.id)"
          @request-return="requestReturn(order.id, $event)"
        />
      </div>
      <ul class="order-lines">
        <li v-for="line in order.snapshot.lines" :key="line.skuId" class="order-line">
          <img v-if="catalogBySku[line.skuId]?.image" :src="catalogBySku[line.skuId]?.image || ''" :alt="catalogBySku[line.skuId]?.alt || formatProductNumber(line.skuId)" loading="lazy" />
          <div class="line-copy">
            <h4>{{ catalogBySku[line.skuId]?.name || line.name?.value || formatProductNumber(line.skuId) }}</h4>
            <p>{{ productNumberLabel(locale) }}：{{ formatProductNumber(line.skuId) }} · {{ colorLabel(locale) }}：{{ localizedProductColor(line.colorId === 'color_demo' ? catalogBySku[line.skuId]?.colorId : line.colorId, locale) || '—' }} · {{ sizeLabel(locale) }}：{{ line.garmentSize || '—' }}</p>
            <p>{{ locale === 'en' ? 'Qty' : locale === 'ja' ? '数量' : '數量' }} {{ line.quantity }} × {{ order.snapshot.currency }} {{ line.unitPriceTwd.toLocaleString(locale) }}</p>
          </div>
          <strong>{{ order.snapshot.currency }} {{ line.paidTwd.toLocaleString(locale) }}</strong>
        </li>
      </ul>
    </li>
  </ul>
</template>

<style scoped>
.orders{list-style:none;padding:0;display:grid;gap:1rem}.order{border:1px solid #8a817b;border-radius:.5rem;padding:1rem}.order h3{margin:.1rem 0 .5rem;overflow-wrap:anywhere}.order p{margin:.35rem 0;overflow-wrap:anywhere}.details-toggle{min-height:2.75rem;padding:.5rem 1rem;border:0;border-radius:.25rem;background:#6d2942;color:white;font:inherit;cursor:pointer}.details-toggle:focus-visible{outline:3px solid #235b9d;outline-offset:3px}.details{margin-top:1rem}.order-lines{list-style:none;padding:0;margin:1rem 0 0;display:grid;gap:.75rem}.order-line{display:grid;grid-template-columns:5rem minmax(0,1fr) auto;align-items:center;gap:1rem;border-top:1px solid #ded9d5;padding-top:.75rem}.order-line img{width:5rem;aspect-ratio:4/5;object-fit:cover;border-radius:.25rem;background:#f5f2ee}.line-copy h4,.line-copy p{margin:.2rem 0;overflow-wrap:anywhere}.order-line strong{white-space:nowrap}.empty{color:#625c58}
.pay-order{display:inline-flex;min-height:2.75rem;align-items:center;padding:.45rem 1rem;border-radius:.25rem;background:#6d2942;color:#fff;text-decoration:none}.pay-order:focus-visible{outline:3px solid #235b9d;outline-offset:3px}
@media (max-width: 15rem) { .order { padding: .75rem; } }
@media (max-width: 30rem) { .order-line { grid-template-columns:4rem minmax(0,1fr); gap:.65rem; } .order-line img { width:4rem; } .order-line strong { grid-column:2; } }
</style>
