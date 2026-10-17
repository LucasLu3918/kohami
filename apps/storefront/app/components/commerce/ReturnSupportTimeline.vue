<script setup lang="ts">
type Locale = 'zh-TW' | 'ja' | 'en'
type ReturnTimelineEvent = { state: string; at: string | null; message?: string }
type ReturnSummary = {
  id: string
  state: string
  caseSummary: {
    classification: string
    openedAt: string | null
    firstResponseAt: string | null
    firstResponseElapsedSeconds: number | null
    escalatedAt: string | null
    escalationElapsedSeconds: number | null
  }
  timeline: ReturnTimelineEvent[]
  inspectionSummary: { resellableQuantity: number; quarantinedQuantity: number } | null
}

const props = defineProps<{ returns: ReturnSummary[]; locale: Locale }>()
const copy = computed(() => props.locale === 'zh-TW'
  ? { title: '退貨案件歷程', classification: '案件分類', opened: '提出時間', firstResponse: '首次客服回應', escalated: '升級時間', elapsed: '經過', seconds: '秒', inspection: '驗收結果', resellable: '可再售', quarantined: '隔離', states: { Requested: '已提出', Accepted: '已受理', Escalated: '已升級處理', Rejected: '已拒絕', Received: '已收件', Inspected: '已完成驗收' } }
  : props.locale === 'ja'
    ? { title: '返品案件の履歴', classification: '分類', opened: '申請日時', firstResponse: '最初のサポート対応', escalated: 'エスカレーション日時', elapsed: '経過', seconds: '秒', inspection: '検品結果', resellable: '再販可能', quarantined: '隔離', states: { Requested: '申請済み', Accepted: '受理済み', Escalated: 'エスカレーション済み', Rejected: '却下', Received: '受領済み', Inspected: '検品完了' } }
    : { title: 'Return case timeline', classification: 'Classification', opened: 'Opened', firstResponse: 'First support response', escalated: 'Escalated', elapsed: 'Elapsed', seconds: 'seconds', inspection: 'Inspection result', resellable: 'Resellable', quarantined: 'Quarantined', states: { Requested: 'Requested', Accepted: 'Accepted', Escalated: 'Escalated', Rejected: 'Rejected', Received: 'Received', Inspected: 'Inspected' } })

function stateName(state: string) { return (copy.value.states as Record<string, string>)[state] || state }
function date(value: string | null) { return value ? new Date(value).toLocaleString(props.locale, { timeZone: 'UTC', timeZoneName: 'short' }) : '—' }
function duration(value: number | null) { return value === null ? '—' : `${value} ${copy.value.seconds}` }
</script>

<template>
  <section v-if="returns.length" class="return-support-timeline" aria-labelledby="return-support-timeline-title">
    <h3 id="return-support-timeline-title">{{ copy.title }}</h3>
    <article v-for="item in returns" :key="item.id">
      <h4>{{ stateName(item.state) }} · <code>{{ item.id }}</code></h4>
      <p>{{ copy.classification }}: {{ item.caseSummary.classification }}</p>
      <dl>
        <dt>{{ copy.opened }}</dt><dd><time :datetime="item.caseSummary.openedAt || undefined">{{ date(item.caseSummary.openedAt) }}</time></dd>
        <dt>{{ copy.firstResponse }}</dt><dd><time :datetime="item.caseSummary.firstResponseAt || undefined">{{ date(item.caseSummary.firstResponseAt) }}</time> · {{ duration(item.caseSummary.firstResponseElapsedSeconds) }}</dd>
        <template v-if="item.caseSummary.escalatedAt">
          <dt>{{ copy.escalated }}</dt><dd><time :datetime="item.caseSummary.escalatedAt">{{ date(item.caseSummary.escalatedAt) }}</time> · {{ duration(item.caseSummary.escalationElapsedSeconds) }}</dd>
        </template>
      </dl>
      <ol>
        <li v-for="(event, index) in item.timeline" :key="`${event.state}-${event.at}-${index}`">
          {{ stateName(event.state) }}<template v-if="event.at"> · <time :datetime="event.at">{{ date(event.at) }}</time></template><p v-if="event.message">{{ event.message }}</p>
        </li>
      </ol>
      <p v-if="item.inspectionSummary">
        {{ copy.inspection }}: {{ copy.resellable }} {{ item.inspectionSummary.resellableQuantity }} · {{ copy.quarantined }} {{ item.inspectionSummary.quarantinedQuantity }}
      </p>
    </article>
  </section>
</template>
