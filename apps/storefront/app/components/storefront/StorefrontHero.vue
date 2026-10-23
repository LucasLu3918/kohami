<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const content = computed(() => props.locale === 'ja'
  ? { eyebrow: 'KOHAMI · AUTUMN EDIT', title: '今日のわたしに、ちょうどいい。', body: 'やわらかなニットや、気負わず着られる秋の一着。', cta: '秋の新着アイテムを見る', label: 'KOHAMI 秋のセレクト' }
  : props.locale === 'en'
    ? { eyebrow: 'KOHAMI · AUTUMN EDIT', title: 'A little softness, every day.', body: 'Easy knits and thoughtful layers for the days ahead.', cta: 'Explore autumn arrivals', label: 'KOHAMI autumn edit' }
    : { eyebrow: 'KOHAMI · AUTUMN EDIT', title: '今天的日常，穿得剛剛好。', body: '柔軟針織與自在層次，陪你迎接微涼的日子。', cta: '探索秋日新品', label: 'KOHAMI 秋日選品' })
</script>

<template>
  <section class="home-intro" :aria-label="content.label">
    <div class="storefront-hero">
      <img src="/demo-assets/products/demo-shirt-dress.png" :alt="locale === 'ja' ? '淡いベージュのシャツワンピース' : locale === 'en' ? 'Soft beige shirt dress from the local collection' : '本地選品中的米色襯衫洋裝'" width="1024" height="1024" loading="eager" decoding="async" fetchpriority="high" />
      <div class="storefront-hero__copy">
        <p class="storefront-hero__eyebrow">{{ content.eyebrow }}</p>
        <h1>{{ content.title }}</h1>
        <p>{{ content.body }}</p>
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ content.cta }} <span aria-hidden="true">→</span></NuxtLink>
      </div>
    </div>
  </section>
</template>

<style scoped>
.home-intro { max-width: 88rem; margin: .75rem auto 0; padding: 0 clamp(1rem, 3vw, 2.5rem); }
.storefront-hero { position: relative; min-width: 0; min-height: clamp(21rem, 31vw, 27rem); overflow: hidden; background: var(--store-linen); }
.storefront-hero img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; object-position: 50% 38%; opacity: .9; }
.storefront-hero::after { position: absolute; inset: 0; background: linear-gradient(90deg, rgb(251 247 248 / 96%) 0%, rgb(251 247 248 / 86%) 31%, rgb(251 247 248 / 42%) 49%, rgb(251 247 248 / 0%) 78%); content: ''; }
.storefront-hero__copy { position: relative; z-index: 1; display: flex; min-height: inherit; max-width: 35rem; flex-direction: column; justify-content: center; align-items: flex-start; padding: clamp(1.5rem, 5vw, 4.5rem); }
.storefront-hero__eyebrow { margin: 0 0 1rem; color: var(--store-accent-strong); font-size: .68rem; letter-spacing: .2em; }
.storefront-hero h1 { max-width: 10em; margin: 0; color: var(--store-ink-strong); font-family: var(--store-font-display); font-size: clamp(2rem, 4.2vw, 3.4rem); font-weight: 500; line-height: 1.3; letter-spacing: .03em; }
.storefront-hero__copy > p:not(.storefront-hero__eyebrow) { margin: 1rem 0 1.5rem; color: var(--store-muted-strong); font-size: .9rem; line-height: 1.8; }
.storefront-hero__copy > a { display: inline-flex; min-height: 2.8rem; align-items: center; gap: 1.25rem; padding: 0 1rem; background: var(--store-accent-strong); color: #fff; font-size: .8rem; text-decoration: none; transition: background-color .16s ease; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
.storefront-hero__copy > a:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
@media (max-width: 700px) {
  .home-intro { margin-top: .5rem; }
  .storefront-hero { min-height: 21rem; }
  .storefront-hero img { object-position: 60% center; }
  .storefront-hero::after { background: linear-gradient(90deg, rgb(251 247 248 / 94%), rgb(251 247 248 / 79%) 67%, rgb(251 247 248 / 35%)); }
  .storefront-hero__copy { padding: 1.5rem; }
}
@media (prefers-reduced-motion: reduce) { .storefront-hero__copy > a { transition: none; } }
</style>
