<script setup lang="ts">
type RefundSummary = {
  requestedCount: number
  processingCount: number
  unknownCount: number
  succeededCount: number
  failedCount: number
  succeededTwd: number
  pendingTwd: number
}

const props = defineProps<{
  summary: RefundSummary
  locale: 'zh-TW' | 'ja' | 'en'
}>()

const labels = {
  'zh-TW': {
    title: '退款處理狀態', notice: '退款資料記錄於系統；外部退款服務尚未連接。', requested: '待處理申請', processing: '處理中', unknown: '結果待確認', succeeded: '退款成功', failed: '退款失敗', refunded: '已退款金額', reserved: '處理中及待確認保留額度',
  },
  ja: {
    title: '返金処理状況', notice: '返金データはシステムに記録されます。外部返金サービスは未接続です。', requested: '申請受付', processing: '処理中', unknown: '結果確認待ち', succeeded: '返金成功', failed: '返金失敗', refunded: '返金済み金額', reserved: '処理中・確認待ちの予約額',
  },
  en: {
    title: 'Refund processing status', notice: 'Refund records are stored by the system; external refund processing is not connected.', requested: 'Requested', processing: 'Processing', unknown: 'Awaiting confirmation', succeeded: 'Succeeded', failed: 'Failed', refunded: 'Refunded', reserved: 'Still reserved',
  },
} as const

const money = (amount: number) => `TWD ${new Intl.NumberFormat(props.locale, { maximumFractionDigits: 0 }).format(amount)}`
</script>

<template>
  <section class="refund-status-timeline" :aria-label="labels[locale].title">
    <h3>{{ labels[locale].title }}</h3>
    <p class="refund-status-notice">{{ labels[locale].notice }}</p>
    <dl>
      <div><dt>{{ labels[locale].requested }}</dt><dd>{{ summary.requestedCount }}</dd></div>
      <div><dt>{{ labels[locale].processing }}</dt><dd>{{ summary.processingCount }}</dd></div>
      <div><dt>{{ labels[locale].unknown }}</dt><dd>{{ summary.unknownCount }}</dd></div>
      <div><dt>{{ labels[locale].succeeded }}</dt><dd>{{ summary.succeededCount }}</dd></div>
      <div><dt>{{ labels[locale].failed }}</dt><dd>{{ summary.failedCount }}</dd></div>
      <div><dt>{{ labels[locale].refunded }}</dt><dd>{{ money(summary.succeededTwd) }}</dd></div>
      <div><dt>{{ labels[locale].reserved }}</dt><dd>{{ money(summary.pendingTwd) }}</dd></div>
    </dl>
  </section>
</template>

<style scoped>
.refund-status-timeline { margin-block: 1rem; padding: 1rem; border: 1px solid #d7dce2; border-radius: .5rem; }
.refund-status-timeline h3 { margin: 0; }
.refund-status-notice { margin-block: .5rem; }
.refund-status-timeline dl { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .5rem 1rem; margin: 0; }
.refund-status-timeline dl div { display: flex; justify-content: space-between; gap: 1rem; }
.refund-status-timeline dt, .refund-status-timeline dd { margin: 0; }
</style>
