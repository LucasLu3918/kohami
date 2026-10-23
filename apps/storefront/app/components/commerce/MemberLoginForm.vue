<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale; pending: boolean; error: string | null; testAccountAvailable?: boolean }>()
const emit = defineEmits<{ submit: [email: string, password: string] }>()
const email = shallowRef('')
const password = shallowRef('')

const labels = computed(() => props.locale === 'en'
  ? { title: 'Member sign in', email: 'Email', password: 'Password', submit: 'Sign in', testLogin: 'One-click demo sign in', pending: 'Signing in…', invalid: 'Email or password is incorrect.', unavailable: 'Sign in is temporarily unavailable. Try again.' }
  : props.locale === 'ja'
    ? { title: '会員ログイン', email: 'メールアドレス', password: 'パスワード', submit: 'ログイン', testLogin: 'デモにワンクリックでログイン', pending: 'ログイン中…', invalid: 'メールアドレスまたはパスワードが正しくありません。', unavailable: '現在ログインできません。もう一度お試しください。' }
    : { title: '會員登入', email: '電子郵件', password: '密碼', submit: '登入', testLogin: '一鍵測試登入', pending: '登入中…', invalid: '電子郵件或密碼不正確。', unavailable: '目前無法登入，請稍後再試。' })

function signInWithTestAccount() {
  if (props.pending) return
  emit('submit', 'demo@test.com', 'Kohami-Member-Demo-2026!')
}

function submit() {
  if (props.pending || !email.value.trim() || !password.value) return
  emit('submit', email.value.trim(), password.value)
}
</script>

<template>
  <section class="member-sign-in" aria-labelledby="member-sign-in-heading">
    <h2 id="member-sign-in-heading">{{ labels.title }}</h2>
    <form @submit.prevent="submit">
      <label>{{ labels.email }}
        <input v-model="email" type="email" autocomplete="username" maxlength="254" required :disabled="pending" />
      </label>
      <label>{{ labels.password }}
        <input v-model="password" type="password" autocomplete="current-password" required :disabled="pending" />
      </label>
      <div class="member-sign-in__actions">
        <button type="submit" :disabled="pending || !email.trim() || !password">{{ pending ? labels.pending : labels.submit }}</button>
        <button v-if="testAccountAvailable" class="member-sign-in__test" type="button" :disabled="pending" @click="signInWithTestAccount">{{ labels.testLogin }}</button>
      </div>
    </form>
    <p v-if="error" role="alert">{{ error === 'INVALID_CREDENTIALS' ? labels.invalid : labels.unavailable }}</p>
    <p class="synthetic-note">{{ locale === 'en' ? 'For a local member account only.' : locale === 'ja' ? '本機會員帳號適用。' : '僅供本機會員帳號使用。' }}</p>
  </section>
</template>

<style scoped>
.member-sign-in{max-width:34rem;background:#fff;padding:1.25rem;margin:1rem 0;border:1px solid var(--store-border-strong);border-radius:.5rem}.member-sign-in form{display:grid;gap:.9rem}.member-sign-in label{display:grid;gap:.35rem}.member-sign-in input{min-height:2.75rem;box-sizing:border-box;padding:.6rem;font:inherit}.member-sign-in__actions{display:flex;gap:.65rem;flex-wrap:wrap}.member-sign-in button{min-height:2.75rem;padding:.65rem 1rem;background:var(--store-accent-strong);color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.member-sign-in button.member-sign-in__test{background:#fff;color:var(--store-accent-strong);border:1px solid var(--store-accent-strong)}.member-sign-in button:disabled{background:var(--store-muted-strong);color:#fff;opacity:1;cursor:not-allowed}.member-sign-in [role=alert]{color:var(--store-danger)}.synthetic-note{color:var(--store-muted-strong)}.member-sign-in :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
</style>
