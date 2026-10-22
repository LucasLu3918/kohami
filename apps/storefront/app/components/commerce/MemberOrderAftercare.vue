<script setup lang="ts">
import type { MemberOrderDetail, MemberOrderReceipts } from '../../composables/useMemberOrderDetails'
import ShipmentTimeline from './ShipmentTimeline.vue'
import ReturnSupportTimeline from './ReturnSupportTimeline.vue'
import RefundStatusTimeline from './RefundStatusTimeline.vue'

const props = defineProps<{ detail: MemberOrderDetail; receipts: MemberOrderReceipts | null; locale: 'zh-TW' | 'ja' | 'en' }>()
const labels = computed(() => props.locale === 'en'
  ? { payment: 'Payment status', collected: 'Confirmed amount', confirmed: 'Confirmed at', receipt: 'Receipts and adjustments', receiptState: 'Receipt status', pending: 'Pending adjustments', amount: 'Amount', empty: 'No receipt documents.', receiptError: 'Receipt documents are temporarily unavailable.', mock: 'Demo document; no tax effect.' }
  : props.locale === 'ja'
    ? { payment: '支払状況', collected: '確認済み金額', confirmed: '確認日時', receipt: '領収書と調整', receiptState: '領収書の状態', pending: '処理待ち調整', amount: '金額', empty: '領収書はありません。', receiptError: '領収書を一時的に利用できません。', mock: 'デモ書類です。税務上の効力はありません。' }
    : { payment: '付款狀態', collected: '已確認金額', confirmed: '確認時間', receipt: '收據與調整', receiptState: '憑證狀態', pending: '待處理調整', amount: '金額', empty: '目前沒有收據。', receiptError: '憑證暫時無法讀取。', mock: '展示憑證，不具稅務效力。' })
function money(value: number) { return `TWD ${new Intl.NumberFormat(props.locale, { maximumFractionDigits: 0 }).format(value)}` }
</script>

<template>
  <div class="aftercare">
    <section class="payment-summary">
      <h4>{{ labels.payment }}</h4>
      <p>{{ detail.paymentSummary.state }} · {{ labels.collected }}: {{ money(detail.paymentSummary.confirmedCollectedTwd) }}</p>
      <p v-if="detail.paymentSummary.confirmedAt">{{ labels.confirmed }}: <time :datetime="detail.paymentSummary.confirmedAt">{{ new Date(detail.paymentSummary.confirmedAt).toLocaleString(locale) }}</time></p>
    </section>
    <ShipmentTimeline :shipments="detail.shipmentSummary.shipments" :locale="locale" />
    <ReturnSupportTimeline :returns="detail.returns" :locale="locale" />
    <RefundStatusTimeline :summary="detail.refundSummary" :locale="locale" />
    <section class="receipt-summary">
      <h4>{{ labels.receipt }}</h4>
      <template v-if="receipts">
        <p>{{ labels.receiptState }}: {{ detail.receiptSummary.state }} · {{ labels.pending }}: {{ detail.receiptSummary.pendingAdjustmentCount }}</p>
        <ul v-if="receipts.receipts.length || receipts.adjustments.length">
          <li v-for="document in [...receipts.receipts, ...receipts.adjustments]" :key="document.id">
            {{ document.state }} · {{ labels.amount }}: {{ money(document.amountTwd) }} · <time :datetime="document.createdAt">{{ new Date(document.createdAt).toLocaleString(locale) }}</time>
            <p>{{ document.displayNotice || labels.mock }}</p>
          </li>
        </ul>
        <p v-else>{{ labels.empty }}</p>
        <p class="notice">{{ labels.mock }}</p>
      </template>
      <p v-else role="status">{{ labels.receiptError }}</p>
    </section>
  </div>
</template>

<style scoped>
.aftercare{border-top:1px solid #ded9d5;margin-top:1rem;padding-top:1rem}.aftercare section{margin-block:1rem}.aftercare h4{margin:.25rem 0}.aftercare p{overflow-wrap:anywhere}.receipt-summary ul{padding-inline-start:1.25rem}.receipt-summary li{margin-block:.75rem}.notice{color:#625c58}
</style>
