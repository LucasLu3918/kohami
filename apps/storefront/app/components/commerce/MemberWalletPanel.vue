<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { StorefrontLocale } from '../../utils/storefront-locale'
import type { MemberWalletError, MemberWalletSnapshot } from '../../composables/useMemberWallet'

const props = defineProps<{
  snapshot: DeepReadonly<MemberWalletSnapshot> | null
  loading: boolean
  loadingMore: boolean
  error: MemberWalletError | null
  locale: StorefrontLocale
  topUpPending: boolean
  topUpError: string
}>()

const emit = defineEmits<{
  loadMore: []
  topUp: [amountTwd: number, cardNumber: string]
}>()
const topUpAmount = ref(1000)
const cardNumber = ref('')

const labels = computed(() => props.locale === 'en'
  ? {
      title: 'Wallet balance', note: 'Balance is recorded in the system; external deposits are not connected.', amount: 'Available balance', ledger: 'Balance ledger',
      empty: 'No ledger entries yet.', loading: 'Loading wallet…', auth: 'Please sign in again to view your wallet.',
      failure: 'Wallet service is temporarily unavailable.', moreFailure: 'More ledger entries could not be loaded.', more: 'Load more', loadingMore: 'Loading more…',
      date: 'Date', credit: 'System credit', memberTopUp: 'Card deposit', adminCredit: 'Admin credit', debit: 'Checkout debit', other: 'Wallet entry', topUp: 'Add balance', topUpAmount: 'Amount (TWD)', cardNumber: 'Card number', fillTestCard: 'Fill sample details', submitTopUp: 'Add balance', topUpNotice: 'Bank payment is not connected. Do not enter a real card number.', topUpPending: 'Adding…',
    }
  : props.locale === 'ja'
    ? {
        title: 'ウォレット残高', note: '残高はシステムに記録されます。外部入金サービスは未接続です。', amount: '利用可能残高', ledger: '残高台帳',
        empty: '台帳の記録はありません。', loading: 'ウォレットを読み込み中…', auth: 'ウォレットを表示するには再度ログインしてください。',
        failure: 'ウォレットサービスを一時的に利用できません。', moreFailure: '続きの記録を読み込めませんでした。', more: '続きを読み込む', loadingMore: '読み込み中…',
        date: '日時', credit: 'システム入金', memberTopUp: 'カード入金', adminCredit: '管理者入金', debit: '購入時の残高利用', other: '残高記録', topUp: '残高を追加', topUpAmount: '金額 (TWD)', cardNumber: 'カード番号', fillTestCard: 'サンプル情報を入力', submitTopUp: '残高を追加', topUpNotice: '銀行決済は未接続です。実際のカード番号を入力しないでください。', topUpPending: '追加中…',
      }
    : {
        title: '會員儲值餘額', note: '餘額異動記錄於系統；外部收款服務尚未連接。', amount: '可用餘額', ledger: '餘額帳本',
        empty: '目前沒有帳本紀錄。', loading: '正在載入錢包…', auth: '請重新登入以查看錢包。',
        failure: '錢包服務暫時無法使用。', moreFailure: '無法載入更多帳本紀錄。', more: '載入更多', loadingMore: '載入中…',
        date: '時間', credit: '系統入帳', memberTopUp: '卡片加值', adminCredit: '管理員手動加值', debit: '結帳扣抵', other: '餘額紀錄', topUp: '新增餘額', topUpAmount: '加值金額 (TWD)', cardNumber: '卡號', fillTestCard: '帶入卡號', submitTopUp: '新增餘額', topUpNotice: '銀行收款服務尚未連接，請勿輸入真實卡號。', topUpPending: '加值中…',
      })

function formatAmount(amount: number, currency: string) {
  return `${currency} ${new Intl.NumberFormat(props.locale).format(amount)}`
}

