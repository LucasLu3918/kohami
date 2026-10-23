<script setup lang="ts">
import { computed } from 'vue'

type Locale = 'zh-TW' | 'ja' | 'en'

const props = defineProps<{
  locale: Locale
  availableTwd: number | null
  loading: boolean
  errorCode: string | null
  orderState: string | null
  paymentState: string | null
}>()

const copy = computed(() => props.locale === 'zh-TW'
  ? {
      title: '會員餘額支付', balance: '可用會員餘額', loading: '正在載入會員餘額…', unavailable: '目前無法讀取會員餘額，請重新整理後再試。',
      insufficient: '會員餘額不足，訂單尚未建立，錢包未扣款。', success: '錢包扣抵成功，訂單已確認。',
      pending: '會員錢包使用系統記錄的餘額；外部信用卡收款服務尚未連接。',
    }
  : props.locale === 'ja'
    ? {
        title: '会員残高で支払う', balance: '利用可能な会員残高', loading: '会員残高を読み込み中…', unavailable: '会員残高を読み込めません。再読み込みしてからお試しください。',
        insufficient: '会員残高が不足しています。注文は作成されず、残高も引き落とされていません。', success: 'ウォレット決済が完了し、注文が確定しました。',
        pending: '会員ウォレットはシステムに記録された残高を使用します。外部カード決済サービスは未接続です。',
      }
    : {
        title: 'Member wallet payment', balance: 'Available member balance', loading: 'Loading member balance…', unavailable: 'The member balance is unavailable. Reload and try again.',
        insufficient: 'Your member balance is insufficient. No order was created and no wallet funds were debited.', success: 'Wallet payment succeeded; the order is confirmed.',
        pending: 'Member wallet payments use the balance recorded by this system. External card processing is not connected.',
      })

const formattedBalance = computed(() => props.availableTwd === null
  ? ''
  : props.availableTwd.toLocaleString(props.locale))
const isPaid = computed(() => props.orderState === 'Confirmed' && props.paymentState === 'Succeeded')
</script>

<template>
  <section class="member-wallet-checkout" aria-labelledby="member-wallet-checkout-heading">
    <h3 id="member-wallet-checkout-heading">{{ copy.title }}</h3>
    <p v-if="loading" role="status">{{ copy.loading }}</p>
    <p v-else-if="availableTwd !== null" class="member-wallet-balance">
      {{ copy.balance }}: TWD {{ formattedBalance }}
    </p>
    <p v-else role="status">{{ copy.unavailable }}</p>
    <p class="member-wallet-note">{{ copy.pending }}</p>
    <p v-if="errorCode === 'WALLET_INSUFFICIENT_BALANCE'" class="member-wallet-error" role="alert">
      {{ copy.insufficient }}
    </p>
    <p v-if="isPaid" class="member-wallet-success" role="status">{{ copy.success }}</p>
  </section>
</template>

<style scoped>
.member-wallet-checkout { margin-block: 1rem; padding: 1rem; border: 1px solid var(--store-border-strong); border-radius: .5rem; }
.member-wallet-checkout h3 { margin-top: 0; }
.member-wallet-balance { font-weight: 700; }
.member-wallet-note { color: var(--store-muted-strong); }
.member-wallet-error { border-inline-start: 4px solid var(--store-danger); padding: .75rem; background: var(--store-danger-surface); color: var(--store-danger-ink); }
.member-wallet-success { border-inline-start: 4px solid var(--store-success); padding: .75rem; background: var(--store-success-surface); }
</style>
