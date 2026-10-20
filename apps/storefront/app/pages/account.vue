<script setup lang="ts">
import MemberOrderList from '../components/commerce/MemberOrderList.vue'
import MemberWalletPanel from '../components/commerce/MemberWalletPanel.vue'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const { locale } = useStorefrontLocale()
const { orders, loading, pending, error, claimError, load, claim } = useMemberOrders()
const wallet = useMemberWallet()
const claimOrderId = shallowRef('')
const session = shallowRef<{ state: string }>({ state: 'Unavailable' })

onMounted(async () => {
  try { session.value = await $fetch<{ state: string }>('/api/v1/auth/session') }
  catch { session.value = { state: 'Unavailable' } }
  if (session.value.state === 'MemberAuthenticated') await Promise.all([load(), wallet.load()])
  else wallet.clear()
})

async function logout() {
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch('/api/v1/auth/session', { method: 'DELETE', headers: { 'X-CSRF-TOKEN': csrf.requestToken } })
    session.value = { state: 'Anonymous' }
    orders.value = []
    wallet.clear()
  } catch {
    session.value = { state: 'Unavailable' }
    orders.value = []
    wallet.clear()
  }
}

const labels = computed(() => locale.value === 'en'
  ? { title: 'My account', orders: 'My orders', cart: 'Cart', signIn: 'Sign in from the storefront', claim: 'Claim a guest order', orderId: 'Guest order ID', claimButton: 'Claim order', logout: 'Sign out', loading: 'Loading orders…', auth: 'Please sign in with your KOHAMI account.', notFound: 'Order could not be claimed. Check the order and guest verification in this browser.', failure: 'Orders are temporarily unavailable.' }
  : locale.value === 'ja'
    ? { title: 'マイアカウント', orders: '注文履歴', cart: 'カート', signIn: 'ストアでログイン', claim: 'ゲスト注文を会員に紐付け', orderId: 'ゲスト注文 ID', claimButton: '注文を紐付ける', logout: 'ログアウト', loading: '注文を読み込み中…', auth: 'KOHAMI アカウントでログインしてください。', notFound: '注文を紐付けできません。注文 ID とこのブラウザーでのゲスト確認を確認してください。', failure: '注文を一時的に利用できません。' }
    : { title: '會員中心', orders: '我的訂單', cart: '購物車', signIn: '前往商店登入', claim: '將訪客訂單歸戶至會員', orderId: '訪客訂單編號', claimButton: '歸戶訂單', logout: '登出', loading: '正在載入訂單…', auth: '請先使用 KOHAMI 帳號登入。', notFound: '無法歸戶此訂單，請確認訂單編號及此瀏覽器中的訪客驗證。', failure: '訂單服務暫時無法使用。' })

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
    <section v-if="session.state !== 'MemberAuthenticated'" aria-live="polite">
      <p>{{ labels.auth }}</p><NuxtLink :to="localizedPath(locale, '/')">{{ labels.signIn }}</NuxtLink>
    </section>
    <template v-else>
      <div class="actions"><button class="secondary" @click="logout">{{ labels.logout }}</button><button @click="load">{{ locale === 'en' ? 'Refresh' : locale === 'ja' ? '更新' : '重新整理' }}</button></div>
      <MemberWalletPanel
        :snapshot="wallet.snapshot.value"
        :loading="wallet.loading.value"
        :loading-more="wallet.loadingMore.value"
        :error="wallet.error.value"
        :locale="locale"
        @load-more="wallet.loadMore"
      />
      <section aria-labelledby="orders-heading">
        <h2 id="orders-heading">{{ labels.orders }}</h2>
        <p v-if="loading" role="status">{{ labels.loading }}</p>
        <p v-else-if="error === 'SERVICE_UNAVAILABLE'" role="alert">{{ labels.failure }}</p>
        <MemberOrderList v-else :items="orders" :locale="locale" />
      </section>
      <section aria-labelledby="claim-heading">
        <h2 id="claim-heading">{{ labels.claim }}</h2>
        <form @submit.prevent="claim(claimOrderId)"><label>{{ labels.orderId }}<input v-model="claimOrderId" autocomplete="off" required /></label><button :disabled="pending">{{ labels.claimButton }}</button></form>
        <p v-if="claimError === 'RESOURCE_NOT_FOUND'" role="alert">{{ labels.notFound }}</p>
        <p v-else-if="claimError === 'SERVICE_UNAVAILABLE'" role="alert">{{ labels.failure }}</p>
      </section>
    </template>
  </main>
</template>

<style scoped>
.account{max-width:64rem;margin:auto;padding:1.5rem;color:#25252c}.account section{background:white;padding:1.25rem;margin:1rem 0;border:1px solid #8a817b;border-radius:.5rem}.account nav,.actions{display:flex;gap:1rem;flex-wrap:wrap}.account nav a{display:inline-flex;min-inline-size:24px;min-block-size:24px;align-items:center}.account label{display:block;margin:.75rem 0}.account input{display:block;width:100%;max-width:28rem;box-sizing:border-box;padding:.65rem;font:inherit}.account button{padding:.7rem 1.2rem;background:#6d2942;color:white;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.account button:disabled{background:#625c58;color:#fff;opacity:1;cursor:not-allowed}.account .secondary{background:#4b5360}.account :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
@media (max-width: 15rem) { .account { padding: .5rem; } .account section { padding: .75rem; } .account h1, .account h2 { overflow-wrap: anywhere; } }
</style>
