<script setup lang="ts">
const route = useRoute()
const router = useRouter()
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
  <main id="main-content" class="login-page">
    <NuxtLink class="back-store" to="/">← 返回商店</NuxtLink>
    <div class="login-scene">
      <section class="login-image" aria-label="Kohami 日系服飾展示">
        <img src="/demo-assets/products/demo-wrap-midi-dress.png" alt="日系女裝商品示意">
        <div class="image-caption"><span>CURATED DAILY WEAR</span><p>日常裡，留一點柔軟給自己。</p></div>
      </section>
      <section class="login-panel" aria-labelledby="login-title">
        <div class="login-brand"><span class="brand-mark">K</span><span>KOHAMI</span></div>
        <p class="eyebrow">LOCAL DEMO · ADMINISTRATOR</p>
        <h1 id="login-title">管理者登入</h1>
        <p class="login-intro">歡迎回到商品管理空間。請使用本機展示管理者帳號登入。</p>
        <form class="login-form" :data-admin-hydrated="hydrated" @submit.prevent="submit">
          <label for="admin-email">電子郵件</label>
          <input id="admin-email" v-model="email" type="email" name="email" autocomplete="username" maxlength="254" required>
          <label for="admin-password">密碼</label>
          <input id="admin-password" v-model="password" type="password" name="password" autocomplete="current-password" maxlength="256" required>
          <p v-if="error" class="login-error" role="alert">{{ error }}</p>
          <button type="submit" :disabled="pending || !email.trim() || !password">{{ pending ? '正在登入…' : '登入管理後台' }}<span aria-hidden="true">→</span></button>
        </form>
        <p class="security-note"><span aria-hidden="true">◇</span> 僅限已授權的本機商品管理者使用</p>
        <NuxtLink class="storefront-link" to="/">前往 KOHAMI 商店</NuxtLink>
      </section>
    </div>
    <footer class="login-footer">© KOHAMI · LOCAL DEMO <span>日常衣著，細節相伴</span></footer>
  </main>
</template>

<style scoped>
.login-page { min-height: 100vh; padding: 1.4rem clamp(1rem, 4vw, 3.5rem) 1rem; color: #39312e; background: #f8f6f3; }
.back-store { color: #80756f; text-decoration: none; font-size: .72rem; }
.back-store:hover, .storefront-link:hover { color: #8f5f65; }
.login-scene { width: min(100%, 1020px); min-height: min(690px, calc(100vh - 130px)); display: grid; grid-template-columns: 1.02fr .98fr; margin: 1rem auto 1.5rem; background: #fff; box-shadow: 0 18px 60px #463b3210; }
.login-image { position: relative; min-height: 570px; overflow: hidden; background: #e9e2dc; }
.login-image img { width: 100%; height: 100%; position: absolute; inset: 0; object-fit: cover; }
.login-image::after { position: absolute; inset: 35% 0 0; content: ''; background: linear-gradient(transparent, #201915a8); }
.image-caption { position: absolute; z-index: 1; bottom: 2.5rem; left: 2.5rem; color: #fff; }
.image-caption span { font-size: .6rem; letter-spacing: .2em; }
.image-caption p { margin: .6rem 0 0; font-family: Georgia, 'Noto Serif TC', serif; font-size: 1.25rem; letter-spacing: .08em; }
.login-panel { display: flex; flex-direction: column; justify-content: center; padding: clamp(2rem, 6vw, 4.5rem); }
.login-brand { display: flex; align-items: center; gap: .7rem; margin-bottom: 3.5rem; color: #514441; font-family: Georgia, serif; font-size: .82rem; letter-spacing: .18em; }
.brand-mark { width: 35px; height: 35px; display: grid; place-items: center; color: white; background: #905e64; border-radius: 50%; font-size: 1rem; letter-spacing: 0; }
.eyebrow { margin: 0; color: #a47e7b; font-size: .58rem; letter-spacing: .18em; }
h1 { margin: .7rem 0 .65rem; color: #423a36; font-family: Georgia, 'Noto Serif TC', serif; font-size: 1.85rem; font-weight: 400; letter-spacing: .08em; }
.login-intro { margin: 0; color: #8a807a; font-size: .78rem; line-height: 1.8; }
.login-form { display: grid; gap: .65rem; margin-top: 2.2rem; }
.login-form label { margin-top: .55rem; color: #675d58; font-size: .72rem; }
.login-form input { width: 100%; height: 44px; box-sizing: border-box; padding: 0 .8rem; color: #493f3b; background: #fff; border: 1px solid #e5dfda; border-radius: 0; font: inherit; font-size: .8rem; }
.login-form input:focus { border-color: #9a6d72; outline: 2px solid #9a6d7228; outline-offset: 1px; }
.login-form button { display: flex; align-items: center; justify-content: space-between; height: 48px; margin-top: .7rem; padding: 0 1rem; color: #fff; background: #86595f; border: 1px solid #86595f; font: inherit; font-size: .76rem; letter-spacing: .06em; cursor: pointer; }
.login-form button:hover:not(:disabled) { background: #714a50; }
.login-form button:disabled { opacity: .55; cursor: not-allowed; }
.login-error { margin: .3rem 0 0; color: #a44747; font-size: .72rem; }
.security-note { margin: 1.2rem 0 0; color: #928883; font-size: .65rem; }
.security-note span { margin-right: .25rem; color: #9e7778; }
.storefront-link { align-self: flex-start; margin-top: 1.8rem; color: #7f716a; text-decoration: none; font-size: .7rem; }
.login-footer { display: flex; justify-content: space-between; width: min(100%, 1020px); margin: auto; color: #a09690; font-size: .58rem; letter-spacing: .12em; }
.login-footer span { letter-spacing: .04em; }
@media (max-width: 760px) { .login-page { padding: 1rem; } .login-scene { grid-template-columns: 1fr; margin-top: 1rem; } .login-image { min-height: 220px; max-height: 260px; } .image-caption { bottom: 1.2rem; left: 1.3rem; } .image-caption p { font-size: 1rem; } .login-panel { padding: 1.7rem 1.4rem 2rem; } .login-brand { margin-bottom: 2rem; } .login-footer { gap: .7rem; font-size: .52rem; } }
</style>
