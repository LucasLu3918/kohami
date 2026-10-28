<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale; pending: boolean; error: string | null }>()
const emit = defineEmits<{ submit: [input: { email: string; password: string; displayName: string; phone: string }] }>()
const form = reactive({ email: '', password: '', displayName: '', phone: '' })

const labels = computed(() => props.locale === 'en'
  ? { title: 'Create your account', email: 'Email', password: 'Password', name: 'Name (optional)', phone: 'Phone (optional)', submit: 'Create account', pending: 'Creating account…', login: 'Email or password is invalid. Please review the fields.', unavailable: 'Registration is temporarily unavailable. Try again.' }
  : props.locale === 'ja'
    ? { title: '会員登録', email: 'メールアドレス', password: 'パスワード', name: 'お名前（任意）', phone: '電話番号（任意）', submit: 'アカウントを作成', pending: '作成中…', login: '入力内容をご確認ください。', unavailable: '現在登録できません。もう一度お試しください。' }
    : { title: '建立會員帳號', email: '電子郵件', password: '密碼', name: '姓名（選填）', phone: '電話（選填）', submit: '建立帳號', pending: '正在建立帳號…', login: '請確認電子郵件與表單內容。', unavailable: '目前無法註冊，請稍後再試。' })

function submit() {
  if (props.pending) return
  emit('submit', {
    email: form.email.trim(),
    password: form.password,
    displayName: form.displayName.trim(),
    phone: form.phone.trim(),
  })
}
</script>

<template>
  <section class="member-registration" aria-labelledby="member-registration-heading">
    <h2 id="member-registration-heading">{{ labels.title }}</h2>
    <form @submit.prevent="submit">
      <label>{{ labels.email }}
        <input v-model="form.email" type="email" autocomplete="email" maxlength="254" required :disabled="pending" />
      </label>
      <label>{{ labels.password }}
        <input v-model="form.password" type="password" autocomplete="new-password" maxlength="256" required :disabled="pending" />
      </label>
      <label>{{ labels.name }}
        <input v-model="form.displayName" type="text" autocomplete="name" maxlength="120" :disabled="pending" />
      </label>
      <label>{{ labels.phone }}
        <input v-model="form.phone" type="tel" autocomplete="tel" maxlength="32" :disabled="pending" />
      </label>
      <button type="submit" :disabled="pending || !form.email.trim() || !form.password">{{ pending ? labels.pending : labels.submit }}</button>
    </form>
    <p v-if="error" role="alert">{{ error === 'VALIDATION_ERROR' || error === 'INVALID_CREDENTIALS' ? labels.login : labels.unavailable }}</p>
    <p class="registration-note">{{ locale === 'en' ? 'You can sign in with your email and password after registration.' : locale === 'ja' ? '登録後はメールアドレスとパスワードでログインできます。' : '註冊完成後即可使用電子郵件與密碼登入。' }}</p>
  </section>
</template>

<style scoped>
.member-registration{max-width:38rem;background:#fff;padding:1.25rem;margin:1rem auto;border:1px solid var(--store-border-strong);border-radius:.5rem}.member-registration form{display:grid;gap:.8rem}.member-registration label{display:grid;gap:.35rem}.member-registration input{min-height:2.75rem;box-sizing:border-box;padding:.6rem;font:inherit}.member-registration button{min-height:2.75rem;padding:.65rem 1rem;background:var(--store-accent-strong);color:#fff;border:0;border-radius:.25rem;font:inherit;cursor:pointer}.member-registration button:disabled{background:var(--store-paper);color:var(--store-muted-strong);box-shadow:inset 0 0 0 1px var(--store-border);opacity:1;cursor:not-allowed}.member-registration [role=alert]{color:var(--store-danger)}.registration-note{color:var(--store-muted-strong)}.member-registration :focus-visible{outline:3px solid var(--store-focus);outline-offset:3px}
</style>
