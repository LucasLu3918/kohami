<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale; pending: boolean; error: string | null }>()
const emit = defineEmits<{ submit: [email: string, password: string] }>()
const email = ref('')
const password = ref('')

const labels = computed(() => props.locale === 'en'
  ? { title: 'Member sign in', email: 'Email', password: 'Password', submit: 'Sign in', pending: 'Signing in…', invalid: 'Email or password is incorrect.', unavailable: 'Sign in is temporarily unavailable. Try again.' }
  : props.locale === 'ja'
    ? { title: '会員ログイン', email: 'メールアドレス', password: 'パスワード', submit: 'ログイン', pending: 'ログイン中…', invalid: 'メールアドレスまたはパスワードが正しくありません。', unavailable: '現在ログインできません。もう一度お試しください。' }
    : { title: '會員登入', email: '電子郵件', password: '密碼', submit: '登入', pending: '登入中…', invalid: '電子郵件或密碼不正確。', unavailable: '目前無法登入，請稍後再試。' })

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
      <button type="submit" :disabled="pending || !email.trim() || !password">{{ pending ? labels.pending : labels.submit }}</button>
    </form>
    <p v-if="error" role="alert">{{ error === 'INVALID_CREDENTIALS' ? labels.invalid : labels.unavailable }}</p>
    <p class="synthetic-note">{{ locale === 'en' ? 'Synthetic demo account only.' : locale === 'ja' ? '合成デモアカウント専用です。' : '僅供合成展示帳號使用。' }}</p>
  </section>
</template>

<style scoped>
.member-sign-in{max-width:34rem;background:#fff;padding:1.25rem;margin:1rem 0;border:1px solid #8a817b;border-radius:.5rem}.member-sign-in form{display:grid;gap:.9rem}.member-sign-in label{display:grid;gap:.35rem}.member-sign-in input{min-height:2.75rem;box-sizing:border-box;padding:.6rem;font:inherit}.member-sign-in button{min-height:2.75rem;padding:.65rem 1rem;background:#6d2942;color:#fff;border:0;border-radius:.25rem;font:inherit}.member-sign-in button:disabled{background:#625c58;opacity:1}.member-sign-in [role=alert]{color:#a11919}.synthetic-note{color:#625c58}.member-sign-in :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
</style>
