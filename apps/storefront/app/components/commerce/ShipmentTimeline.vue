<script setup lang="ts">
type Locale = 'zh-TW' | 'ja' | 'en'
type ShipmentEvent = { state: string; at: string }
type Shipment = {
  id: string
  mode?: 'mock'
  state: string
  createdAt: string
  history: ShipmentEvent[]
  timing: {
    paymentToShipmentSeconds: number | null
    shipmentToShippedSeconds: number | null
    shippedToDeliveredSeconds: number | null
  }
}

const props = defineProps<{ shipments: Shipment[]; locale: Locale }>()
const copy = computed(() => props.locale === 'zh-TW'
  ? { title: '配送歷程', note: '狀態由系統保存；承運商尚未連接，無法追蹤包裹。', states: { Pending: '待出貨', Shipped: '已出貨', InTransit: '運送中', Delivered: '已送達', Exception: '配送異常', Returned: '已退回' }, created: '建立出貨', paymentTime: '付款確認至建立出貨', shipTime: '建立出貨至已出貨', deliveryTime: '已出貨至已送達', seconds: '秒' }
  : props.locale === 'ja'
    ? { title: '配送履歴', note: '状態はシステムに保存されます。配送業者は未接続のため荷物を追跡できません。', states: { Pending: '出荷待ち', Shipped: '出荷済み', InTransit: '配送中', Delivered: '配達済み', Exception: '配送例外', Returned: '返送済み' }, created: '出荷作成', paymentTime: '支払確認から出荷作成まで', shipTime: '出荷作成から出荷まで', deliveryTime: '出荷から配達まで', seconds: '秒' }
    : { title: 'Shipment timeline', note: 'Status is saved by the system. A carrier is not connected, so parcels cannot be tracked.', states: { Pending: 'Pending', Shipped: 'Shipped', InTransit: 'In transit', Delivered: 'Delivered', Exception: 'Delivery exception', Returned: 'Returned' }, created: 'Shipment created', paymentTime: 'Payment confirmed to shipment creation', shipTime: 'Shipment creation to shipped', deliveryTime: 'Shipped to delivered', seconds: 'seconds' })
function stateName(state: string) { return (copy.value.states as Record<string, string>)[state] || state }
function duration(value: number | null) { return value === null ? '—' : `${value} ${copy.value.seconds}` }
function date(value: string) { return new Date(value).toLocaleString(props.locale, { timeZone: 'UTC', timeZoneName: 'short' }) }
</script>

<template>
  <section v-if="shipments.length" class="shipment-timeline" aria-labelledby="shipment-timeline-title">
    <h3 id="shipment-timeline-title">{{ copy.title }}</h3>
    <p class="notice">{{ copy.note }}</p>
    <article v-for="shipment in shipments" :key="shipment.id" class="shipment-entry">
      <h4>{{ stateName(shipment.state) }} · <code>{{ shipment.id }}</code></h4>
      <p>{{ copy.created }}: <time :datetime="shipment.createdAt">{{ date(shipment.createdAt) }}</time></p>
      <ol>
        <li v-for="(event, index) in shipment.history" :key="`${event.state}-${event.at}-${index}`">
          {{ stateName(event.state) }} · <time :datetime="event.at">{{ date(event.at) }}</time>
        </li>
      </ol>
      <dl>
        <dt>{{ copy.paymentTime }}</dt><dd>{{ duration(shipment.timing.paymentToShipmentSeconds) }}</dd>
        <dt>{{ copy.shipTime }}</dt><dd>{{ duration(shipment.timing.shipmentToShippedSeconds) }}</dd>
        <dt>{{ copy.deliveryTime }}</dt><dd>{{ duration(shipment.timing.shippedToDeliveredSeconds) }}</dd>
      </dl>
    </article>
  </section>
</template>
