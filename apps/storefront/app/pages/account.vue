<script setup lang="ts">
const $fetch = useStorefrontApi()
import MemberOrderList from '../components/commerce/MemberOrderList.vue'
import MemberLoginForm from '../components/commerce/MemberLoginForm.vue'
import { onActivated, onBeforeUnmount } from 'vue'
import { localizedPath } from '../utils/storefront-locale'
import { formatProductNumber } from '../utils/product-number'

const route = useRoute()
const { locale } = useStorefrontLocale()
const { source } = useDataSource()
const { orders, loading, error, load } = useMemberOrders()
const wallet = useMemberWallet()
const member = useMemberSession()
const router = useRouter()
const catalogBySku = ref<Record<string, { name: string; image: string; alt: string; colorId: string }>>({})

async function loadCatalogForOrders() {
  try {
    const result = await $fetch<{ items: Array<{ skuId: string; colorId: string; translations: Array<{ locale: string; name: string }>; media: Array<{ media: { url: string; alt: { value: string } } }> }> }>('/api/v1/products?limit=100&locale=' + encodeURIComponent(locale.value))
    catalogBySku.value = Object.fromEntries(result.items.map(item => [item.skuId, {
      name: item.translations.find(t => t.locale === locale.value)?.name || item.translations[0]?.name || formatProductNumber(item.skuId),
      image: item.media[0]?.media.url || '',
      alt: item.media[0]?.media.alt.value || formatProductNumber(item.skuId),
      colorId: item.colorId,
    }]))
  } catch {
    catalogBySku.value = {}
  }
}

async function loadAccountData() { await Promise.all([load(), wallet.load()]); await loadCatalogForOrders() }
let lastOrderRefreshAt = 0
async function refreshOrdersOnReturn() {
  if (member.session.value.state !== 'MemberAuthenticated' || document.visibilityState !== 'visible') return
  const now = Date.now()
  if (now - lastOrderRefreshAt < 1500) return
  lastOrderRefreshAt = now
  await load()
}
function handleVisibilityChange() {
  if (document.visibilityState === 'visible') void refreshOrdersOnReturn()
}
onMounted(async () => {
  window.addEventListener('focus', handleVisibilityChange)
  document.addEventListener('visibilitychange', handleVisibilityChange)
  const current = await member.load()
  if (current.state === 'MemberAuthenticated') await loadAccountData()
  else wallet.clear()
})
onActivated(() => { void refreshOrdersOnReturn() })
onBeforeUnmount(() => {
  window.removeEventListener('focus', handleVisibilityChange)
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})

function safeReturnPath() {
  const value = route.query.returnTo
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return null
  if (/[\u0000-\u001f]/.test(value)) return null
  return value
}

async function signIn(email: string, password: string) {
  if (!await member.login(email, password)) return
  await loadAccountData()
  const returnPath = safeReturnPath()
  if (returnPath) await router.replace(returnPath)
}
async function signInDemo() {
  if (!await member.loginDemo()) return
  await loadAccountData()
}

async function logout() {
  await member.logout()
  orders.value = []
  wallet.clear()
}

const labels = computed(() => locale.value === 'en'
  ? { title: 'My account', orders: 'My orders', wallet: 'Wallet balance', available: 'Available balance', walletLoading: 'Loading balance…', walletUnavailable: 'Wallet balance is temporarily unavailable.', walletAuth: 'Please sign in again to view your balance.', walletTopUp: 'Top up balance', signIn: 'Sign in from the storefront', logout: 'Sign out', loading: 'Loading orders…', auth: 'Please sign in with your KOHAMI account.', failure: 'Orders are temporarily unavailable.' }
  : locale.value === 'ja'
    ? { title: 'マイアカウント', orders: '注文履歴', wallet: 'ウォレット残高', available: '利用可能残高', walletLoading: '残高を読み込み中…', walletUnavailable: '残高を一時的に利用できません。', walletAuth: '残高を確認するには再度ログインしてください。', walletTopUp: '残高をチャージ', signIn: 'ストアでログイン', logout: 'ログアウト', loading: '注文を読み込み中…', auth: 'KOHAMI アカウントでログインしてください。', failure: '注文を一時的に利用できません。' }
    : { title: '會員中心', orders: '我的訂單', wallet: '會員儲值餘額', available: '可用餘額', walletLoading: '正在載入餘額…', walletUnavailable: '目前無法讀取錢包餘額。', walletAuth: '請重新登入以查看餘額。', walletTopUp: '前往儲值', signIn: '前往商店登入', logout: '登出', loading: '正在載入訂單…', auth: '請先使用 KOHAMI 帳號登入。', failure: '訂單服務暫時無法使用。' })

function formatWalletAmount(amount: number, currency: string) {
  return `${currency} ${new Intl.NumberFormat(locale.value).format(amount)}`
}

