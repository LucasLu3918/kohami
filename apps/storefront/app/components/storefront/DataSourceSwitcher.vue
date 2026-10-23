<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const { source, preference, backendAvailable } = useDataSource()
const pending = shallowRef(false)
const copy = computed(() => props.locale === 'ja'
  ? { title: 'データモード', mock: backendAvailable ? 'フロントエンド模擬' : '静的フロントエンド展示', backend: 'バックエンドデータ', note: 'ローカル合成データです。会員ページの「デモにワンクリックでログイン」からログインできます。注文・支払いは画面内で模擬し、実際の決済は行いません。', publicNote: '静的な商品展示です。会員ログインと取引は利用できません。', live: '既存APIのデータを表示しています。操作はローカル SQL に保存されます。', unavailable: 'バックエンド未設定', toBackend: 'バックエンドに切り替え', toMock: '模擬データに切り替え' }
  : props.locale === 'en'
    ? { title: 'Data mode', mock: backendAvailable ? 'Interactive frontend demo' : 'Static frontend demo', backend: 'Backend data', note: 'Synthetic local data. Use “One-click demo sign in” on the account page. Orders and payment are simulated in the browser; no real payment is processed.', publicNote: 'Static product showcase. Member sign-in and transactions are unavailable.', live: 'Showing existing API data. Changes are saved to the local SQL database.', unavailable: 'Backend not configured', toBackend: 'Switch to backend', toMock: 'Switch to frontend demo' }
    : { title: '資料模式', mock: backendAvailable ? '前端互動模擬' : '靜態前端展示', backend: '後端資料', note: '本機合成資料，在會員中心按「一鍵測試登入」即可登入。訂單與付款只在瀏覽器模擬，不會實際收款。', publicNote: '靜態商品展示；不提供會員登入與交易。', live: '透過既有 API 讀取資料，變更會寫入本機 SQL，並由後端授權控管。', unavailable: '後端尚未設定', toBackend: '切換至後端資料', toMock: '切換至前端模擬' })

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
    <p class="data-source__note">{{ source === 'mock' ? (backendAvailable ? copy.note : copy.publicNote) : copy.live }}</p>
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
.data-source .data-source__note { color: var(--store-muted-strong); font-size: .75rem; line-height: 1.6; }
.data-source button { min-height: 2.75rem; padding: .55rem .85rem; border: 1px solid var(--store-accent-strong); border-radius: .25rem; background: #fff; color: var(--store-accent-strong); font: inherit; font-size: .8rem; cursor: pointer; }
.data-source button:disabled { border-color: #777; background: var(--store-border-subtle); color: var(--store-muted-strong); cursor: not-allowed; }
.data-source button:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
</style>
