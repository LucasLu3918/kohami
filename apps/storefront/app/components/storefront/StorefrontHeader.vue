<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import LocaleSwitcher from '../LocaleSwitcher.vue'

const props = defineProps<{ locale: StorefrontLocale; brandOnly?: boolean }>()
const { readOnly } = useDataSource()
const route = useRoute()
const router = useRouter()
const searchTerm = ref(String(route.query.q || ''))
const copy = computed(() => props.locale === 'ja'
  ? { home: 'KOHAMI ホーム', arrivals: '新着アイテム', cart: 'カート', account: 'マイページ', menu: 'ショップナビゲーション', search: '商品を検索', announcement: '商品と取引の状態はシステムに記録されます。' }
  : props.locale === 'en'
    ? { home: 'KOHAMI home', arrivals: 'New arrivals', cart: 'Cart', account: 'My account', menu: 'Shop navigation', search: 'Search products', announcement: 'Product and transaction status is recorded by the system.' }
    : { home: 'KOHAMI 首頁', arrivals: '本週新品', cart: '購物車', account: '會員中心', menu: '商店導覽', search: '搜尋商品', announcement: '商品與交易狀態由系統記錄。' })

watch(() => route.query.q, value => { searchTerm.value = String(value || '') })

function submitSearch() {
  const query = searchTerm.value.trim()
  void router.push({ path: localizedPath(props.locale, '/'), query: query ? { q: query } : {}, hash: '#catalog-heading' })
}
</script>

<template>
  <header class="store-header">
    <div class="store-header__announcement" role="region" :aria-label="locale === 'ja' ? 'お知らせ' : locale === 'en' ? 'Announcement' : '公告'">{{ readOnly ? (locale === 'ja' ? 'フロントエンド展示 · 合成データ · 取引不可' : locale === 'en' ? 'Frontend demo · Synthetic data · Transactions disabled' : '純前端展示 · 合成資料 · 不提供交易') : copy.announcement }}</div>
    <div class="store-header__top" :class="{ 'store-header__top--brand-only': brandOnly }">
      <form v-if="!brandOnly" class="store-header__search" role="search" @submit.prevent="submitSearch">
        <label class="visually-hidden" for="store-header-search">{{ copy.search }}</label>
        <input id="store-header-search" v-model="searchTerm" type="search" name="q" maxlength="100" :placeholder="copy.search" />
        <button type="submit" :aria-label="copy.search">⌕</button>
      </form>
      <NuxtLink class="store-header__brand" :to="localizedPath(locale, '/')" :aria-label="copy.home">KOHAMI</NuxtLink>
      <div v-if="brandOnly" class="store-header__actions store-header__actions--brand-only">
        <NuxtLink class="store-header__back-shop" :to="localizedPath(locale, '/')">{{ locale === 'ja' ? 'ショップへ戻る' : locale === 'en' ? 'Back to shop' : '返回商店' }}</NuxtLink>
      </div>
      <div v-else class="store-header__actions">
        <nav class="store-header__quick-links" :aria-label="copy.menu">
          <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ copy.arrivals }}</NuxtLink>
          <NuxtLink v-if="!readOnly" :to="localizedPath(locale, '/account')">{{ copy.account }}</NuxtLink>
          <NuxtLink v-if="!readOnly" :to="localizedPath(locale, '/cart')">{{ copy.cart }}</NuxtLink>
        </nav>
        <LocaleSwitcher class="store-header__locale" :locale="locale" />
      </div>
    </div>
    <div class="store-header__rule" />
  </header>
</template>

<style scoped>
.store-header { color: #272322; background: #fff; }
.store-header__announcement { min-height: 2rem; display: flex; align-items: center; justify-content: center; padding: .25rem 1rem; background: #6d2942; color: #fff; font-size: .68rem; letter-spacing: .05em; text-align: center; }
.store-header__top { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) minmax(8rem, auto) minmax(0, 1fr); align-items: center; gap: clamp(.8rem, 2vw, 2rem); max-width: 88rem; margin-inline: auto; padding: 1rem clamp(1rem, 3vw, 2.5rem); }
.store-header__brand { grid-column: 2; grid-row: 1; justify-self: center; color: inherit; font-family: Georgia, 'Times New Roman', serif; font-size: 1.8rem; font-weight: 700; letter-spacing: .16em; text-decoration: none; }
.store-header__search { display: flex; min-height: 2.5rem; border-bottom: 1px solid #aaa19c; }
.store-header__search input { width: 100%; min-width: 0; border: 0; background: transparent; color: inherit; font: inherit; font-size: .8rem; outline-offset: 3px; }
.store-header__search button { width: 2.5rem; border: 0; background: transparent; color: #6d2942; font-size: 1.5rem; cursor: pointer; }
.store-header__actions { grid-column: 3; display: flex; align-items: center; justify-content: flex-end; gap: .75rem; min-width: 0; }
.store-header__quick-links { display: flex; justify-content: flex-end; gap: clamp(.7rem, 1.5vw, 1.5rem); }
.store-header__quick-links a { color: inherit; font-size: .76rem; letter-spacing: .03em; text-decoration: none; white-space: nowrap; }
.store-header__quick-links a:hover, .store-header__quick-links a:focus-visible { color: #975e65; text-decoration: underline; text-underline-offset: .3rem; }
.store-header__locale { justify-self: end; }
.store-header__back-shop { color: inherit; font-size: .76rem; text-decoration: none; white-space: nowrap; }
.store-header__back-shop:hover, .store-header__back-shop:focus-visible { color: #975e65; text-decoration: underline; text-underline-offset: .3rem; }
.store-header__top--brand-only .store-header__actions { grid-column: 3; }
.store-header__rule { height: 1px; background: #eee9e4; }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 58rem) {
  .store-header__top { grid-template-columns: minmax(0, 1fr); gap: .65rem; padding: .75rem 1rem; }
  .store-header__brand { grid-column: 1; grid-row: 1; font-size: 1.5rem; }
  .store-header__search { grid-column: 1; grid-row: 2; }
  .store-header__actions { display: contents; }
  .store-header__quick-links { grid-column: 1; grid-row: 3; justify-content: center; gap: clamp(.8rem, 6vw, 2rem); padding-top: .1rem; }
  .store-header__locale { position: absolute; top: .7rem; right: .8rem; }
  .store-header__top--brand-only { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }
  .store-header__top--brand-only .store-header__brand { grid-column: 1 / -1; }
  .store-header__top--brand-only .store-header__actions { display: flex; grid-column: 2; grid-row: 1; justify-content: flex-end; }
}
</style>
