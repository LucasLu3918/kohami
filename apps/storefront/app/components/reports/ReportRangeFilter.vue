<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ from: string; to: string; pending: boolean }>()
const emit = defineEmits<{
  'update:from': [value: string]
  'update:to': [value: string]
  submit: []
}>()
const rangeIsValid = computed(() => {
  const start = Date.parse(`${props.from}T00:00:00Z`)
  const end = Date.parse(`${props.to}T00:00:00Z`)
  return Number.isFinite(start) && Number.isFinite(end) && end >= start && (end - start) / 86_400_000 <= 30
})
</script>

<template>
  <form class="report-range-filter" @submit.prevent="emit('submit')">
    <label>開始日期（UTC）<input :value="from" type="date" required @input="emit('update:from', ($event.target as HTMLInputElement).value)"></label>
    <label>結束日期（UTC）<input :value="to" type="date" required @input="emit('update:to', ($event.target as HTMLInputElement).value)"></label>
    <button :disabled="pending || !rangeIsValid">{{ pending ? '載入中…' : '查詢摘要' }}</button>
    <p v-if="!rangeIsValid" class="range-error" role="alert">日期範圍需有效、先後正確，且最多 31 天。</p>
  </form>
</template>

<style scoped>
.report-range-filter { display: flex; flex-wrap: wrap; align-items: end; gap: 1rem; margin-block: 1.5rem; }
.report-range-filter label { display: grid; gap: .35rem; }
.report-range-filter input, .report-range-filter button { padding: .6rem; font: inherit; }
.range-error { flex-basis: 100%; color: #9b1c1c; }
</style>
