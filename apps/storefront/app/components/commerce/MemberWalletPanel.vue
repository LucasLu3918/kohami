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
}>()

const emit = defineEmits<{
  loadMore: []
}>()

const labels = computed(() => props.locale === 'en'
  ? {
      title: 'Wallet balance', mock: 'Synthetic test balance — no real funds.', amount: 'Available balance', ledger: 'Balance ledger',
      empty: 'No ledger entries yet.', loading: 'Loading wallet…', auth: 'Please sign in again to view your wallet.',
      failure: 'Wallet service is temporarily unavailable.', moreFailure: 'More ledger entries could not be loaded.', more: 'Load more', loadingMore: 'Loading more…',
      date: 'Date', credit: 'Synthetic credit', debit: 'Checkout debit', other: 'Wallet entry',
    }
  : props.locale === 'ja'
    ? {
        title: 'ウォレット残高', mock: '合成テスト残高 — 実際の資金ではありません。', amount: '利用可能残高', ledger: '残高台帳',
        empty: '台帳の記録はありません。', loading: 'ウォレットを読み込み中…', auth: 'ウォレットを表示するには再度ログインしてください。',
        failure: 'ウォレットサービスを一時的に利用できません。', moreFailure: '続きの記録を読み込めませんでした。', more: '続きを読み込む', loadingMore: '読み込み中…',
        date: '日時', credit: '合成入金', debit: '購入時の残高利用', other: '残高記録',
      }
    : {
        title: '會員儲值餘額', mock: '合成測試餘額，非實際資金。', amount: '可用餘額', ledger: '餘額帳本',
        empty: '目前沒有帳本紀錄。', loading: '正在載入錢包…', auth: '請重新登入以查看錢包。',
        failure: '錢包服務暫時無法使用。', moreFailure: '無法載入更多帳本紀錄。', more: '載入更多', loadingMore: '載入中…',
        date: '時間', credit: '合成入帳', debit: '結帳扣抵', other: '餘額紀錄',
      })

function formatAmount(amount: number, currency: string) {
  return `${currency} ${new Intl.NumberFormat(props.locale).format(amount)}`
}

function entryTypeLabel(type: string) {
  if (type === 'SyntheticCredit') return labels.value.credit
  if (type === 'CheckoutDebit') return labels.value.debit
  return labels.value.other
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(props.locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}
</script>

<template>
  <section class="wallet-panel" aria-labelledby="wallet-heading" aria-describedby="wallet-mock-note">
    <h2 id="wallet-heading">{{ labels.title }}</h2>
    <p id="wallet-mock-note" class="wallet-note">{{ labels.mock }}</p>
    <p v-if="loading" role="status">{{ labels.loading }}</p>
    <p v-else-if="error === 'AUTHENTICATION_REQUIRED'" role="alert">{{ labels.auth }}</p>
    <p v-else-if="error === 'SERVICE_UNAVAILABLE' && !snapshot" role="alert">{{ labels.failure }}</p>
    <template v-else-if="snapshot">
      <div class="wallet-balance">
        <p class="wallet-amount-label">{{ labels.amount }}</p>
        <p class="wallet-amount">{{ formatAmount(snapshot.availableTwd, snapshot.currency) }}</p>
      </div>
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
.wallet-panel{background:#fff;padding:1.25rem;margin:1rem 0;border:1px solid #8a817b;border-radius:.5rem;color:#25252c}
.wallet-panel h2,.wallet-ledger-heading{margin-block-start:0}
.wallet-note{color:#625c58}
.wallet-balance{padding:1rem;background:#f7f1ef;border-radius:.35rem}
.wallet-amount-label,.wallet-amount{margin:.25rem 0}
.wallet-amount{font-size:1.5rem;font-weight:700;font-variant-numeric:tabular-nums;overflow-wrap:anywhere}
.wallet-ledger-heading{margin-block:1.25rem .5rem}
.wallet-entries{list-style:none;padding:0;display:grid;gap:.5rem}
.wallet-entry{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:.75rem 0;border-block-end:1px solid #ded9d5}
.wallet-entry-type,.wallet-entry-date{margin:0;overflow-wrap:anywhere}
.wallet-entry-date{font-size:.9rem;color:#625c58}
.wallet-entry-amount{font-weight:600;white-space:nowrap;font-variant-numeric:tabular-nums}
.wallet-empty{color:#625c58}
.wallet-more{min-height:44px;padding:.65rem 1rem;background:#6d2942;color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}
.wallet-more:disabled{background:#625c58;opacity:1;cursor:not-allowed}
.wallet-panel :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
@media(max-width:20rem){.wallet-entry{align-items:flex-start;flex-direction:column;gap:.35rem}.wallet-entry-amount{white-space:normal}}
</style>
