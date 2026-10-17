<script setup lang="ts">
import { shallowRef } from 'vue'
import ReportAggregateTable from '../../components/reports/ReportAggregateTable.vue'
import ReportRangeFilter from '../../components/reports/ReportRangeFilter.vue'
import type { AggregateReport } from '../../types/reports'

const todayUtc = new Date().toISOString().slice(0, 10)
function shiftUtcDate(value: string, days: number) {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}
const from = shallowRef(shiftUtcDate(todayUtc, -6))
const to = shallowRef(todayUtc)
const report = shallowRef<AggregateReport | null>(null)
const pending = shallowRef(false)
const error = shallowRef('')

async function loadReport() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  report.value = null
  try {
    report.value = await $fetch<AggregateReport>(`/api/v1/admin/reports?from=${encodeURIComponent(from.value)}&to=${encodeURIComponent(to.value)}`)
  } catch (cause: any) {
    error.value = cause?.data?.code === 'FORBIDDEN' || cause?.statusCode === 403
      ? '目前帳號沒有此報表的讀取權限。'
      : '目前無法載入本機報表，請稍後重試。'
  } finally {
    pending.value = false
  }
}

onMounted(loadReport)
</script>

<template>
  <main class="reports-admin">
    <NuxtLink to="/">← 返回商店</NuxtLink>
    <h1>本機營運摘要</h1>
    <p class="report-note" role="note">合成資料聚合示意，非正式財務報表。日期依 UTC 訂單建立日分組，收款與退款不代表入帳日；僅提供日期與收款類型彙總，不含個人資料或訂單明細。</p>
    <ReportRangeFilter v-model:from="from" v-model:to="to" :pending="pending" @submit="loadReport" />
    <p v-if="error" class="report-error" role="alert">{{ error }}</p>
    <p v-if="pending" class="report-status" role="status">正在載入聚合摘要…</p>
    <ReportAggregateTable v-if="report" :rows="report.rows" :cutoff="report.cutoff" :notice="report.notice" />
  </main>
</template>

<style scoped>
.reports-admin { max-width: 76rem; margin: 2rem auto; padding: 1rem; font-family: system-ui, sans-serif; }
.report-error { color: #9b1c1c; }
</style>
