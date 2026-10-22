<script setup lang="ts">
import MemberOrderList from '../components/commerce/MemberOrderList.vue'
import MemberWalletPanel from '../components/commerce/MemberWalletPanel.vue'
import MemberLoginForm from '../components/commerce/MemberLoginForm.vue'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const { locale } = useStorefrontLocale()
const runtimeConfig = useRuntimeConfig()
const { orders, loading, error, load } = useMemberOrders()
const wallet = useMemberWallet()
const member = useMemberSession()
const router = useRouter()
const catalogBySku = ref<Record<string, { name: string; image: string; alt: string }>>({})
const topUpPending = ref(false)
const topUpError = ref('')
const topUpOperation = ref<{ fingerprint: string; key: string } | null>(null)

async function loadCatalogForOrders() {
  try {
    const result = await $fetch<{ items: Array<{ skuId: string; translations: Array<{ locale: string; name: string }>; media: Array<{ media: { url: string; alt: { value: string } } }> }> }>('/api/v1/products?limit=100&locale=' + encodeURIComponent(locale.value))
    catalogBySku.value = Object.fromEntries(result.items.map(item => [item.skuId, {
      name: item.translations.find(t => t.locale === locale.value)?.name || item.translations[0]?.name || item.skuId,
      image: item.media[0]?.media.url || '',
      alt: item.media[0]?.media.alt.value || item.skuId,
    }]))
  } catch {
    catalogBySku.value = {}
  }
}

async function loadAccountData() { await Promise.all([load(), wallet.load()]); await loadCatalogForOrders() }
async function topUpWallet(amountTwd: number, cardNumber: string) {
  if (topUpPending.value) return
  topUpPending.value = true
  topUpError.value = ''
  try {
    const fingerprint = String(amountTwd)
    if (topUpOperation.value?.fingerprint !== fingerprint) topUpOperation.value = { fingerprint, key: `storefront-${crypto.randomUUID()}` }
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch('/api/v1/wallet/top-ups', { method: 'POST', body: { amountTwd, cardNumber }, headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': topUpOperation.value.key } })
    await wallet.load()
    if (!wallet.error.value) topUpOperation.value = null
  } catch (cause: any) {
    topUpError.value = cause?.data?.code === 'CARD_INVALID' ? '測試卡號不正確。' : cause?.data?.code === 'VALIDATION_ERROR' ? '請輸入 1 至 50,000 的整數金額及測試卡號。' : '錢包加值失敗，請重新整理餘額後再試。'
  } finally {
    topUpPending.value = false
  }
}

onMounted(async () => {
  const current = await member.load()
  if (current.state === 'MemberAuthenticated') await loadAccountData()
  else wallet.clear()
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

async function logout() {
  await member.logout()
  orders.value = []
  topUpOperation.value = null
  wallet.clear()
}

const labels = computed(() => locale.value === 'en'
  ? { title: 'My account', orders: 'My orders', cart: 'Cart', signIn: 'Sign in from the storefront', logout: 'Sign out', loading: 'Loading orders…', auth: 'Please sign in with your KOHAMI account.', failure: 'Orders are temporarily unavailable.' }
  : locale.value === 'ja'
    ? { title: 'マイアカウント', orders: '注文履歴', cart: 'カート', signIn: 'ストアでログイン', logout: 'ログアウト', loading: '注文を読み込み中…', auth: 'KOHAMI アカウントでログインしてください。', failure: '注文を一時的に利用できません。' }
    : { title: '會員中心', orders: '我的訂單', cart: '購物車', signIn: '前往商店登入', logout: '登出', loading: '正在載入訂單…', auth: '請先使用 KOHAMI 帳號登入。', failure: '訂單服務暫時無法使用。' })

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
      <nav aria-label="主要導覽"><NuxtLink :to="localizedPath(locale, '/')">KOHAMI</NuxtLink><NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.cart }}</NuxtLink></nav>
    </header>
    <section v-if="member.session.value.state !== 'MemberAuthenticated'" aria-live="polite">
      <p>{{ labels.auth }}</p>
      <MemberLoginForm :locale="locale" :pending="member.pending.value" :error="member.error.value" :test-account-available="runtimeConfig.public.localDemo === true" @submit="signIn" />
      <NuxtLink :to="localizedPath(locale, '/')">{{ labels.signIn }}</NuxtLink>
      <NuxtLink class="register-link" :to="localizedPath(locale, '/register')">{{ locale === 'en' ? 'Create an account' : locale === 'ja' ? '新規登録' : '建立會員帳號' }}</NuxtLink>
    </section>
    <template v-else>
      <div class="actions"><button class="secondary" @click="logout">{{ labels.logout }}</button><button @click="loadAccountData">{{ locale === 'en' ? 'Refresh' : locale === 'ja' ? '更新' : '重新整理' }}</button></div>
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
.account{max-width:64rem;margin:auto;padding:1.5rem;color:#25252c}.account section{background:white;padding:1.25rem;margin:1rem 0;border:1px solid #8a817b;border-radius:.5rem}.account nav,.actions{display:flex;gap:1rem;flex-wrap:wrap}.account nav a{display:inline-flex;min-inline-size:24px;min-block-size:24px;align-items:center}.account label{display:block;margin:.75rem 0}.account input{display:block;width:100%;max-width:28rem;box-sizing:border-box;padding:.65rem;font:inherit}.account button{padding:.7rem 1.2rem;background:#6d2942;color:white;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.account button:disabled{background:#625c58;color:#fff;opacity:1;cursor:not-allowed}.account .secondary{background:#4b5360}.account :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
.account .register-link{display:inline-block;margin:.5rem 0;color:#6d2942}
@media (max-width: 15rem) { .account { padding: .5rem; } .account section { padding: .75rem; } .account h1, .account h2 { overflow-wrap: anywhere; } }
</style>
