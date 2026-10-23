<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { localizedPath } from '../../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const content = computed(() => props.locale === 'ja'
  ? { eyebrow: 'KOHAMI SPRING EDIT', title: '毎日に、やさしい装いを。', body: '軽やかな素材と、長く着たくなるシルエット。', cta: '新着アイテムを見る', categoriesLabel: '商品カテゴリー', categories: [{ label: 'ブラウス・トップス', query: 'ブラウス' }, { label: 'ニット・カーディガン', query: 'ニット' }, { label: 'ワンピース', query: 'ワンピース' }, { label: 'スカート・パンツ', query: 'スカート' }] }
  : props.locale === 'en'
    ? { eyebrow: 'KOHAMI SPRING EDIT', title: 'Soft layers for every day.', body: 'Light textures and easy silhouettes made to wear on repeat.', cta: 'Explore new arrivals', categoriesLabel: 'Shop categories', categories: [{ label: 'Blouses & tops', query: 'blouse' }, { label: 'Knitwear', query: 'knit' }, { label: 'Dresses', query: 'dress' }, { label: 'Skirts & trousers', query: 'skirt' }] }
    : { eyebrow: 'KOHAMI 春日選品', title: '把日常，穿得更柔軟。', body: '輕盈材質與自在剪裁，陪伴每一個日常時刻。', cta: '探索本週新品', categoriesLabel: '商品分類', categories: [{ label: '襯衫・上衣', query: '襯衫' }, { label: '針織・外搭', query: '針織' }, { label: '洋裝', query: '洋裝' }, { label: '裙裝・褲裝', query: '裙' }] })
</script>

<template>
  <section class="home-intro" aria-label="KOHAMI collection">
    <nav class="home-categories" :aria-label="content.categoriesLabel">
      <h2>{{ content.categoriesLabel }}</h2>
      <NuxtLink
        v-for="category in content.categories"
        :key="category.query"
        :to="{ path: localizedPath(locale, '/'), query: { q: category.query }, hash: '#catalog-heading' }"
      >{{ category.label }}<span aria-hidden="true">→</span></NuxtLink>
    </nav>
    <div class="storefront-hero">
      <img src="/demo-assets/products/demo-shirt-dress.png" :alt="locale === 'ja' ? '淡いベージュのシャツワンピース' : locale === 'en' ? 'Soft beige shirt dress from the local collection' : '本地選品中的米色襯衫洋裝'" fetchpriority="high" />
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
.home-intro { display: grid; grid-template-columns: minmax(11rem, 16rem) minmax(0, 1fr); gap: clamp(1rem, 3vw, 2.5rem); max-width: 88rem; margin: 1rem auto 0; padding: 0 clamp(1rem, 3vw, 2.5rem); }
.home-categories { align-self: center; display: grid; gap: .15rem; padding: 1.4rem .4rem; }
.home-categories h2 { margin: 0 0 .8rem; color: var(--store-muted); font-size: .7rem; font-weight: 500; letter-spacing: .18em; text-transform: uppercase; }
.home-categories a { display: flex; align-items: center; justify-content: space-between; gap: .5rem; padding: .85rem .2rem; border-bottom: 1px solid var(--store-border-subtle); color: var(--store-ink); font-size: .9rem; text-decoration: none; }
.home-categories a span { color: var(--store-accent); }
.home-categories a:hover, .home-categories a:focus-visible { color: var(--store-accent); }
.storefront-hero { position: relative; min-width: 0; min-height: clamp(22rem, 43vw, 37rem); overflow: hidden; background: var(--store-linen); }
.storefront-hero img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; object-position: 50% 38%; opacity: .72; }
.storefront-hero::after { position: absolute; inset: 0; background: linear-gradient(90deg, rgb(250 247 242 / 94%) 0%, rgb(250 247 242 / 83%) 35%, rgb(250 247 242 / 8%) 78%); content: ''; }
.storefront-hero__copy { position: relative; z-index: 1; display: flex; min-height: inherit; max-width: 35rem; flex-direction: column; justify-content: center; align-items: flex-start; padding: clamp(1.5rem, 5vw, 4.5rem); }
.storefront-hero__eyebrow { margin: 0 0 1.1rem; color: #8d6664; font-size: .68rem; letter-spacing: .2em; }
.storefront-hero h1 { margin: 0; color: #392f2c; font-family: var(--store-font-display); font-size: clamp(2rem, 4.6vw, 3.7rem); font-weight: 500; line-height: 1.25; letter-spacing: .04em; }
.storefront-hero__copy > p:not(.storefront-hero__eyebrow) { margin: 1rem 0 1.5rem; color: var(--store-muted-strong); font-size: .9rem; line-height: 1.8; }
.storefront-hero__copy > a { display: inline-flex; min-height: 2.8rem; align-items: center; gap: 1.25rem; padding: 0 1rem; background: var(--store-accent-strong); color: #fff; font-size: .8rem; text-decoration: none; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
@media (max-width: 700px) {
  .home-intro { grid-template-columns: minmax(0, 1fr); gap: .6rem; margin-top: .5rem; }
  .home-categories { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 .8rem; padding: .5rem 0; }
  .home-categories h2 { grid-column: 1 / -1; margin-bottom: .2rem; }
  .home-categories a { padding: .65rem .1rem; font-size: .82rem; }
  .storefront-hero { min-height: 26rem; }
  .storefront-hero img { object-position: 60% center; }
  .storefront-hero::after { background: linear-gradient(90deg, rgb(250 247 242 / 92%), rgb(250 247 242 / 65%) 70%, rgb(250 247 242 / 26%)); }
  .storefront-hero__copy { padding: 1.5rem; }
}
</style>
