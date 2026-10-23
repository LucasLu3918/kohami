<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const content = computed(() => props.locale === 'ja'
  ? { title: '今日のわたしに、ちょうどいい服。', body: '細部に自分らしいリズムを見つけて、好きな服とともに、何気なくも大切な毎日をつないでいく。', cta: '新着アイテムを見る', label: 'KOHAMI 日常服のセレクト' }
  : props.locale === 'en'
    ? { title: 'A little lovely, every day.', body: 'Find your own rhythm in the details, and weave the pieces you love into everyday moments.', cta: 'Explore new arrivals', label: 'KOHAMI everyday wardrobe edit' }
    : { title: '把日常，穿成喜歡的樣子。', body: '從細節中找到屬於自己的節奏，用喜歡的單品，串起每一個平凡而珍貴的日常。', cta: '逛逛本週新品', label: 'KOHAMI 日常衣櫥選品' })
</script>

<template>
  <section class="home-intro" :aria-label="content.label">
    <div class="storefront-hero">
      <div class="storefront-hero__copy">
        <h1>{{ content.title }}</h1>
        <p>{{ content.body }}</p>
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ content.cta }} <span aria-hidden="true">→</span></NuxtLink>
      </div>
      <figure class="storefront-hero__visual">
        <img src="/demo-assets/products/demo-shirt-dress.png" :alt="locale === 'ja' ? '淡いベージュのシャツワンピース' : locale === 'en' ? 'Soft beige shirt dress from the local collection' : '本地選品中的米色襯衫洋裝'" width="1024" height="1024" loading="eager" decoding="async" fetchpriority="high" />
      </figure>
    </div>
  </section>
</template>

<style scoped>
.home-intro { max-width: none; margin: 0; padding: 0; }
.storefront-hero { display: grid; min-height: clamp(27rem, 35vw, 34rem); grid-template-columns: minmax(0, .86fr) minmax(0, 1.14fr); align-items: stretch; overflow: hidden; background: var(--store-surface); }
.storefront-hero__copy { display: flex; flex-direction: column; justify-content: center; align-items: flex-start; padding: clamp(2.5rem, 6.5vw, 7rem); }
.storefront-hero h1 { max-width: 10em; margin: 0; color: var(--store-ink-strong); font-family: var(--store-font-display); font-size: clamp(2.75rem, 4.4vw, 4.3rem); font-weight: 500; line-height: 1.3; letter-spacing: .035em; }
.storefront-hero__copy > p { max-width: 32rem; margin: 1.2rem 0 1.7rem; color: var(--store-muted-strong); font-size: .95rem; line-height: 1.9; }
.storefront-hero__copy > a { display: inline-flex; min-height: 3rem; align-items: center; gap: 1.5rem; padding: 0 1.25rem; background: var(--store-accent-strong); color: #fff; font-size: .82rem; text-decoration: none; transition: background-color .16s ease; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
.storefront-hero__copy > a:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.storefront-hero__visual { min-width: 0; min-height: 27rem; margin: 0; overflow: hidden; background: var(--store-linen); }
.storefront-hero__visual img { display: block; width: 100%; height: 100%; object-fit: cover; object-position: 50% 38%; }
@media (max-width: 760px) {
  .storefront-hero { grid-template-columns: minmax(0, 1fr); }
  .storefront-hero__copy { padding: 2.4rem 1.25rem 1.6rem; }
  .storefront-hero h1 { max-width: 12em; font-size: clamp(2.25rem, 9vw, 3.2rem); }
  .storefront-hero__copy > p { margin: 1rem 0 1.3rem; font-size: .88rem; }
  .storefront-hero__visual { min-height: 0; aspect-ratio: 1.12 / 1; }
}
@media (prefers-reduced-motion: reduce) { .storefront-hero__copy > a { transition: none; } }
</style>
