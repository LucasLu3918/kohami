<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const { source, preference, backendAvailable } = useDataSource()
const pending = shallowRef(false)
const copy = computed(() => props.locale === 'ja'
  ? { title: 'データモード', mock: 'フロントエンド展示', backend: 'バックエンドデータ', note: '合成サンプルです。ログイン・取引は利用できません。', live: '既存APIのデータを表示しています。', unavailable: 'バックエンド未設定', toBackend: 'バックエンドに切り替え', toMock: '展示に切り替え' }
  : props.locale === 'en'
    ? { title: 'Data mode', mock: 'Frontend demo', backend: 'Backend data', note: 'Synthetic samples. Sign-in and transactions are disabled.', live: 'Showing data from the existing API.', unavailable: 'Backend not configured', toBackend: 'Switch to backend', toMock: 'Switch to frontend demo' }
    : { title: '資料模式', mock: '純前端展示', backend: '後端資料', note: '使用合成展示資料，不提供登入與交易。', live: '透過既有 API 讀取資料，存取仍受後端授權控管。', unavailable: '後端尚未設定', toBackend: '切換至後端資料', toMock: '切換至純前端展示' })

async function switchSource() {
  if (pending.value || !backendAvailable) return
  pending.value = true
  preference.value = source.value === 'mock' ? 'backend' : 'mock'
  await nextTick()
  reloadNuxtApp({ path: localizedPath(props.locale, '/'), force: true })
}
</script>

<template>
  <section class="data-source" :aria-label="copy.title">
    <h3>{{ copy.title }}</h3>
    <p role="status">{{ source === 'mock' ? copy.mock : copy.backend }}</p>
    <p class="data-source__note">{{ source === 'mock' ? copy.note : copy.live }}</p>
    <button type="button" :disabled="pending || !backendAvailable" :aria-describedby="!backendAvailable ? 'backend-unavailable' : undefined" @click="switchSource">
      {{ source === 'mock' ? copy.toBackend : copy.toMock }}
    </button>
    <p v-if="!backendAvailable" id="backend-unavailable" class="data-source__note">{{ copy.unavailable }}</p>
  </section>
</template>

<style scoped>
.data-source { max-width: 22rem; margin-top: 1.2rem; padding-top: 1rem; border-top: 1px solid #ded7d0; }
.data-source h3 { margin: 0; font-size: .8rem; font-weight: 600; }
.data-source p { margin: .45rem 0; font-size: .82rem; }
.data-source .data-source__note { color: #625c58; font-size: .75rem; line-height: 1.6; }
.data-source button { min-height: 2.75rem; padding: .55rem .85rem; border: 1px solid #6d2942; border-radius: .25rem; background: #fff; color: #6d2942; font: inherit; font-size: .8rem; cursor: pointer; }
.data-source button:disabled { border-color: #777; background: #eee9e4; color: #625c58; cursor: not-allowed; }
.data-source button:focus-visible { outline: 3px solid #235b9d; outline-offset: 3px; }
</style>
