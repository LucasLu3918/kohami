<script setup lang="ts">
const route = useRoute()
const locale = computed<'zh-TW' | 'ja' | 'en'>(() => ['zh-TW', 'ja', 'en'].includes(String(route.query.lang)) ? String(route.query.lang) as 'zh-TW' | 'ja' | 'en' : 'zh-TW')
const copy = computed(() => ({ 'zh-TW': { title: 'KOHAMI 本機展示', notice: '全流程模擬，沒有實際付款、退款、配送或有效發票。', login: '登入與身份驗證', email: '測試 Email', password: '密碼', totp: '驗證器代碼', result: '操作結果', status: '目前身份', action: '送出', payment: '查詢原付款狀態', checkout: 'Checkout ID', challenge: 'Challenge ID', nonce: 'Request nonce' }, ja: { title: 'KOHAMI ローカルデモ', notice: '全処理はシミュレーションです。実際の支払・返金・配送・有効な請求書はありません。', login: 'ログインと本人確認', email: 'テストメール', password: 'パスワード', totp: '認証コード', result: '操作結果', status: '現在の認証状態', action: '送信', payment: '元の支払状態を確認', checkout: 'Checkout ID', challenge: 'Challenge ID', nonce: 'Request nonce' }, en: { title: 'KOHAMI local demo', notice: 'All transactions are simulated. No real payments, refunds, deliveries or valid tax invoices.', login: 'Sign in and verification', email: 'Test email', password: 'Password', totp: 'Authenticator code', result: 'Operation result', status: 'Current identity', action: 'Submit', payment: 'Check original payment status', checkout: 'Checkout ID', challenge: 'Challenge ID', nonce: 'Request nonce' } }[locale.value]!))
useHead(() => ({ title: copy.value.title, htmlAttrs: { lang: locale.value } }))
const requestFetch = useRequestFetch()
const { data: session, refresh } = await useAsyncData('current-identity', () => requestFetch('/api/v1/auth/session').catch(() => ({ state: 'Unavailable', mode: 'mock' })))
const catalog = computed(() => ({'zh-TW': ['合成示意商品 A', '合成示意商品 B', '合成示意商品 C'], ja: ['デモ商品 A', 'デモ商品 B', 'デモ商品 C'], en: ['Demo item A', 'Demo item B', 'Demo item C']}[locale.value]))
const email = ref(''), password = ref(''), code = ref(''), challengeId = ref(''), requestNonce = ref(''), checkoutId = ref(''), result = ref<unknown>(null), pending = ref(false)
async function invoke(path: string, body?: Record<string, unknown>, method?: 'DELETE') {
  pending.value = true
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    result.value = await $fetch(`/api/v1${path}`, { method: method || (body ? 'POST' : 'GET'), body, headers: body || method ? { 'X-CSRF-TOKEN': csrf.requestToken } : undefined })
    password.value = ''; code.value = ''; await refresh()
    if (path === '/auth/sessions') { const context = await $fetch<{requestNonce?: string}>('/api/v1/auth/account-context'); requestNonce.value = context.requestNonce || ''; challengeId.value = (session.value as any)?.mfaChallengeId || '' }
    if (method === 'DELETE') { result.value = null; requestNonce.value = ''; challengeId.value = ''; checkoutId.value = '' }
  } catch (error: any) { result.value = error.data || { code: 'SERVICE_UNAVAILABLE' } }
  finally { pending.value = false }
}
function clearPrivateView() { result.value = null; password.value = ''; code.value = ''; void refresh() }
onMounted(() => window.addEventListener('pageshow', clearPrivateView))
onBeforeUnmount(() => window.removeEventListener('pageshow', clearPrivateView))
watch(locale, () => { result.value = null })
</script>
<template>
  <main>
    <header><h1>{{ copy.title }}</h1><p class="notice">{{ copy.notice }}</p><nav aria-label="Language"><NuxtLink v-for="lang in ['zh-TW','ja','en']" :key="lang" :to="{query:{...route.query,lang}}">{{ lang }}</NuxtLink></nav></header>
    <section aria-labelledby="catalog"><h2 id="catalog">{{ locale === 'zh-TW' ? '示意商品' : locale === 'ja' ? 'デモ商品' : 'Demo catalog' }}</h2><ul><li v-for="(name,index) in catalog" :key="index">{{ name }} — TWD {{ [500,900,650][index] }}</li></ul><p>{{ locale === 'zh-TW' ? '合成資料；價格與庫存以結帳結果為準。' : locale === 'ja' ? '合成データ。価格と在庫は決済時に確認されます。' : 'Synthetic data. Checkout confirms price and availability.' }}</p></section>
    <section aria-labelledby="identity"><h2 id="identity">{{ copy.status }}</h2><pre>{{ session }}</pre><button :disabled="pending" @click="invoke('/auth/session',undefined,'DELETE')">{{ locale === 'zh-TW' ? '登出' : locale === 'ja' ? 'ログアウト' : 'Sign out' }}</button></section>
    <section aria-labelledby="login"><h2 id="login">{{ copy.login }}</h2>
      <form @submit.prevent="invoke('/auth/sessions',{email,password})"><label>{{ copy.email }}<input v-model="email" type="email" autocomplete="username" required /></label><label>{{ copy.password }}<input v-model="password" type="password" autocomplete="current-password" required /></label><button :disabled="pending">{{ copy.action }}</button></form>
      <form @submit.prevent="invoke('/auth/mfa-verifications',{challengeId,code,requestNonce})"><label>{{ copy.challenge }}<input v-model="challengeId" required /></label><label>{{ copy.nonce }}<input v-model="requestNonce" required /></label><label>{{ copy.totp }}<input v-model="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" required /></label><button :disabled="pending">{{ copy.action }}</button></form>
    </section>
    <section aria-labelledby="payment"><h2 id="payment">{{ copy.payment }}</h2><form @submit.prevent="invoke('/checkouts/'+encodeURIComponent(checkoutId)+'/status')"><label>{{ copy.checkout }}<input v-model="checkoutId" required /></label><button :disabled="pending">{{ copy.action }}</button></form></section>
    <section aria-labelledby="result"><h2 id="result">{{ copy.result }}</h2><pre role="status" aria-live="polite">{{ result }}</pre></section>
  </main>
</template>
<style>
:root{font-family:system-ui,sans-serif;color:#25252c;background:#faf8f5}main{max-width:52rem;margin:auto;padding:1.5rem}section{background:white;padding:1.25rem;margin:1rem 0;border:1px solid #b0a7a0;border-radius:.5rem}nav{display:flex;gap:1rem}.notice{border-left:4px solid #8b3952;padding:.75rem}label{display:block;margin:.75rem 0}input{display:block;width:100%;max-width:28rem;box-sizing:border-box;padding:.65rem;font:inherit}button{padding:.7rem 1.2rem;background:#6d2942;color:white;border:0;border-radius:.25rem;font:inherit}pre{white-space:pre-wrap;overflow-wrap:anywhere}:focus-visible{outline:3px solid #235b9d;outline-offset:3px}
</style>
