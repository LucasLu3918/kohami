<script setup lang="ts">
import { shallowRef } from 'vue'

export type ReconciliationAction = 'QueueOriginalCheck' | 'LinkCompensationCase' | 'RecordReviewedNotApplicable' | 'CloseReconciled'
export type ReconciliationFinding = {
  caseId: string
  code: string
  disposition: string
  responsibleActor?: string | null
  version: string
  sourceId?: string
  orderId?: string
  operationId?: string
  compensationCaseId?: string
  [key: string]: unknown
}

const props = defineProps<{ finding: ReconciliationFinding; locale: 'zh-TW' | 'ja' | 'en'; pending: boolean; allowedActions?: ReconciliationAction[] }>()
const emit = defineEmits<{
  resolve: [payload: { finding: ReconciliationFinding; action: ReconciliationAction; reason: string; evidenceRef: string; compensationCaseId?: string }]
}>()

const action = shallowRef<ReconciliationAction>(props.allowedActions?.[0] || 'QueueOriginalCheck')
const reason = shallowRef('')
const evidenceRef = shallowRef('')
const compensationCaseId = shallowRef('')
const copy = {
  'zh-TW': { action: '處置方式', disposition: '處置狀態', actor: '最近處置人', evidence: '證據參照 ID', reason: '處置原因', compensation: '補償案件 ID', submit: '送出處置', actions: { QueueOriginalCheck: '排入原始資料查核', LinkCompensationCase: '連結補償案件', RecordReviewedNotApplicable: '記錄為已檢視且不適用', CloseReconciled: '以查核結果結案' } },
  ja: { action: '処置方法', disposition: '処置状態', actor: '最終処置者', evidence: '証拠参照 ID', reason: '処置理由', compensation: '補償案件 ID', submit: '処置を送信', actions: { QueueOriginalCheck: '元データの確認を依頼', LinkCompensationCase: '補償案件をリンク', RecordReviewedNotApplicable: '確認済み・対象外として記録', CloseReconciled: '確認結果でクローズ' } },
  en: { action: 'Action', disposition: 'Disposition', actor: 'Last handled by', evidence: 'Evidence reference ID', reason: 'Reason', compensation: 'Compensation case ID', submit: 'Submit resolution', actions: { QueueOriginalCheck: 'Queue original source check', LinkCompensationCase: 'Link compensation case', RecordReviewedNotApplicable: 'Mark reviewed as not applicable', CloseReconciled: 'Close after reconciliation' } },
} as const
const labels = computed(() => copy[props.locale])
const actions = computed<ReconciliationAction[]>(() => props.allowedActions || ['QueueOriginalCheck', 'LinkCompensationCase', 'RecordReviewedNotApplicable', 'CloseReconciled'])

function submit() {
  emit('resolve', {
    finding: props.finding,
    action: action.value,
    reason: reason.value.trim(),
    evidenceRef: evidenceRef.value.trim(),
    ...(action.value === 'LinkCompensationCase' ? { compensationCaseId: compensationCaseId.value.trim() } : {}),
  })
}
</script>

<template>
  <article class="reconciliation-case" :data-case-id="finding.caseId">
    <h4>{{ finding.code }}</h4>
    <dl>
      <dt>Case</dt><dd>{{ finding.caseId }}</dd>
      <template v-for="key in ['sourceId', 'orderId', 'operationId'] as const" :key="key">
        <template v-if="finding[key]"><dt>{{ key }}</dt><dd>{{ finding[key] }}</dd></template>
      </template>
      <dt>{{ labels.disposition }}</dt><dd>{{ finding.disposition }}</dd>
      <template v-if="finding.responsibleActor"><dt>{{ labels.actor }}</dt><dd>{{ finding.responsibleActor }}</dd></template>
    </dl>
    <form @submit.prevent="submit">
      <label>{{ labels.action }}
        <select v-model="action">
          <option v-for="option in actions" :key="option" :value="option">{{ labels.actions[option] }}</option>
        </select>
      </label>
      <label v-if="action === 'LinkCompensationCase'">{{ labels.compensation }}
        <input v-model="compensationCaseId" required maxlength="100" pattern="[A-Za-z0-9_-]+" />
      </label>
      <label>{{ labels.reason }}<input v-model="reason" required maxlength="500" /></label>
      <label>{{ labels.evidence }}<input v-model="evidenceRef" required maxlength="100" pattern="[A-Za-z0-9_-]+" /></label>
      <button :disabled="pending || !finding.version">{{ labels.submit }}</button>
    </form>
  </article>
</template>

<style scoped>
.reconciliation-case{border:1px solid #d9d1cb;border-radius:.4rem;padding:1rem;margin:.75rem 0}
dl{display:grid;grid-template-columns:minmax(8rem,auto) 1fr;gap:.35rem .75rem;overflow-wrap:anywhere}
dt{font-weight:600}dd{margin:0}
</style>
