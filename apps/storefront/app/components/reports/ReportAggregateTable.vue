<script setup lang="ts">
import type { ReportRow } from '../../types/reports'

defineProps<{ rows: ReportRow[]; cutoff: string; notice: string }>()
</script>

<template>
  <section class="report-aggregate" aria-label="日期聚合報表">
    <p class="report-cutoff">{{ notice }} · 截止 {{ cutoff }}</p>
    <div class="report-table-wrap">
      <table>
        <thead><tr><th>UTC 訂單日期</th><th>收款類型</th><th>訂單數</th><th>已收 TWD</th><th>成功退款 TWD</th><th>待處理 TWD</th><th>可用 TWD</th><th>淨收 TWD</th><th>待補償訂單數</th></tr></thead>
        <tbody>
          <tr v-for="row in rows" :key="`${row.date}-${row.sourceKind}`">
            <td>{{ row.date }}</td><td>{{ row.sourceKind }}</td><td>{{ row.orderCount }}</td>
            <td>{{ row.collectedTwd }}</td><td>{{ row.succeededRefundTwd }}</td><td>{{ row.pendingTwd }}</td>
            <td>{{ row.availableTwd }}</td><td>{{ row.netConfirmedTwd }}</td><td>{{ row.discrepancyCount }}</td>
          </tr>
          <tr v-if="rows.length === 0"><td colspan="9">此日期範圍沒有聚合資料。</td></tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.report-table-wrap { overflow-x: auto; }
.report-table-wrap table { border-collapse: collapse; min-width: 68rem; width: 100%; }
.report-table-wrap th, .report-table-wrap td { border: 1px solid #bbb; padding: .5rem; text-align: right; }
.report-table-wrap th:first-child, .report-table-wrap td:first-child, .report-table-wrap th:nth-child(2), .report-table-wrap td:nth-child(2) { text-align: left; }
</style>
