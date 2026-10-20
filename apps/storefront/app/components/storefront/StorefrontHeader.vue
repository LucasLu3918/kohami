<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import LocaleSwitcher from '../LocaleSwitcher.vue'

const props = defineProps<{ locale: StorefrontLocale }>()
const labels = computed(() => props.locale === 'ja'
  ? { home: 'KOHAMI ホーム', arrivals: '新着アイテム', cart: 'カート', account: 'マイページ', menu: 'ショップナビゲーション' }
  : props.locale === 'en'
    ? { home: 'KOHAMI home', arrivals: 'New arrivals', cart: 'Cart', account: 'My account', menu: 'Shop navigation' }
    : { home: 'KOHAMI 首頁', arrivals: '本週新品', cart: '購物車', account: '會員中心', menu: '商店導覽' })
</script>

<template>
  <header class="store-header">
    <div class="store-header__top">
      <NuxtLink class="store-header__brand" :to="localizedPath(locale, '/')" :aria-label="labels.home">KOHAMI</NuxtLink>
      <nav class="store-header__quick-links" :aria-label="labels.menu">
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ labels.arrivals }}</NuxtLink>
        <NuxtLink :to="localizedPath(locale, '/cart')">{{ labels.cart }}</NuxtLink>
        <NuxtLink :to="localizedPath(locale, '/account')">{{ labels.account }}</NuxtLink>
      </nav>
      <LocaleSwitcher :locale="locale" />
    </div>
    <div class="store-header__rule" />
  </header>
</template>

<style scoped>
.store-header { color: #272322; background: #fff; }
.store-header__top { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 1.5rem; max-width: 84rem; margin-inline: auto; padding: 1.15rem clamp(1rem, 4vw, 3.5rem); }
.store-header__brand { justify-self: start; color: inherit; font-family: Georgia, 'Times New Roman', serif; font-size: 1.8rem; font-weight: 700; letter-spacing: .16em; text-decoration: none; }
.store-header__quick-links { display: flex; justify-content: center; gap: clamp(.85rem, 2vw, 2rem); }
.store-header__quick-links a { color: inherit; font-size: .82rem; letter-spacing: .08em; text-decoration: none; }
.store-header__quick-links a:hover, .store-header__quick-links a:focus-visible { color: #975e65; text-decoration: underline; text-underline-offset: .3rem; }
.store-header :deep(.locale-switcher) { justify-self: end; gap: .25rem; }
.store-header :deep(.locale-switcher a) { min-width: 2.5rem; min-height: 2rem; padding: .2rem; color: #625c58; font-size: .7rem; text-decoration: none; }
.store-header :deep(.locale-switcher a[aria-current='page']) { color: #272322; }
.store-header__rule { height: 1px; background: #eee9e4; }
@media (max-width: 48rem) {
  .store-header__top { grid-template-columns: 1fr auto; gap: .7rem; padding: .8rem 1rem; }
  .store-header__brand { font-size: 1.45rem; }
  .store-header__quick-links { grid-column: 1 / -1; grid-row: 2; justify-content: space-between; padding-top: .35rem; }
  .store-header :deep(.locale-switcher) { grid-column: 2; grid-row: 1; }
}
</style>
