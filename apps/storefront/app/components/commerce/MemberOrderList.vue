<script setup lang="ts">
import type { MemberOrder } from '../../composables/useMemberOrders'

defineProps<{ items: MemberOrder[]; locale: string }>()
</script>

<template>
  <p v-if="items.length === 0" class="empty">{{ locale === 'en' ? 'No member orders yet.' : locale === 'ja' ? '会員注文はまだありません。' : '目前沒有會員訂單。' }}</p>
  <ul v-else class="orders">
    <li v-for="order in items" :key="order.id" class="order">
      <h3>{{ order.id }}</h3>
      <p>{{ locale === 'en' ? 'Placed' : locale === 'ja' ? '注文日時' : '下單時間' }}: <time :datetime="order.placedAt">{{ new Date(order.placedAt).toLocaleString(locale) }}</time></p>
      <p>{{ locale === 'en' ? 'Status' : locale === 'ja' ? '状態' : '狀態' }}: {{ order.orderState }}</p>
      <p>{{ order.snapshot.currency }} {{ order.snapshot.totalTwd.toLocaleString(locale) }} · {{ order.snapshot.lines.length }} {{ locale === 'en' ? 'items' : locale === 'ja' ? '点' : '項商品' }}</p>
    </li>
  </ul>
</template>

<style scoped>
.orders{list-style:none;padding:0;display:grid;gap:1rem}.order{border:1px solid #ded7d1;border-radius:.5rem;padding:1rem}.order h3{margin:.1rem 0 .5rem}.order p{margin:.35rem 0}.empty{color:#625c58}
</style>