function entryTypeLabel(type: string) {
  if (type === 'SyntheticCredit') return labels.value.credit
  if (type === 'MemberTopUp') return labels.value.memberTopUp
  if (type === 'AdminCredit') return labels.value.adminCredit
  if (type === 'CheckoutDebit') return labels.value.debit
  return labels.value.other
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(props.locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}
function fillTestCard() { cardNumber.value = '4111111111111111' }
function submitTopUp() {
  if (props.topUpPending || !Number.isInteger(topUpAmount.value) || topUpAmount.value < 1 || topUpAmount.value > 50000 || !cardNumber.value) return
  const number = cardNumber.value
  cardNumber.value = ''
  emit('topUp', topUpAmount.value, number)
}
</script>

<template>
  <section class="wallet-panel" aria-labelledby="wallet-heading" aria-describedby="wallet-data-note">
    <h2 id="wallet-heading">{{ labels.title }}</h2>
    <p id="wallet-data-note" class="wallet-note">{{ labels.note }}</p>
    <p v-if="loading" role="status">{{ labels.loading }}</p>
    <p v-else-if="error === 'AUTHENTICATION_REQUIRED'" role="alert">{{ labels.auth }}</p>
    <p v-else-if="error === 'SERVICE_UNAVAILABLE' && !snapshot" role="alert">{{ labels.failure }}</p>
    <template v-else-if="snapshot">
      <div class="wallet-balance">
        <p class="wallet-amount-label">{{ labels.amount }}</p>
        <p class="wallet-amount">{{ formatAmount(snapshot.availableTwd, snapshot.currency) }}</p>
      </div>
      <form class="wallet-topup" @submit.prevent="submitTopUp">
        <h3>{{ labels.topUp }}</h3>
        <label>{{ labels.topUpAmount }}<input v-model.number="topUpAmount" type="number" min="1" max="50000" step="1" :disabled="topUpPending" /></label>
        <label>{{ labels.cardNumber }}<input v-model.trim="cardNumber" inputmode="numeric" maxlength="19" autocomplete="off" :disabled="topUpPending" /></label>
        <button type="button" :disabled="topUpPending" @click="fillTestCard">{{ labels.fillTestCard }}</button>
        <p>{{ labels.topUpNotice }}</p>
        <p v-if="topUpError" role="alert">{{ topUpError }}</p>
        <button type="submit" :disabled="topUpPending || !cardNumber">{{ topUpPending ? labels.topUpPending : labels.submitTopUp }}</button>
      </form>
      <h3 class="wallet-ledger-heading">{{ labels.ledger }}</h3>
      <p v-if="error === 'SERVICE_UNAVAILABLE'" role="alert">{{ labels.moreFailure }}</p>
      <p v-if="snapshot.entries.length === 0" class="wallet-empty">{{ labels.empty }}</p>
      <ol v-else class="wallet-entries">
        <li v-for="entry in snapshot.entries" :key="entry.id" class="wallet-entry">
          <div>
            <p class="wallet-entry-type">{{ entryTypeLabel(entry.type) }}</p>
            <time class="wallet-entry-date" :datetime="entry.createdAt">{{ labels.date }}：{{ formatDate(entry.createdAt) }}</time>
          </div>
          <span class="wallet-entry-amount">{{ formatAmount(entry.amountTwd, entry.currency) }}</span>
        </li>
      </ol>
      <button v-if="snapshot.nextCursor" class="wallet-more" :disabled="loadingMore" @click="emit('loadMore')">
        {{ loadingMore ? labels.loadingMore : labels.more }}
      </button>
    </template>
    <p v-else-if="error === 'SERVICE_UNAVAILABLE'" role="alert">{{ labels.failure }}</p>
  </section>
</template>

<style scoped>
.wallet-panel{background:#fff;padding:1.25rem;margin:1rem 0;border:1px solid var(--store-border-strong);border-radius:.5rem;color:var(--store-ink-strong)}
.wallet-panel h2,.wallet-ledger-heading{margin-block-start:0}
.wallet-note{color:var(--store-muted-strong)}
.wallet-balance{padding:1rem;background:#f7f1ef;border-radius:.35rem}
.wallet-topup{display:grid;max-width:32rem;gap:.6rem;margin-block:1rem;padding:1rem;border:1px solid var(--store-border);border-radius:.35rem}.wallet-topup h3,.wallet-topup p{margin:.2rem 0}.wallet-topup label{display:grid;gap:.35rem}.wallet-topup input{box-sizing:border-box;width:100%;min-height:2.75rem;padding:.5rem;font:inherit}.wallet-topup button{min-height:2.75rem;padding:.5rem 1rem;background:var(--store-accent-strong);color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}
.wallet-amount-label,.wallet-amount{margin:.25rem 0}
.wallet-amount{font-size:1.5rem;font-weight:700;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}
.wallet-ledger-heading{margin-block:1.25rem .5rem}
.wallet-entries{list-style:none;padding:0;display:grid;gap:.5rem}
.wallet-entry{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:.75rem 0;border-block-end:1px solid var(--store-border)}
.wallet-entry-type,.wallet-entry-date{margin:0;overflow-wrap:anywhere}
.wallet-entry-date{font-size:.9rem;color:var(--store-muted-strong)}
.wallet-entry-amount{font-weight:600;white-space:nowrap;font-variant-numeric:tabular-nums}
.wallet-empty{color:var(--store-muted-strong)}
.wallet-more{min-height:44px;padding:.65rem 1rem;background:var(--store-accent-strong);color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}
.wallet-more:disabled{background:var(--store-muted-strong);opacity:1;cursor:not-allowed}
.wallet-panel :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
@media(max-width:20rem){.wallet-entry{align-items:flex-start;flex-direction:column;gap:.35rem}.wallet-entry-amount{white-space:normal}}
</style>
