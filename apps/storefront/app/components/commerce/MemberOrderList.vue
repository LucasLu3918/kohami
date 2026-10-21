<script setup lang="ts">
import type { MemberOrder } from '../../composables/useMemberOrders'

defineProps<{ items: MemberOrder[]; locale: string; catalogBySku: Record<string, { name: string; image: string; alt: string }> }>()

const orderStateLabels: Record<string, Record<string, string>> = {
  'zh-TW': { AwaitingPayment: '等待付款', Confirmed: '已確認', Cancelled: '已取消', Expired: '已到期' },
  ja: { AwaitingPayment: '支払待ち', Confirmed: '確認済み', Cancelled: 'キャンセル済み', Expired: '期限切れ' },
  en: { AwaitingPayment: 'Awaiting payment', Confirmed: 'Confirmed', Cancelled: 'Cancelled', Expired: 'Expired' },
}
function orderStateLabel(state: string, locale: string) {
  return orderStateLabels[locale]?.[state] || state
}
</script>

<template>
  <p v-if="items.length === 0" class="empty">{{ locale === 'en' ? 'No member orders yet.' : locale === 'ja' ? '会員注文はまだありません。' : '目前沒有會員訂單。' }}</p>
  <ul v-else class="orders">
    <li v-for="order in items" :key="order.id" class="order">
      <h3>{{ order.id }}</h3>
      <p>{{ locale === 'en' ? 'Placed' : locale === 'ja' ? '注文日時' : '下單時間' }}: <time :datetime="order.placedAt">{{ new Date(order.placedAt).toLocaleString(locale) }}</time></p>
      <p>{{ locale === 'en' ? 'Status' : locale === 'ja' ? '状態' : '狀態' }}: {{ orderStateLabel(order.orderState, locale) }}</p>
      <p>{{ order.snapshot.currency }} {{ order.snapshot.totalTwd.toLocaleString(locale) }} · {{ order.snapshot.lines.reduce((total, line) => total + line.quantity, 0) }} {{ locale === 'en' ? 'items' : locale === 'ja' ? '点' : '件商品' }}</p>
      <ul class="order-lines">
        <li v-for="line in order.snapshot.lines" :key="line.skuId" class="order-line">
          <img v-if="catalogBySku[line.skuId]?.image" :src="catalogBySku[line.skuId]?.image || ''" :alt="catalogBySku[line.skuId]?.alt || line.skuId" loading="lazy" />
          <div class="line-copy">
            <h4>{{ catalogBySku[line.skuId]?.name || line.name?.value || line.skuId }}</h4>
            <p>{{ locale === 'en' ? 'SKU' : 'SKU' }}：{{ line.skuId }} · {{ line.colorId || '—' }} · {{ line.garmentSize || '—' }}</p>
            <p>{{ locale === 'en' ? 'Qty' : locale === 'ja' ? '数量' : '數量' }} {{ line.quantity }} × {{ order.snapshot.currency }} {{ line.unitPriceTwd.toLocaleString(locale) }}</p>
          </div>
          <strong>{{ order.snapshot.currency }} {{ line.paidTwd.toLocaleString(locale) }}</strong>
        </li>
      </ul>
    </li>
  </ul>
</template>

<style scoped>
.orders{list-style:none;padding:0;display:grid;gap:1rem}.order{border:1px solid #8a817b;border-radius:.5rem;padding:1rem}.order h3{margin:.1rem 0 .5rem;overflow-wrap:anywhere}.order p{margin:.35rem 0;overflow-wrap:anywhere}.order-lines{list-style:none;padding:0;margin:1rem 0 0;display:grid;gap:.75rem}.order-line{display:grid;grid-template-columns:5rem minmax(0,1fr) auto;align-items:center;gap:1rem;border-top:1px solid #ded9d5;padding-top:.75rem}.order-line img{width:5rem;aspect-ratio:4/5;object-fit:cover;border-radius:.25rem;background:#f5f2ee}.line-copy h4,.line-copy p{margin:.2rem 0;overflow-wrap:anywhere}.order-line strong{white-space:nowrap}.empty{color:#625c58}
@media (max-width: 15rem) { .order { padding: .75rem; } }
@media (max-width: 30rem) { .order-line { grid-template-columns:4rem minmax(0,1fr); gap:.65rem; } .order-line img { width:4rem; } .order-line strong { grid-column:2; } }
</style>
