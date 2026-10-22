<script setup lang="ts">
const $fetch = useStorefrontApi()
import MemberRegistrationForm from '../components/commerce/MemberRegistrationForm.vue'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const router = useRouter()
const { locale } = useStorefrontLocale()
const member = useMemberSession()
const pending = shallowRef(false)
const error = shallowRef<string | null>(null)

function safeReturnPath() {
  const value = route.query.returnTo
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return null
  if (/[\u0000-\u001f]/.test(value)) return null
  return value
}

async function register(input: { email: string; password: string; displayName: string; phone: string }) {
  if (pending.value) return
  pending.value = true
  error.value = null
  try {
    const csrf = await $fetch<{ requestToken: string }>('/api/v1/auth/csrf')
    await $fetch('/api/v1/auth/registrations', {
      method: 'POST',
      body: input,
      headers: { 'X-CSRF-TOKEN': csrf.requestToken },
    })
    if (!await member.login(input.email, input.password)) {
      error.value = member.error.value === 'INVALID_CREDENTIALS' ? 'INVALID_CREDENTIALS' : 'SERVICE_UNAVAILABLE'
      return
    }
    await router.replace(safeReturnPath() ?? localizedPath(locale.value, '/account'))
  } catch (cause: any) {
    error.value = cause?.data?.code === 'VALIDATION_ERROR' ? 'VALIDATION_ERROR' : 'SERVICE_UNAVAILABLE'
  } finally {
    pending.value = false
  }
}

useLocalizedPageHead({ title: computed(() => locale.value === 'en' ? 'Create your account' : locale.value === 'ja' ? '会員登録' : '建立會員帳號'), path: computed(() => route.path), locale, publicPage: true })
</script>

<template>
  <main id="main-content" class="registration-page" tabindex="-1">
    <header class="registration-header">
      <NuxtLink :to="localizedPath(locale, '/')">KOHAMI</NuxtLink>
      <NuxtLink :to="localizedPath(locale, '/account')">{{ locale === 'en' ? 'Sign in' : locale === 'ja' ? 'ログイン' : '會員登入' }}</NuxtLink>
    </header>
    <MemberRegistrationForm :locale="locale" :pending="pending" :error="error" @submit="register" />
  </main>
</template>

<style scoped>
.registration-page{max-width:64rem;margin:auto;padding:1.5rem;color:#25252c}.registration-header{display:flex;justify-content:space-between;gap:1rem;align-items:center}.registration-header a{color:#6d2942}.registration-page :focus-visible{outline:3px solid #235b9d;outline-offset:3px}
</style>
