<script setup lang="ts">
const route = useRoute()
const router = useRouter()
const { session, logout } = useAdminAuth()
const pending = ref(false)
const error = ref('')

async function signOut() {
  if (pending.value) return
  pending.value = true
  error.value = ''
  try {
    await logout()
    await router.replace('/admin/login')
  } catch {
    error.value = '目前無法登出，請重試。'
  } finally {
    pending.value = false
  }
}
</script>

<template>
  <div class="admin-frame">
    <header class="admin-topbar">
      <NuxtLink class="brand" to="/admin/catalog" aria-label="Kohami 管理後台首頁">
        <span class="brand-mark">K</span>
        <span><strong>KOHAMI</strong><small>STUDIO</small></span>
      </NuxtLink>
      <div class="admin-topbar__right">
        <span class="session-label"><i aria-hidden="true" />管理者工作階段</span>
        <button type="button" class="logout-button" :disabled="pending" @click="signOut">{{ pending ? '登出中…' : '登出' }}</button>
      </div>
    </header>
    <div class="admin-body">
      <aside class="admin-rail" aria-label="管理後台導覽">
        <p class="rail-caption">CATALOG</p>
        <NuxtLink class="rail-link" :aria-current="route.path.startsWith('/admin/catalog') ? 'page' : undefined" to="/admin/catalog">
          <span aria-hidden="true">◇</span> 商品目錄
        </NuxtLink>
        <div class="rail-note">
          <span class="rail-note__tag">READ ONLY</span>
          <p>此帳號僅能查看已上架商品與商品編號資訊。</p>
        </div>
        <div class="rail-account">
          <span class="avatar" aria-hidden="true">店</span>
          <span><strong>商品管理者</strong><small>{{ session.userId }}</small></span>
        </div>
      </aside>
      <main id="main-content" class="admin-content" tabindex="-1">
        <p v-if="error" class="admin-error" role="alert">{{ error }}</p>
        <slot />
        <footer class="admin-footer">KOHAMI <span>商品管理</span></footer>
      </main>
    </div>
  </div>
</template>

<style>
.admin-frame { min-height: 100vh; color: #38312e; background: #f8f7f5; font-family: var(--store-font-body); }
.admin-topbar { height: 76px; display: flex; align-items: center; justify-content: space-between; padding: 0 clamp(1.25rem, 4vw, 4rem); background: #fff; border-bottom: 1px solid #ece8e4; }
.brand { display: inline-flex; align-items: center; gap: .75rem; color: inherit; text-decoration: none; }
.brand-mark { width: 36px; height: 36px; display: grid; place-items: center; color: #fff; background: #905e64; border-radius: 50%; font-family: Georgia, serif; font-size: 1.1rem; }
.brand strong, .brand small { display: block; letter-spacing: .15em; }
.brand strong { font-family: Georgia, serif; font-size: .85rem; }
.brand small { margin-top: .25rem; color: #938984; font-size: .56rem; }
.admin-topbar__right { display: flex; align-items: center; gap: 1.5rem; }
.session-label { color: #766d68; font-size: .78rem; }
.session-label i { display: inline-block; width: 7px; height: 7px; margin-right: .5rem; border-radius: 50%; background: #6c927c; }
.logout-button { padding: .55rem 1rem; color: #625955; background: #fff; border: 1px solid #ded8d3; border-radius: 2px; font: inherit; font-size: .78rem; cursor: pointer; }
.logout-button:hover { border-color: #905e64; color: #905e64; }
.admin-body { display: grid; grid-template-columns: 232px minmax(0, 1fr); min-height: calc(100vh - 76px); }
.admin-rail { position: relative; display: flex; flex-direction: column; padding: 2.3rem 1.25rem 1.2rem; background: #f1eeea; border-right: 1px solid #e8e2dd; }
.rail-caption { margin: 0 0 .8rem .75rem; color: #9b918c; font-size: .62rem; letter-spacing: .18em; }
.rail-link { display: flex; align-items: center; gap: .7rem; padding: .8rem .75rem; color: #574e49; text-decoration: none; font-size: .87rem; border-radius: 2px; }
.rail-link[aria-current="page"] { color: #8e5c63; background: #fff; box-shadow: 0 1px 3px #31231a0a; }
.rail-link span { font-size: 1.2rem; }
.rail-note { margin: 2.2rem .25rem; padding: 1rem; background: #faf8f6; border: 1px solid #e8e1dc; }
.rail-note__tag { color: #926269; font-size: .59rem; letter-spacing: .14em; }
.rail-note p { margin: .55rem 0 0; color: #817771; font-size: .73rem; line-height: 1.7; }
.rail-account { display: flex; align-items: center; gap: .7rem; margin-top: auto; padding: 1rem .25rem 0; border-top: 1px solid #ded8d2; }
.avatar { width: 32px; height: 32px; display: grid; place-items: center; color: #895a61; background: #e7d8d7; border-radius: 50%; font-size: .8rem; }
.rail-account strong, .rail-account small { display: block; max-width: 145px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rail-account strong { font-size: .75rem; font-weight: 500; }
.rail-account small { margin-top: .2rem; color: #8c827d; font-size: .65rem; }
.admin-content { min-width: 0; padding: clamp(1.5rem, 4vw, 3.5rem) clamp(1.25rem, 5vw, 4.5rem) 1.5rem; }
.admin-footer { display: flex; justify-content: space-between; margin-top: 3rem; padding-top: 1rem; color: #a09792; border-top: 1px solid #e9e5e1; font-size: .62rem; letter-spacing: .1em; }
.admin-footer span { letter-spacing: 0; }
.admin-error { color: #9b4141; }
@media (max-width: 720px) {
  .admin-topbar { height: 66px; padding-inline: 1rem; }
  .admin-topbar__right { gap: .7rem; }
  .session-label { display: none; }
  .admin-body { grid-template-columns: 1fr; min-height: calc(100vh - 66px); }
  .admin-rail { padding: .7rem 1rem; border-right: 0; border-bottom: 1px solid #e8e2dd; }
  .rail-caption, .rail-note, .rail-account { display: none; }
  .rail-link { padding: .65rem .75rem; }
  .admin-content { padding: 1.3rem 1rem; }
  .admin-footer { gap: .75rem; font-size: .56rem; }
}
</style>