watch(error, (value) => {
  if (value === 'AUTHENTICATION_REQUIRED') {
    orders.value = []
    wallet.clear()
  }
})
useLocalizedPageHead({ title: computed(() => labels.value.title), path: computed(() => route.path), locale, publicPage: false })
</script>

<template>
  <main id="main-content" class="account" tabindex="-1">
    <header>
      <h1>{{ labels.title }}</h1>
    </header>
    <section v-if="member.session.value.state !== 'MemberAuthenticated'" aria-live="polite">
      <p>{{ labels.auth }}</p>
      <MemberLoginForm :locale="locale" :pending="member.pending.value" :error="member.error.value" :test-account-available="source === 'mock'" @submit="signIn" @demo-login="signInDemo" />
      <NuxtLink :to="localizedPath(locale, '/')">{{ labels.signIn }}</NuxtLink>
      <NuxtLink v-if="source === 'backend'" class="register-link" :to="localizedPath(locale, '/register')">{{ locale === 'en' ? 'Create an account' : locale === 'ja' ? '新規登録' : '建立會員帳號' }}</NuxtLink>
    </section>
    <template v-else>
      <div class="actions"><button class="secondary" @click="logout">{{ labels.logout }}</button><button @click="loadAccountData">{{ locale === 'en' ? 'Refresh' : locale === 'ja' ? '更新' : '重新整理' }}</button></div>
      <section class="wallet-summary" aria-labelledby="wallet-heading">
        <div>
          <h2 id="wallet-heading">{{ labels.wallet }}</h2>
          <p v-if="wallet.loading.value" role="status">{{ labels.walletLoading }}</p>
          <p v-else-if="wallet.error.value === 'AUTHENTICATION_REQUIRED'" role="alert">{{ labels.walletAuth }}</p>
          <p v-else-if="wallet.error.value === 'SERVICE_UNAVAILABLE' && !wallet.snapshot.value" role="alert">{{ labels.walletUnavailable }}</p>
          <template v-else-if="wallet.snapshot.value">
            <p class="wallet-amount-label">{{ labels.available }}</p>
            <p class="wallet-amount">{{ formatWalletAmount(wallet.snapshot.value.availableTwd, wallet.snapshot.value.currency) }}</p>
          </template>
        </div>
        <NuxtLink class="wallet-topup-link" :to="localizedPath(locale, '/wallet')">{{ labels.walletTopUp }}</NuxtLink>
      </section>
      <section aria-labelledby="orders-heading">
        <h2 id="orders-heading">{{ labels.orders }}</h2>
        <p v-if="loading && orders.length === 0" role="status">{{ labels.loading }}</p>
        <p v-else-if="error === 'SERVICE_UNAVAILABLE'" role="alert">{{ labels.failure }}</p>
        <MemberOrderList v-else :items="orders" :locale="locale" :catalog-by-sku="catalogBySku" @refresh-orders="load" />
      </section>
    </template>
  </main>
</template>

<style scoped>
.account{max-width:64rem;margin:auto;padding:1.5rem;color:var(--store-ink-strong)}.account section{background:var(--store-surface);padding:1.25rem;margin:1rem 0;border:1px solid var(--store-border-strong);border-radius:var(--store-radius)}.actions{display:flex;gap:1rem;flex-wrap:wrap}.account label{display:block;margin:.75rem 0}.account input{display:block;width:100%;max-width:28rem;box-sizing:border-box;padding:.65rem;font:inherit}.account button{padding:.7rem 1.2rem;background:var(--store-accent-strong);color:white;border:0;border-radius:var(--store-radius-sm);font:inherit;cursor:pointer}.account button:disabled{background:var(--store-muted-strong);color:#fff;opacity:1;cursor:not-allowed}.account .secondary{background:#4b5360}.account :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
.account .register-link{display:inline-block;margin:.5rem 0;color:var(--store-accent-strong)}
.wallet-summary{display:flex;align-items:center;justify-content:space-between;gap:1.5rem}.wallet-summary h2,.wallet-summary p{margin:.2rem 0}.wallet-amount-label{color:var(--store-muted-strong)}.wallet-amount{font-size:1.5rem;font-weight:700;font-variant-numeric:tabular-nums}.wallet-topup-link{display:inline-flex;align-items:center;justify-content:center;min-height:2.75rem;padding:.6rem 1rem;background:var(--store-accent-strong);color:#fff;text-decoration:none;border-radius:var(--store-radius-sm);font-weight:600}.wallet-topup-link:hover{background:var(--store-accent-hover)}
.wallet-topup-link:focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
@media (max-width: 15rem) { .account { padding: .5rem; } .account section { padding: .75rem; } .account h1, .account h2 { overflow-wrap: anywhere; } }
@media (max-width: 32rem) { .wallet-summary{align-items:flex-start;flex-direction:column} }
</style>
