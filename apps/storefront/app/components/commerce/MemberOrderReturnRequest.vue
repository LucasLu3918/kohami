<script setup lang="ts">
import type { MemberOrderDetail } from '../../composables/useMemberOrderDetails'
import { formatProductNumber } from '../../utils/product-number'
import { localizedProductColor } from '../../utils/product-color'

const props = defineProps<{
  lines: Array<MemberOrderDetail['snapshot']['lines'][number] & { returnableQuantity: number }>
  locale: 'zh-TW' | 'ja' | 'en'
  pending?: boolean
  error?: 'stale' | 'invalid' | 'unavailable' | null
}>()
const emit = defineEmits<{
  requestReturn: [request: { orderLineId: string; quantity: number; reason: string }]
}>()

const eligibleLines = computed(() => props.lines.filter(line => line.returnableQuantity > 0))
const selectedLineId = shallowRef(eligibleLines.value[0]?.orderLineId || '')
const quantity = shallowRef(1)
const reason = shallowRef('')
const validationError = shallowRef(false)
const selectedLine = computed(() => eligibleLines.value.find(line => line.orderLineId === selectedLineId.value) || null)
watch(eligibleLines, lines => {
  if (!lines.some(line => line.orderLineId === selectedLineId.value)) {
    selectedLineId.value = lines[0]?.orderLineId || ''
    quantity.value = 1
  } else if (selectedLine.value && quantity.value > selectedLine.value.returnableQuantity) {
    quantity.value = selectedLine.value.returnableQuantity
  }
}, { deep: true })
watch(selectedLineId, () => { quantity.value = 1 })
const labels = computed(() => props.locale === 'en'
  ? { title: 'Request a return', item: 'Item', productNumber: 'Product no.', color: 'Color', size: 'Size', quantity: 'Quantity', reason: 'Reason', submit: 'Submit return request', pending: 'Submitting return request…', required: 'Choose an item, enter a quantity within the remaining return quantity, and provide a reason.', stale: 'The order changed. Details were refreshed; review them before submitting again.', invalid: 'Please check the return details and try again.', unavailable: 'The return request could not be submitted. Your entries are saved; review the details before trying again.' }
  : props.locale === 'ja'
    ? { title: '返品を申請', item: '商品', productNumber: '商品番号', color: '色', size: 'サイズ', quantity: '数量', reason: '理由', submit: '返品を申請する', pending: '返品を申請中…', required: '商品を選び、残りの返品可能数量以内の数量と理由を入力してください。', stale: '注文内容が変更されました。詳細を更新したので、確認してから再申請してください。', invalid: '返品内容を確認して、もう一度お試しください。', unavailable: '返品を申請できませんでした。入力内容は保持されています。内容を確認してから再試行してください。' }
    : { title: '申請退貨', item: '商品', productNumber: '商品編號', color: '顏色', size: '尺寸', quantity: '數量', reason: '退貨原因', submit: '送出退貨申請', pending: '正在送出退貨申請…', required: '請選擇商品、輸入不超過剩餘可退件數的數量，並填寫原因。', stale: '訂單狀態已變更，明細已更新。請確認明細後再重新送出。', invalid: '請檢查退貨資料後再試一次。', unavailable: '退貨申請未能送出，已保留輸入內容；請確認資料後再試。' })

function displayLine(line: MemberOrderDetail['snapshot']['lines'][number]) {
  const name = line.name?.value || ''
  const color = localizedProductColor(line.colorId, props.locale)
  return [name, `${labels.value.productNumber}: ${formatProductNumber(line.skuId)}`, color ? `${labels.value.color}: ${color}` : '', line.garmentSize ? `${labels.value.size}: ${line.garmentSize}` : ''].filter(Boolean).join(' · ')
}

function submit() {
  const line = selectedLine.value
  const normalizedReason = reason.value.trim()
  if (!line || !Number.isInteger(quantity.value) || quantity.value < 1 || quantity.value > line.returnableQuantity || !normalizedReason || normalizedReason.length > 500) {
    validationError.value = true
    return
  }
  validationError.value = false
  emit('requestReturn', { orderLineId: line.orderLineId, quantity: quantity.value, reason: normalizedReason })
}
</script>

<template>
  <form class="return-request" :aria-label="labels.title" novalidate @submit.prevent="submit">
    <h4>{{ labels.title }}</h4>
    <label>
      <span>{{ labels.item }}</span>
      <select v-model="selectedLineId" :disabled="pending" required>
        <option v-for="line in eligibleLines" :key="line.orderLineId" :value="line.orderLineId">{{ displayLine(line) }}</option>
      </select>
    </label>
    <label>
      <span>{{ labels.quantity }}</span>
      <input v-model.number="quantity" type="number" min="1" :max="selectedLine?.returnableQuantity || 1" step="1" :disabled="pending" required />
    </label>
    <label>
      <span>{{ labels.reason }}</span>
      <textarea v-model="reason" maxlength="500" rows="3" :disabled="pending" required />
    </label>
    <p v-if="pending" role="status">{{ labels.pending }}</p>
    <p v-if="validationError" role="alert">{{ labels.required }}</p>
    <p v-else-if="error" role="alert">{{ labels[error] }}</p>
    <button type="submit" :disabled="pending || !eligibleLines.length">{{ pending ? labels.pending : labels.submit }}</button>
  </form>
</template>

<style scoped>
.return-request{display:grid;gap:.75rem;border:1px solid var(--store-border);border-radius:.375rem;padding:1rem}.return-request h4{margin:0}.return-request label{display:grid;gap:.25rem}.return-request input,.return-request select,.return-request textarea{box-sizing:border-box;width:100%;min-height:2.75rem;padding:.55rem;border:1px solid #817a75;border-radius:.25rem;font:inherit}.return-request textarea{min-height:5rem;resize:vertical}.return-request button{justify-self:start;min-height:2.75rem;padding:.55rem 1rem;background:var(--store-accent-strong);color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.return-request button:disabled{background:var(--store-paper);color:var(--store-muted-strong);box-shadow:inset 0 0 0 1px var(--store-border);cursor:progress}.return-request :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}.return-request [role=alert]{color:var(--store-danger);margin:0}
</style>
