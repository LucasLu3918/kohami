<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'
import DataSourceSwitcher from './DataSourceSwitcher.vue'

const props = defineProps<{ locale: StorefrontLocale }>()
const labels = computed(() => props.locale === 'ja'
  ? { tagline: '毎日に、小さなときめきを。', policies: 'ご利用ガイド', support: 'お問い合わせ', demo: 'KOHAMI · 商品情報と取引状況' }
  : props.locale === 'en'
    ? { tagline: 'A little delight for every day.', policies: 'Shopping guide', support: 'Support', demo: 'KOHAMI · Product information and transaction status' }
    : { tagline: '讓日常，多一點心動。', policies: '購物須知', support: '客服支援', demo: 'KOHAMI · 商品資訊與交易狀態' })
</script>

<template>
  <footer class="store-footer">
    <div class="store-footer__inner">
      <div>
        <NuxtLink class="store-footer__brand" :to="localizedPath(locale, '/')">KOHAMI</NuxtLink>
        <p>{{ labels.tagline }}</p>
      </div>
      <div class="store-footer__resources">
        <h2>{{ locale === 'ja' ? 'サポート情報' : locale === 'en' ? 'Customer resources' : '客服資源' }}</h2>
        <nav class="store-footer__links" :aria-label="locale === 'ja' ? 'フッターナビゲーション' : locale === 'en' ? 'Footer navigation' : '頁尾導覽'">
          <NuxtLink :to="localizedPath(locale, '/policies')">{{ labels.policies }}</NuxtLink>
          <NuxtLink :to="localizedPath(locale, '/support')">{{ labels.support }}</NuxtLink>
        </nav>
        <DataSourceSwitcher :locale="locale" />
      </div>
      <small>{{ labels.demo }}</small>
    </div>
  </footer>
</template>

<style scoped>
.store-footer { margin-top: 4rem; border-top: 1px solid #eee9e4; background: #f8f6f3; color: #514b48; }
.store-footer__inner { display: grid; grid-template-columns: 1fr auto; gap: 1rem 2rem; max-width: 84rem; margin-inline: auto; padding: 2.5rem clamp(1rem, 4vw, 3.5rem); }
.store-footer__brand { color: #272322; font-family: Georgia, 'Times New Roman', serif; font-weight: 700; letter-spacing: .15em; text-decoration: none; }
.store-footer p { margin: .45rem 0 0; color: #756c67; font-size: .85rem; }
.store-footer__links { display: flex; align-items: center; gap: 1.25rem; }
.store-footer__resources h2 { margin: 0 0 .8rem; font-size: .85rem; font-weight: 600; }
.store-footer__links a { color: inherit; font-size: .82rem; }
.store-footer small { grid-column: 1 / -1; color: #756c67; font-size: .72rem; }
@media (max-width: 36rem) { .store-footer__inner { grid-template-columns: 1fr; } .store-footer__links { flex-wrap: wrap; } .store-footer small { grid-column: auto; } }
</style>
