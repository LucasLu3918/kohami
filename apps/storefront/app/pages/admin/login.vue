<script setup lang="ts">
import StorefrontHeader from '../../components/storefront/StorefrontHeader.vue'
const route = useRoute()
const router = useRouter()
const { source } = useDataSource()
const { login, refresh } = useAdminAuth()
const email = ref('')
const password = ref('')
const pending = ref(false)
const error = ref('')
const hydrated = ref(false)

function safeDestination(value: unknown) {
  if (typeof value !== 'string' || !value.startsWith('/admin/') || value.startsWith('//') || value === '/admin/login') return '/admin/catalog'
  return value
}

onMounted(async () => {
  const session = await refresh()
  hydrated.value = true
  if (session.state === 'AdminAuthenticated' && session.permissions.includes('catalog.read')) {
    await router.replace(safeDestination(route.query.next))
  }
})

async function submit() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    const session = await login(email.value.trim(), password.value)
    if (session.state !== 'AdminAuthenticated' || !session.permissions.includes('catalog.read')) {
      await refresh()
      error.value = '此帳號沒有商品目錄讀取權限。'
      return
    }
    password.value = ''
    await router.replace(safeDestination(route.query.next))
  } catch (cause: any) {
    error.value = cause?.statusCode === 401 || cause?.data?.code === 'AUTHENTICATION_REQUIRED'
      ? '帳號或密碼不正確，請再試一次。'
      : '目前無法登入，請稍後重試。'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <div class="admin-login-shell">
    <StorefrontHeader locale="zh-TW" :brand-only="true" />
    <main id="main-content" class="login-page">
      <section class="login-panel" aria-labelledby="login-title">
        <p class="eyebrow">ADMINISTRATOR</p>
        <h1 id="login-title">管理者登入</h1>
        <p class="login-intro">歡迎回到商品管理空間。請使用管理者帳號登入。</p>
        <form class="login-form" :data-admin-hydrated="hydrated" @submit.prevent="submit">
          <label for="admin-email">電子郵件</label>
          <input id="admin-email" v-model="email" type="email" name="email" autocomplete="username" maxlength="254" required>
          <label for="admin-password">密碼</label>
          <input id="admin-password" v-model="password" type="password" name="password" autocomplete="current-password" maxlength="256" required>
          <p v-if="error" class="login-error" role="alert">{{ error }}</p>
          <button type="submit" :disabled="pending || !email.trim() || !password">{{ pending ? '正在登入…' : '登入管理後台' }}<span aria-hidden="true">→</span></button>
        </form>
        <p class="security-note"><span aria-hidden="true">◇</span> {{ source === 'mock' ? '本機展示管理者帳號：demo-admin@test.com／Kohami-Admin-Demo-2026!；僅能操作合成資料。' : '僅限已授權的本機商品管理者使用' }}</p>
      </section>
      <footer class="login-footer">© KOHAMI <NuxtLink to="/">返回商店</NuxtLink></footer>
    </main>
  </div>
</template>

<style scoped>
.admin-login-shell { min-height: 100vh; color: #39312e; background: #fff; }
.login-page { display: flex; min-height: calc(100vh - 9rem); flex-direction: column; justify-content: center; padding: 2.5rem clamp(1rem, 4vw, 3.5rem) 1rem; }
.login-panel { width: min(100%, 430px); box-sizing: border-box; margin: 0 auto; padding: clamp(1.5rem, 4vw, 2.8rem); background: #fff; }
.eyebrow { margin: 0; color: #8b6065; font-size: .62rem; letter-spacing: .16em; }
h1 { margin: .8rem 0 .65rem; color: #423a36; font-family: Georgia, 'Noto Serif TC', serif; font-size: 1.85rem; font-weight: 400; letter-spacing: .08em; }
.login-intro { margin: 0; color: var(--store-muted); font-size: .82rem; line-height: 1.8; }
.login-form { display: grid; gap: .65rem; margin-top: 1.7rem; }
.login-form label { margin-top: .45rem; color: #514844; font-size: .76rem; }
.login-form input { width: 100%; height: 46px; box-sizing: border-box; padding: 0 .8rem; color: #493f3b; background: #fff; border: 1px solid #cfc8c2; border-radius: 0; font: inherit; font-size: .82rem; }
.login-form input:focus { border-color: #9a6d72; outline: 2px solid #9a6d7228; outline-offset: 1px; }
.login-form button { display: flex; align-items: center; justify-content: space-between; min-height: 48px; margin-top: .7rem; padding: 0 1rem; color: #fff; background: var(--store-accent-strong); border: 1px solid var(--store-accent-strong); font: inherit; font-size: .78rem; letter-spacing: .06em; cursor: pointer; }
.login-form button:hover:not(:disabled) { background: var(--store-accent-hover); }
.login-form button:disabled { opacity: .55; cursor: not-allowed; }
.login-error { margin: .3rem 0 0; color: #a44747; font-size: .72rem; }
.security-note { margin: 1.2rem 0 0; color: #625a56; font-size: .72rem; line-height: 1.7; }
.security-note span { margin-right: .25rem; color: #7c4d55; }
.login-footer { display: flex; justify-content: space-between; width: min(100%, 430px); margin: 2rem auto 0; color: var(--store-muted); font-size: .64rem; letter-spacing: .08em; }
.login-footer a { color: var(--store-accent-strong); text-decoration: none; letter-spacing: 0; }
.login-footer a:hover { text-decoration: underline; text-underline-offset: .2em; }
@media (max-width: 600px) { .login-page { min-height: calc(100vh - 11rem); padding: 2rem 1rem 1rem; } .login-panel { padding: 1.25rem .5rem; } .login-footer { gap: .7rem; font-size: .58rem; } }
</style>
