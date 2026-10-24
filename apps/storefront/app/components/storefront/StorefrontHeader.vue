<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import { PhMagnifyingGlass, PhShoppingCart, PhX } from '@phosphor-icons/vue'
import { useCommerceCart } from '../../composables/useCommerceCart'
import LocaleSwitcher from '../LocaleSwitcher.vue'

const props = defineProps<{ locale: StorefrontLocale; brandOnly?: boolean; hideSearch?: boolean }>()
const { source, readOnly } = useDataSource()
const { cart, load: loadCart } = useCommerceCart()
const route = useRoute()
const router = useRouter()
const searchTerm = ref(String(route.query.q || ''))
const cartCount = computed(() => cart.value?.items.reduce((total, item) => total + item.quantity, 0) ?? 0)
const copy = computed(() => props.locale === 'ja'
  ? { home: 'KOHAMI ホーム', brandDescriptor: 'DAILY WARDROBE', arrivals: '新着アイテム', cart: 'カート', cartLabel: (count: number) => `カート、${count}点`, account: 'マイページ', menu: 'ショップナビゲーション', search: '商品を検索', announcement: '商品と取引の状態はシステムに記録されます。', mockAnnouncement: 'ローカル模擬 · 合成データ · 実際の決済なし' }
  : props.locale === 'en'
    ? { home: 'KOHAMI home', brandDescriptor: 'DAILY WARDROBE', arrivals: 'New arrivals', cart: 'Cart', cartLabel: (count: number) => `Cart, ${count} items`, account: 'My account', menu: 'Shop navigation', search: 'Search products', clear: 'Clear search', announcement: 'Product and transaction status is recorded by the system.', mockAnnouncement: 'Local simulation · Synthetic data · No real payment' }
    : { home: 'KOHAMI 首頁', brandDescriptor: '日常衣櫥選品', arrivals: '本週新品', cart: '購物車', cartLabel: (count: number) => `購物車，${count} 件商品`, account: '會員中心', menu: '商店導覽', search: '搜尋商品', clear: '清除搜尋', announcement: '商品與交易狀態由系統記錄。', mockAnnouncement: '本機模擬 · 合成資料 · 不會實際收款' })
const searchCopy = computed(() => props.locale === 'ja' ? '検索をクリア' : copy.value.clear || '検索をクリア')

watch(() => route.query.q, value => { searchTerm.value = String(value || '') })

onMounted(() => {
  if (!props.brandOnly && !readOnly.value) void loadCart()
})

function submitSearch() {
  const query = searchTerm.value.trim()
  void router.push({ path: localizedPath(props.locale, '/'), query: query ? { q: query } : {}, hash: '#catalog-heading' })
}
async function clearSearch() {
  searchTerm.value = ''
  await router.push({ path: localizedPath(props.locale, '/'), query: {}, hash: '#catalog-heading' })
  await nextTick()
  document.getElementById('store-header-search')?.focus()
}
</script>

<template>
  <header class="store-header">
    <div class="store-header__announcement" role="region" :aria-label="locale === 'ja' ? 'お知らせ' : locale === 'en' ? 'Announcement' : '公告'">{{ readOnly ? (locale === 'ja' ? '静的展示 · 合成データ · 取引不可' : locale === 'en' ? 'Static demo · Synthetic data · Transactions disabled' : '靜態展示 · 合成資料 · 不提供交易') : source === 'mock' ? copy.mockAnnouncement : copy.announcement }}</div>
    <div class="store-header__top" :class="{ 'store-header__top--brand-only': brandOnly }">
      <form v-if="!brandOnly && !hideSearch" class="store-header__search" role="search" @submit.prevent="submitSearch">
        <label class="visually-hidden" for="store-header-search">{{ copy.search }}</label>
        <input id="store-header-search" v-model="searchTerm" type="search" name="q" maxlength="100" :placeholder="copy.search" />
        <button v-if="searchTerm" type="button" class="store-header__clear" :aria-label="searchCopy" @click="clearSearch"><PhX :size="18" weight="regular" aria-hidden="true" /></button>
        <button type="submit" :aria-label="copy.search"><PhMagnifyingGlass :size="20" weight="regular" aria-hidden="true" /></button>
      </form>
      <NuxtLink class="store-header__brand" :to="localizedPath(locale, '/')" :aria-label="copy.home"><span>KOHAMI</span><small>{{ copy.brandDescriptor }}</small></NuxtLink>
      <div v-if="brandOnly" class="store-header__actions store-header__actions--brand-only">
        <NuxtLink class="store-header__back-shop" :to="localizedPath(locale, '/')">{{ locale === 'ja' ? 'ショップへ戻る' : locale === 'en' ? 'Back to shop' : '返回商店' }}</NuxtLink>
      </div>
      <div v-else class="store-header__actions">
        <nav class="store-header__quick-links" :aria-label="copy.menu">
          <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ copy.arrivals }}</NuxtLink>
          <NuxtLink v-if="!readOnly" :to="localizedPath(locale, '/account')">{{ copy.account }}</NuxtLink>
          <NuxtLink v-if="!readOnly" class="store-header__cart-link" :to="localizedPath(locale, '/cart')" :aria-label="copy.cartLabel(cartCount)">
            <PhShoppingCart :size="20" weight="regular" aria-hidden="true" />
            <span>{{ copy.cart }}</span>
            <span v-if="cartCount > 0" class="store-header__cart-count" aria-hidden="true">{{ cartCount > 99 ? '99+' : cartCount }}</span>
          </NuxtLink>
        </nav>
        <LocaleSwitcher class="store-header__locale" :locale="locale" />
      </div>
    </div>
    <div class="store-header__rule" />
  </header>
