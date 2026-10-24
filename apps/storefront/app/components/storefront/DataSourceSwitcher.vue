<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const { source, preference } = useDataSource()
const pending = shallowRef(false)
const copy = computed(() => props.locale === 'ja'
  ? { title: 'データモード', mock: 'フロントエンド模擬', backend: 'API モード', mockNote: '合成データで会員ログインから注文・模擬決済まで操作できます。実際の決済は行いません。', backendNote: '同一オリジンの /api/v1 を呼び出します。接続できない場合は API エラーを表示します。', toBackend: 'API モードに切り替え', toMock: 'mock モードに切り替え' }
  : props.locale === 'en'
    ? { title: 'Data mode', mock: 'Interactive mock', backend: 'API mode', mockNote: 'Use synthetic data for sign-in, ordering, and simulated payment. No real payment is processed.', backendNote: 'Calls the same-origin /api/v1 API. Connection failures appear as API errors.', toBackend: 'Switch to API mode', toMock: 'Switch to mock mode' }
    : { title: '資料模式', mock: '互動式 mock', backend: 'API 模式', mockNote: '使用合成資料完成會員登入、訂購與模擬付款，不會實際收款。', backendNote: '直接呼叫同源 /api/v1；連線失敗時依 API 錯誤流程顯示，不切回 mock。', toBackend: '切換至 API 模式', toMock: '切換至 mock 模式' })

async function switchSource() {
  if (pending.value) return
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
    <p class="data-source__note">{{ source === 'mock' ? copy.mockNote : copy.backendNote }}</p>
    <button type="button" :disabled="pending" @click="switchSource">
      {{ source === 'mock' ? copy.toBackend : copy.toMock }}
    </button>
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
