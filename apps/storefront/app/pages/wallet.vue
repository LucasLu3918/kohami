<script setup lang="ts">
import MemberWalletPanel from '../components/commerce/MemberWalletPanel.vue'
import { onMounted } from 'vue'
import { localizedPath } from '../utils/storefront-locale'

const $fetch = useStorefrontApi()
const { locale } = useStorefrontLocale()
const route = useRoute()
const router = useRouter()
const member = useMemberSession()
const wallet = useMemberWallet()
const topUpPending = ref(false)
const topUpError = ref('')
const topUpOperation = ref<{ fingerprint: string; key: string } | null>(null)

const labels = computed(() => locale.value === 'en'
  ? { title: 'Wallet balance', back: 'Back to my account', refresh: 'Refresh balance', refreshPending: 'Refreshing…', cardInvalid: 'The card number is invalid.', validation: 'Enter a whole amount from 1 to 50,000 and a card number.', topUpFailure: 'Top-up failed. Refresh your balance and try again.' }
  : locale.value === 'ja'
    ? { title: 'ウォレット残高', back: 'マイアカウントに戻る', refresh: '残高を更新', refreshPending: '更新中…', cardInvalid: 'カード番号が正しくありません。', validation: '1〜50,000 の整数金額とカード番号を入力してください。', topUpFailure: 'チャージに失敗しました。残高を更新してからもう一度お試しください。' }
    : { title: '會員儲值餘額', back: '返回會員中心', refresh: '重新整理餘額', refreshPending: '正在更新…', cardInvalid: '卡號不正確。', validation: '請輸入 1 至 50,000 的整數金額及卡號。', topUpFailure: '錢包加值失敗，請重新整理餘額後再試。' })

onMounted(async () => {
  const current = await member.load()
  if (current.state !== 'MemberAuthenticated') {
    const walletPath = localizedPath(locale.value, '/wallet')
    await router.replace(localizedPath(locale.value, `/account?returnTo=${encodeURIComponent(walletPath)}`))
    return
  }
  await wallet.load()
})

async function topUpWallet(amountTwd: number, cardNumber: string) {
  if (topUpPending.value) return
  topUpPending.value = true
  topUpError.value = ''
  try {
    const fingerprint = String(amountTwd)
    let operation = topUpOperation.value
    if (operation?.fingerprint !== fingerprint) {
      operation = { fingerprint, key: `storefront-${crypto.randomUUID()}` }
      topUpOperation.value = operation
    }
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch('/api/v1/wallet/top-ups', {
      method: 'POST',
      body: { amountTwd, cardNumber },
      headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': operation.key },
    })
    await wallet.load()
    if (!wallet.error.value) topUpOperation.value = null
  } catch (cause: any) {
    topUpError.value = cause?.data?.code === 'CARD_INVALID'
      ? labels.value.cardInvalid
      : cause?.data?.code === 'VALIDATION_ERROR'
        ? labels.value.validation
        : labels.value.topUpFailure
  } finally {
    topUpPending.value = false
  }
}

useLocalizedPageHead({ title: computed(() => labels.value.title), path: computed(() => route.path), locale, publicPage: false })
</script>

<template>
  <main id="main-content" class="wallet-page" tabindex="-1">
    <header class="wallet-page-header">
      <div>
        <NuxtLink :to="localizedPath(locale, '/account')">{{ labels.back }}</NuxtLink>
        <h1>{{ labels.title }}</h1>
      </div>
      <button type="button" :disabled="wallet.loading.value" @click="wallet.load">
        {{ wallet.loading.value ? labels.refreshPending : labels.refresh }}
      </button>
    </header>
    <MemberWalletPanel
      :snapshot="wallet.snapshot.value"
      :loading="wallet.loading.value"
      :loading-more="wallet.loadingMore.value"
      :error="wallet.error.value"
      :locale="locale"
      :top-up-pending="topUpPending"
      :top-up-error="topUpError"
      @load-more="wallet.loadMore"
      @top-up="topUpWallet"
    />
  </main>
</template>

<style scoped>
.wallet-page{max-width:64rem;margin:auto;padding:1.5rem;color:var(--store-ink-strong)}.wallet-page-header{display:flex;align-items:flex-end;justify-content:space-between;gap:1rem}.wallet-page-header h1{margin:.5rem 0}.wallet-page-header a{color:var(--store-accent-strong)}.wallet-page-header button{min-height:2.75rem;padding:.6rem 1rem;background:var(--store-surface);color:var(--store-ink-strong);border:1px solid var(--store-border-strong);border-radius:var(--store-radius-sm);font:inherit;cursor:pointer}.wallet-page-header button:disabled{opacity:.7;cursor:wait}.wallet-page :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
@media(max-width:32rem){.wallet-page-header{align-items:flex-start;flex-direction:column}}
</style>
