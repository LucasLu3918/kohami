<script setup lang="ts">
import ReconciliationCaseCard, { type ReconciliationAction, type ReconciliationFinding } from './ReconciliationCaseCard.vue'

type ReconciliationReport = {
  mode: 'mock'
  readOnly: true
  scheduleStatus: 'ManualOnly'
  state: string
  summary: Record<string, number>
  findings: ReconciliationFinding[]
  readyToClose: ReconciliationFinding[]
}
type ReconciliationNotification = { id: string; caseId: string; findingCode: string; createdAt: string; channel: 'InApp' }

const emit = defineEmits<{
  resolve: [payload: { finding: ReconciliationFinding; action: ReconciliationAction; reason: string; evidenceRef: string; compensationCaseId?: string }]
  assign: [payload: { finding: ReconciliationFinding; assigneeEmail: string; reason: string }]
}>()
const copy = {
  'zh-TW': { title: '人工對帳處置工作台', mode: '本機模擬', readOnly: '來源報表為唯讀快照；處置只記錄查核與證據，不修改金融狀態。', schedule: '每日排程未啟用', empty: '目前沒有待處理差異。', ready: '差異已消失，可依查核證據結案', error: '處置失敗，請重新載入案件後再試。', notifications: '重大差異站內通知', noNotifications: '沒有指派給你的重大差異通知。', notification: '已指派重大差異' },
  ja: { title: '手動照合処置ワークベンチ', mode: 'ローカルシミュレーション', readOnly: '元レポートは読み取り専用スナップショットです。処置は確認記録と証拠のみを保存し、金融状態は変更しません。', schedule: '日次スケジュールは無効です', empty: '対応が必要な差異はありません。', ready: '差異は解消済みです。確認証拠に基づいてクローズできます。', error: '処置に失敗しました。案件を再読み込みしてから再試行してください。', notifications: '重大な差異の通知', noNotifications: '割り当てられた重大な差異通知はありません。', notification: '重大な差異が割り当てられました' },
  en: { title: 'Manual reconciliation workbench', mode: 'Local simulation', readOnly: 'The source report is a read-only snapshot. Resolutions record review and evidence without changing financial state.', schedule: 'Daily schedule is disabled', empty: 'There are no findings to review.', ready: 'Finding cleared; close after reviewing the evidence', error: 'Resolution failed. Reload the case before trying again.', notifications: 'Major exception notifications', noNotifications: 'No major exception notifications are assigned to you.', notification: 'Major exception assigned' },
} as const
const props = defineProps<{
  report: ReconciliationReport
  notifications: ReconciliationNotification[]
  locale: 'zh-TW' | 'ja' | 'en'
  pending: boolean
  error?: string
}>()
const labels = computed(() => copy[props.locale])
const summaryRows = computed(() => Object.entries(props.report.summary))
</script>

<template>
  <section aria-labelledby="reconciliation-workbench-heading" class="reconciliation-workbench">
    <h3 id="reconciliation-workbench-heading">{{ labels.title }}</h3>
    <p>{{ labels.mode }} · {{ labels.schedule }}</p>
    <p>{{ labels.readOnly }}</p>
    <section aria-labelledby="reconciliation-notifications-heading" class="notifications">
      <h4 id="reconciliation-notifications-heading">{{ labels.notifications }}</h4>
      <p v-if="notifications.length === 0">{{ labels.noNotifications }}</p>
      <ul v-else>
        <li v-for="item in notifications" :key="item.id">
          {{ labels.notification }}: {{ item.findingCode }} · {{ item.caseId }} · {{ item.createdAt }}
        </li>
      </ul>
    </section>
    <p>State: {{ report.state }}</p>
    <dl class="summary">
      <template v-for="[key, value] in summaryRows" :key="key"><dt>{{ key }}</dt><dd>{{ value }}</dd></template>
    </dl>
    <p v-if="error" class="error" role="alert">{{ error || labels.error }}</p>
    <p v-if="report.findings.length === 0 && report.readyToClose.length === 0">{{ labels.empty }}</p>
    <ReconciliationCaseCard
      v-for="finding in report.findings"
      :key="finding.caseId"
      :finding="finding"
      :locale="locale"
      :pending="pending"
      :allowed-actions="['QueueOriginalCheck', 'LinkCompensationCase', 'RecordReviewedNotApplicable']"
      @resolve="emit('resolve', $event)"
      @assign="emit('assign', $event)"
    />
    <template v-if="report.readyToClose.length">
      <h4>{{ labels.ready }}</h4>
      <ReconciliationCaseCard
        v-for="finding in report.readyToClose"
        :key="finding.caseId"
        :finding="finding"
        :locale="locale"
        :pending="pending"
        :allowed-actions="['CloseReconciled']"
        @resolve="emit('resolve', $event)"
        @assign="emit('assign', $event)"
      />
    </template>
  </section>
</template>

<style scoped>
.reconciliation-workbench{padding-top:1rem}.summary{display:grid;grid-template-columns:minmax(12rem,auto) 1fr;gap:.35rem .75rem}.summary dd{margin:0}.error{color:#711010}.notifications{border-left:.25rem solid #95652f;padding-left:.75rem;margin:.75rem 0}
</style>