</template>

<style scoped>
.store-header { color: var(--store-ink-strong); background: var(--store-paper); }
.store-header__announcement { min-height: 1.7rem; display: flex; align-items: center; justify-content: center; padding: .2rem 1rem; background: #f4ebee; color: var(--store-accent-strong); font-size: .62rem; letter-spacing: .04em; text-align: center; }
.store-header__top { position: relative; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: clamp(1rem, 3vw, 3rem); max-width: 88rem; margin-inline: auto; padding: .85rem clamp(1rem, 3vw, 2.5rem); }
.store-header__brand { display: flex; align-items: baseline; gap: .65rem; grid-column: 1; grid-row: 1; justify-self: start; color: inherit; text-decoration: none; }
.store-header__brand span { font-family: var(--store-font-display); font-size: clamp(2rem, 3.2vw, 3rem); font-weight: 700; letter-spacing: .14em; line-height: 1.05; }
.store-header__brand small { color: var(--store-muted); font-family: var(--store-font-mono); font-size: .6rem; letter-spacing: .14em; }
.store-header__search { display: flex; width: min(100%, 25rem); min-height: 2.5rem; justify-self: center; border-bottom: 1px solid var(--store-border-strong); }
.store-header__search input { width: 100%; min-width: 0; border: 0; background: transparent; color: inherit; font: inherit; font-size: .8rem; outline-offset: 3px; }
.store-header__search button { width: 2.5rem; min-width: 2.5rem; min-height: 2.5rem; border: 0; background: transparent; color: var(--store-accent-strong); font-size: 1.5rem; cursor: pointer; }
.store-header__search button:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.store-header__search button:hover { color: var(--store-accent-hover); }
.store-header__search .store-header__clear { color: var(--store-muted-strong); }
.store-header__actions { grid-column: 3; display: flex; align-items: center; justify-content: flex-end; gap: clamp(.75rem, 1.5vw, 1.5rem); min-width: 0; }
.store-header__quick-links { display: flex; align-items: center; justify-content: flex-end; gap: clamp(.85rem, 2vw, 2rem); }
.store-header__quick-links a { color: inherit; font-size: .76rem; letter-spacing: .03em; text-decoration: none; white-space: nowrap; }
.store-header__cart-link { position: relative; display: inline-flex; min-width: 2.75rem; min-height: 2.75rem; align-items: center; justify-content: center; gap: .35rem; }
.store-header__cart-count { display: inline-grid; min-width: 1.25rem; height: 1.25rem; place-items: center; padding-inline: .25rem; border-radius: 999px; background: var(--store-accent-strong); color: #fff; font-size: .68rem; font-weight: 700; line-height: 1; }
.store-header__quick-links a:hover, .store-header__quick-links a:focus-visible { color: var(--store-accent-strong); text-decoration: underline; text-underline-offset: .3rem; }
.store-header__quick-links a:focus-visible, .store-header__back-shop:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.store-header__locale { justify-self: end; }
.store-header__back-shop { color: inherit; font-size: .76rem; text-decoration: none; white-space: nowrap; }
.store-header__back-shop:hover, .store-header__back-shop:focus-visible { color: var(--store-accent-strong); text-decoration: underline; text-underline-offset: .3rem; }
.store-header__top--brand-only .store-header__actions { grid-column: 3; }
.store-header__rule { height: 1px; background: var(--store-border-subtle); }
.visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 24rem) {
  .store-header__brand small { display: none; }
  .store-header__brand span { font-size: 1.55rem; }
  .store-header__top { column-gap: .65rem; }
}
@media (max-width: 58rem) {
  .store-header__top { grid-template-columns: minmax(0, 1fr) auto; gap: .45rem .8rem; padding: .7rem 1rem; }
  .store-header__brand { grid-column: 1; grid-row: 1; }
  .store-header__brand span { font-size: 1.7rem; }
  .store-header__brand small { font-size: .52rem; }
  .store-header__search { grid-column: 1 / -1; grid-row: 2; width: 100%; }
  .store-header__actions { display: contents; }
  .store-header__quick-links { grid-column: 1 / -1; grid-row: 3; justify-content: space-between; gap: .65rem; padding-top: .1rem; }
  .store-header__quick-links a { font-size: .72rem; }
  .store-header__cart-link { min-width: 2.5rem; min-height: 2.5rem; gap: .25rem; }
  .store-header__locale { grid-column: 2; grid-row: 1; justify-self: end; }
  .store-header__top--brand-only { grid-template-columns: minmax(0, 1fr) auto; }
  .store-header__top--brand-only .store-header__brand { grid-column: 1; grid-row: 1; }
  .store-header__top--brand-only .store-header__actions { display: flex; grid-column: 2; grid-row: 1; justify-content: flex-end; }
}
</style>
