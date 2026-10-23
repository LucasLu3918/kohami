<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const content = computed(() => props.locale === 'ja'
  ? { eyebrow: 'KOHAMI  /  WARDROBE NOTES', title: '今日のわたしに、ちょうどいい服。', body: 'いつもの一日に、小さなときめきを。やわらかな色と着心地を大切に選びました。', cta: '新着アイテムを見る', label: 'KOHAMI 日常服のセレクト', tag: '好きな服と、いつもの私。' }
  : props.locale === 'en'
    ? { eyebrow: 'KOHAMI  /  WARDROBE NOTES', title: 'A little lovely, every day.', body: 'Soft colors, easy layers, and the pieces you will reach for again.', cta: 'Explore new arrivals', label: 'KOHAMI everyday wardrobe edit', tag: 'Made for your kind of day.' }
    : { eyebrow: 'KOHAMI  /  衣櫥手記', title: '把日常，穿成喜歡的樣子。', body: '柔和色調、自在層次，和那些讓每天都更可愛一點的衣服。', cta: '逛逛本週新品', label: 'KOHAMI 日常衣櫥選品', tag: '穿上喜歡的，去過喜歡的日常。' })
</script>

<template>
  <section class="home-intro" :aria-label="content.label">
    <div class="storefront-hero">
      <div class="storefront-hero__copy">
        <p class="storefront-hero__eyebrow">{{ content.eyebrow }}</p>
        <h1>{{ content.title }}</h1>
        <p>{{ content.body }}</p>
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ content.cta }} <span aria-hidden="true">→</span></NuxtLink>
        <p class="storefront-hero__signature"><span aria-hidden="true">✳</span>{{ content.tag }}</p>
      </div>
      <figure class="storefront-hero__visual">
        <img src="/demo-assets/products/demo-shirt-dress.png" :alt="locale === 'ja' ? '淡いベージュのシャツワンピース' : locale === 'en' ? 'Soft beige shirt dress from the local collection' : '本地選品中的米色襯衫洋裝'" width="1024" height="1024" loading="eager" decoding="async" fetchpriority="high" />
        <figcaption>{{ locale === 'ja' ? '今日の気分で、着こなそう。' : locale === 'en' ? 'Wear what feels like you.' : '今天，就穿喜歡的那一件。' }}</figcaption>
      </figure>
      <span class="storefront-hero__side-note" aria-hidden="true">DAILY WEAR · SOFT MOOD</span>
    </div>
  </section>
</template>

<style scoped>
.home-intro { max-width: 88rem; margin: 1.25rem auto 0; padding: 0 clamp(1rem, 3vw, 2.5rem); }
.storefront-hero { position: relative; display: grid; min-height: clamp(29rem, 43vw, 39rem); grid-template-columns: minmax(19rem, .84fr) minmax(0, 1.16fr); align-items: center; overflow: hidden; background: var(--store-paper); }
.storefront-hero::before { position: absolute; inset: 1rem; border: 1px solid var(--store-border); content: ''; pointer-events: none; }
.storefront-hero__copy { position: relative; z-index: 1; display: flex; min-height: inherit; flex-direction: column; justify-content: center; align-items: flex-start; padding: clamp(2.5rem, 6vw, 6.25rem); }
.storefront-hero__eyebrow { margin: 0 0 1.6rem; color: var(--store-accent-strong); font-family: var(--store-font-mono); font-size: .68rem; letter-spacing: .13em; }
.storefront-hero h1 { max-width: 9em; margin: 0; color: var(--store-ink-strong); font-family: var(--store-font-display); font-size: clamp(2.4rem, 4.5vw, 4.4rem); font-weight: 500; line-height: 1.28; letter-spacing: .035em; }
.storefront-hero__copy > p:not(.storefront-hero__eyebrow, .storefront-hero__signature) { max-width: 25rem; margin: 1.25rem 0 1.75rem; color: var(--store-muted-strong); font-size: .92rem; line-height: 1.9; }
.storefront-hero__copy > a { display: inline-flex; min-height: 3rem; align-items: center; gap: 1.5rem; padding: 0 1.25rem; background: var(--store-accent-strong); color: #fff; font-size: .82rem; text-decoration: none; transition: background-color .16s ease; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
.storefront-hero__copy > a:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.storefront-hero__signature { display: flex; align-items: center; gap: .65rem; margin: 2rem 0 0; color: var(--store-muted); font-size: .75rem; line-height: 1.6; }
.storefront-hero__signature span { display: grid; width: 1.65rem; height: 1.65rem; place-items: center; border: 1px solid var(--store-accent); border-radius: 50%; color: var(--store-accent-strong); font-size: .86rem; }
.storefront-hero__visual { position: relative; height: calc(100% - 3rem); min-height: 25rem; margin: 1.5rem 2.5rem 1.5rem 0; overflow: hidden; background: var(--store-linen); }
.storefront-hero__visual img { display: block; width: 100%; height: 100%; min-height: inherit; object-fit: cover; object-position: 50% 40%; }
.storefront-hero__visual figcaption { position: absolute; inset-inline-end: 1.25rem; inset-block-end: 1.25rem; padding: .65rem .9rem; background: var(--store-paper); color: var(--store-ink); font-size: .75rem; letter-spacing: .04em; }
.storefront-hero__side-note { position: absolute; inset-inline-end: .55rem; inset-block-start: 50%; color: var(--store-muted); font-family: var(--store-font-mono); font-size: .58rem; letter-spacing: .12em; writing-mode: vertical-rl; transform: translateY(-50%); }
@media (max-width: 700px) {
  .home-intro { margin-top: .75rem; }
  .storefront-hero { grid-template-columns: minmax(0, 1fr); }
  .storefront-hero::before { inset: .6rem; }
  .storefront-hero__copy { min-height: auto; padding: 2.5rem 2rem 1.5rem; }
  .storefront-hero__eyebrow { margin-bottom: 1.1rem; }
  .storefront-hero h1 { max-width: 12em; font-size: clamp(2.15rem, 9vw, 3rem); }
  .storefront-hero__copy > p:not(.storefront-hero__eyebrow, .storefront-hero__signature) { margin-block: 1rem 1.25rem; }
  .storefront-hero__signature { margin-top: 1.25rem; }
  .storefront-hero__visual { height: auto; min-height: 0; aspect-ratio: 1.08 / 1; margin: .5rem 1.35rem 1.5rem; }
  .storefront-hero__visual img { min-height: 0; }
  .storefront-hero__side-note { inset-inline-end: .25rem; font-size: .5rem; }
}
@media (prefers-reduced-motion: reduce) { .storefront-hero__copy > a { transition: none; } }
</style>
