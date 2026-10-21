<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import LocaleSwitcher from '../LocaleSwitcher.vue'

const props = defineProps<{ locale: StorefrontLocale }>()
const route = useRoute()
const router = useRouter()
const searchTerm = ref(String(route.query.q || ''))
const copy = computed(() => props.locale === 'ja'
  ? { home: 'KOHAMI ホーム', arrivals: '新着アイテム', cart: 'カート', account: 'マイページ', menu: 'ショップナビゲーション', search: '商品を検索', announcement: 'ローカルデモ｜商品と取引データは合成コンテンツです。' }
  : props.locale === 'en'
    ? { home: 'KOHAMI home', arrivals: 'New arrivals', cart: 'Cart', account: 'My account', menu: 'Shop navigation', search: 'Search products', announcement: 'Local demo · Products and transactions use synthetic data.' }
    : { home: 'KOHAMI 首頁', arrivals: '本週新品', cart: '購物車', account: '會員中心', menu: '商店導覽', search: '搜尋商品', announcement: '本機展示｜商品與交易資料皆為合成內容。' })

watch(() => route.query.q, value => { searchTerm.value = String(value || '') })

function submitSearch() {
  const query = searchTerm.value.trim()
  void router.push({ path: localizedPath(props.locale, '/'), query: query ? { q: query } : {}, hash: '#catalog-heading' })
}
</script>

<template>
  <header class="store-header">
    <div class="store-header__announcement" role="region" :aria-label="locale === 'ja' ? 'お知らせ' : locale === 'en' ? 'Announcement' : '公告'">{{ copy.announcement }}</div>
    <div class="store-header__top">
      <NuxtLink class="store-header__brand" :to="localizedPath(locale, '/')" :aria-label="copy.home">KOHAMI</NuxtLink>
      <form class="store-header__search" role="search" @submit.prevent="submitSearch">
        <label class="visually-hidden" for="store-header-search">{{ copy.search }}</label>
        <input id="store-header-search" v-model="searchTerm" type="search" name="q" maxlength="100" :placeholder="copy.search" />
        <button type="submit" :aria-label="copy.search">⌕</button>
      </form>
      <nav class="store-header__quick-links" :aria-label="copy.menu">
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ copy.arrivals }}</NuxtLink>
        <NuxtLink :to="localizedPath(locale, '/account')">{{ copy.account }}</NuxtLink>
        <NuxtLink :to="localizedPath(locale, '/cart')">{{ copy.cart }}</NuxtLink>
      </nav>
      <LocaleSwitcher class="store-header__locale" :locale="locale" />
    </div>
    <div class="store-header__rule" />
  </header>
</template>

<style scoped>
.store-header { color: #272322; background: #fff; }
.store-header__announcement { min-height: 2rem; display: flex; align-items: center; justify-content: center; padding: .25rem 1rem; background: #6d2942; color: #fff; font-size: .68rem; letter-spacing: .05em; text-align: center; }
.store-header__top { display: grid; grid-template-columns: minmax(8rem, 1fr) minmax(14rem, 24rem) minmax(12rem, 1fr) auto; align-items: center; gap: clamp(.8rem, 2vw, 2rem); max-width: 88rem; margin-inline: auto; padding: 1rem clamp(1rem, 3vw, 2.5rem); }
.store-header__brand { justify-self: center; color: inherit; font-family: Georgia, 'Times New Roman', serif; font-size: 1.8rem; font-weight: 700; letter-spacing: .16em; text-decoration: none; }
.store-header__search { display: flex; min-height: 2.5rem; border-bottom: 1px solid #aaa19c; }
.store-header__search input { width: 100%; min-width: 0; border: 0; background: transparent; color: inherit; font: inherit; font-size: .8rem; outline-offset: 3px; }
.store-header__search button { width: 2.5rem; border: 0; background: transparent; color: #6d2942; font-size: 1.5rem; cursor: pointer; }
.store-header__quick-links { display: flex; justify-content: flex-end; gap: clamp(.7rem, 1.5vw, 1.5rem); }
.store-header__quick-links a { color: inherit; font-size: .76rem; letter-spacing: .03em; text-decoration: none; white-space: nowrap; }
.store-header__quick-links a:hover, .store-header__quick-links a:focus-visible { color: #975e65; text-decoration: underline; text-underline-offset: .3rem; }
.store-header :deep(.locale-switcher) { justify-self: end; gap: .15rem; }
.store-header :deep(.locale-switcher a) { min-width: 2rem; min-height: 2rem; padding: .1rem; color: #625c58; font-size: .64rem; text-decoration: none; }
.store-header :deep(.locale-switcher a[aria-current='page']) { color: #272322; }
.store-header__rule { height: 1px; background: #eee9e4; }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 850px) {
  .store-header__top { grid-template-columns: 1fr auto; gap: .65rem 1rem; padding: .75rem 1rem; }
  .store-header__brand { justify-self: start; font-size: 1.5rem; }
  .store-header__search { grid-column: 1 / -1; grid-row: 2; }
  .store-header__quick-links { grid-column: 1 / -1; grid-row: 3; justify-content: space-between; padding-top: .1rem; }
  .store-header__locale { grid-column: 2; grid-row: 1; }
}
</style>
