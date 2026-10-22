<script setup lang="ts">
definePageMeta({ layout: 'admin' })
const memberUserId = ref('')
const amountTwd = ref(1000)
const reason = ref('')
const pending = ref(false)
const message = ref('')
const error = ref('')
const allowed = ref(false)
const operation = ref<{ fingerprint: string; key: string } | null>(null)

onMounted(async () => {
  try {
    const session = await $fetch<{ state: string; permissions?: string[] }>('/api/v1/auth/session')
    allowed.value = session.state === 'AdminAuthenticated' && session.permissions?.includes('user.manage') === true
  } catch { allowed.value = false }
})

async function creditWallet() {
  if (pending.value || !allowed.value) return
  pending.value = true
  message.value = ''
  error.value = ''
  try {
    const fingerprint = `${memberUserId.value.trim()}|${amountTwd.value}|${reason.value.trim()}`
    if (operation.value?.fingerprint !== fingerprint) operation.value = { fingerprint, key: `storefront-${crypto.randomUUID()}` }
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    const result = await $fetch<{ amountTwd: number; availableTwd: number }>('/api/v1/admin/wallet-credits', {
      method: 'POST', body: { memberUserId: memberUserId.value.trim(), amountTwd: amountTwd.value, reason: reason.value.trim() },
      headers: { 'X-CSRF-TOKEN': csrf.requestToken, 'Idempotency-Key': operation.value.key },
    })
    operation.value = null
    message.value = `已為會員加值 TWD ${result.amountTwd.toLocaleString('zh-TW')}；目前餘額 TWD ${result.availableTwd.toLocaleString('zh-TW')}。`
    reason.value = ''
  } catch (cause: any) {
    error.value = cause?.data?.code === 'FORBIDDEN' || cause?.statusCode === 403
      ? '目前帳號沒有此會員的加值權限。'
      : cause?.data?.code === 'VALIDATION_ERROR' ? '請確認會員 User ID、金額與加值原因。' : '加值失敗，請重新載入餘額並確認操作紀錄。'
  } finally { pending.value = false }
}
</script>

<template>
  <main class="wallet-admin">
    <NuxtLink to="/admin/catalog">← 返回管理後台</NuxtLink>
    <h1>會員儲值金管理</h1>
    <p>餘額與管理員操作會記錄於資料庫；外部儲值服務尚未連接。</p>
    <p v-if="!allowed" role="alert">此帳號沒有 user.manage 權限。</p>
    <form v-else @submit.prevent="creditWallet">
      <label>會員 User ID<input v-model.trim="memberUserId" required maxlength="100" autocomplete="off" /></label>
      <label>加值金額 (TWD)<input v-model.number="amountTwd" type="number" min="1" max="50000" step="1" required /></label>
      <label>加值原因<input v-model.trim="reason" required maxlength="500" /></label>
      <button type="submit" :disabled="pending || !memberUserId || !reason">{{ pending ? '加值中…' : '手動加值' }}</button>
    </form>
    <p v-if="message" role="status">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
  </main>
</template>

<style scoped>
.wallet-admin{max-width:48rem;margin:2rem auto;padding:1rem;color:#25252c}.wallet-admin form{display:grid;gap:1rem;margin-top:1.5rem;padding:1.25rem;border:1px solid #8a817b;border-radius:.5rem;background:#fff}.wallet-admin label{display:grid;gap:.4rem}.wallet-admin input{box-sizing:border-box;width:100%;min-height:2.75rem;padding:.55rem;font:inherit}.wallet-admin button{min-height:2.75rem;padding:.6rem 1rem;background:#6d2942;color:white;border:0;border-radius:.25rem;font:inherit}.wallet-admin button:disabled{background:#625c58}.wallet-admin :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
</style>
