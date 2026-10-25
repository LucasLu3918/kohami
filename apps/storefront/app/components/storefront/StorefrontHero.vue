<script setup lang="ts">
import { shallowRef } from 'vue'
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { PhArrowLeft, PhArrowRight } from '@phosphor-icons/vue'

const props = defineProps<{ locale: StorefrontLocale }>()
const activeSlide = shallowRef(0)
const slides = [
  { src: '/demo-assets/editorial/model-master.png', alt: { 'zh-TW': 'KOHAMI 虛構成年型錄模特，日光下的自然人像', ja: 'KOHAMIの架空の成人モデル、自然光のポートレート', en: 'KOHAMI fictional adult catalog model in soft natural light' } },
  { src: '/demo-assets/editorial/banners/hero-01-collection.png', alt: { 'zh-TW': '米白襯衫搭配霧玫瑰百褶裙，午後窗光下的日常選品', ja: 'アイボリーのブラウスとくすみローズのプリーツスカート', en: 'Ivory blouse and dusty rose pleated skirt in afternoon light' } },
  { src: '/demo-assets/editorial/banners/hero-02-sage-skirt.png', alt: { 'zh-TW': '鼠尾草綠長裙與米白開襟衫的輕柔搭配', ja: 'セージグリーンのロングスカートとアイボリーのカーディガン', en: 'Sage skirt with a soft ivory cardigan' } },
  { src: '/demo-assets/editorial/banners/hero-03-floral-dress.png', alt: { 'zh-TW': '碎花洋裝搭配淺燕麥色針織外套', ja: '小花柄ワンピースと淡いオートミール色のカーディガン', en: 'Floral dress with a light oatmeal cardigan' } },
  { src: '/demo-assets/editorial/banners/hero-04-denim-skirt.png', alt: { 'zh-TW': '淺藍襯衫與深藍丹寧長裙，簡單自在的日常搭配', ja: 'ペールブルーのブラウスと濃紺デニムスカート', en: 'Powder blue blouse with an indigo denim skirt' } },
  { src: '/demo-assets/editorial/banners/hero-05-autumn-knit.png', alt: { 'zh-TW': '栗棕色針織外套搭配灰色百褶裙的秋日穿搭', ja: '栗色のニットカーディガンとグレーのプリーツスカート', en: 'Chestnut cardigan with a grey pleated skirt' } },
] as const
const content = computed(() => props.locale === 'ja'
  ? { title: '心地よく、いつもの日々を。', body: '肌ざわりも、シルエットも気持ちよく。好きな服で、毎日の小さな時間を。', cta: '新着アイテムを見る', label: 'KOHAMI 日常服のセレクト', note: 'いつもの日々に、心地よい一着。' }
  : props.locale === 'en'
    ? { title: 'Comfort for the little things.', body: 'Soft fabrics, easy shapes. Wear what you love through the small moments of every day.', cta: 'Explore new arrivals', label: 'KOHAMI everyday wardrobe edit', note: 'A little ease, every day.' }
    : { title: '穿得舒服，\n日子就慢一點。', body: '柔軟的布料、自在的輪廓。把喜歡穿進每天的小日子。', cta: '逛逛本週新品', label: 'KOHAMI 日常衣櫥選品', note: 'いつもの日々に、心地よい一着。' })

function moveSlide(direction: -1 | 1) {
  activeSlide.value = (activeSlide.value + direction + slides.length) % slides.length
}
</script>

<template>
  <section class="home-intro" :aria-label="content.label">
    <div class="storefront-hero">
      <div class="storefront-hero__copy">
        <h1>{{ content.title }}</h1>
        <p>{{ content.body }}</p>
        <span class="storefront-hero__note" lang="ja">{{ content.note }}</span>
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ content.cta }} <span aria-hidden="true">→</span></NuxtLink>
      </div>
      <figure class="storefront-hero__visual">
        <Transition name="hero-image" mode="out-in">
          <img :key="slides[activeSlide]!.src" :src="slides[activeSlide]!.src" :alt="slides[activeSlide]!.alt[locale]" width="1024" height="1024" loading="eager" decoding="async" fetchpriority="high" />
        </Transition>
        <div class="storefront-hero__controls" role="group" :aria-label="locale === 'ja' ? 'バナー切り替え' : locale === 'en' ? 'Banner controls' : '主視覺切換'">
          <button type="button" :aria-label="locale === 'ja' ? '前のバナー' : locale === 'en' ? 'Previous banner' : '上一張主視覺'" @click="moveSlide(-1)">
            <PhArrowLeft :size="18" weight="regular" aria-hidden="true" />
          </button>
          <button v-for="(_, index) in slides" :key="index" type="button" class="storefront-hero__dot" :class="{ 'is-active': activeSlide === index }" :aria-label="locale === 'ja' ? `バナー ${index + 1}` : locale === 'en' ? `Banner ${index + 1}` : `第 ${index + 1} 張主視覺`" :aria-current="activeSlide === index ? 'true' : undefined" @click="activeSlide = index" />
          <button type="button" :aria-label="locale === 'ja' ? '次のバナー' : locale === 'en' ? 'Next banner' : '下一張主視覺'" @click="moveSlide(1)">
            <PhArrowRight :size="18" weight="regular" aria-hidden="true" />
          </button>
        </div>
      </figure>
    </div>
  </section>
</template>

<style scoped>
.home-intro { max-width: none; margin: 0; padding: 0; }
.storefront-hero { display: grid; height: min(78vh, 42rem); min-height: 31rem; grid-template-columns: minmax(0, .86fr) minmax(0, 1.14fr); align-items: stretch; overflow: hidden; background: var(--store-surface); }
.storefront-hero__copy { display: flex; flex-direction: column; justify-content: center; align-items: flex-start; padding: clamp(2.5rem, 6.5vw, 7rem); }
.storefront-hero h1 { max-width: 12em; margin: 0; color: var(--store-ink-strong); font-family: var(--store-font-display); font-size: clamp(2.5rem, 3.7vw, 3.6rem); font-weight: 500; line-height: 1.3; letter-spacing: .035em; white-space: pre-line; }
.storefront-hero__copy > p { max-width: 32rem; margin: 1.2rem 0 1.7rem; color: var(--store-muted-strong); font-size: .95rem; line-height: 1.9; }
.storefront-hero__note { margin: -.8rem 0 1.3rem; color: var(--store-accent-strong); font-size: .76rem; letter-spacing: .08em; }
.storefront-hero__copy > a { display: inline-flex; min-height: 3rem; align-items: center; gap: 1.5rem; padding: 0 1.25rem; background: var(--store-accent-strong); color: #fff; font-size: .82rem; text-decoration: none; transition: background-color .16s ease; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
.storefront-hero__copy > a:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.storefront-hero__visual { position: relative; min-width: 0; min-height: 0; height: 100%; margin: 0; overflow: hidden; background: var(--store-linen); }
.storefront-hero__visual img { display: block; width: 100%; height: 100%; object-fit: contain; object-position: center; }
.hero-image-enter-active, .hero-image-leave-active { transition: opacity .18s ease; }
.hero-image-enter-from, .hero-image-leave-to { opacity: 0; }
.storefront-hero__controls { position: absolute; inset-inline-end: clamp(1rem, 3vw, 2.5rem); inset-block-end: 1rem; display: flex; align-items: center; gap: .65rem; }
.storefront-hero__controls button { display: grid; width: 2.35rem; height: 2.35rem; place-items: center; border: 1px solid rgb(255 255 255 / 68%); border-radius: 50%; background: rgb(255 255 255 / 88%); color: var(--store-ink-strong); font: inherit; cursor: pointer; transition: background-color .16s ease, color .16s ease; }
.storefront-hero__controls button:hover, .storefront-hero__controls button:focus-visible { background: var(--store-accent-strong); color: #fff; }
.storefront-hero__controls button:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.storefront-hero__controls .storefront-hero__dot { width: .55rem; height: .55rem; border: 0; background: rgb(255 255 255 / 65%); }
.storefront-hero__controls .storefront-hero__dot.is-active { background: #fff; box-shadow: 0 0 0 1px rgb(65 50 47 / 44%); }
@media (prefers-reduced-motion: reduce) { .storefront-hero__controls button, .hero-image-enter-active, .hero-image-leave-active { transition: none; } }
@media (max-width: 760px) {
  .storefront-hero { grid-template-columns: minmax(0, 1fr); }
  .storefront-hero { height: auto; min-height: 0; }
  .storefront-hero__copy { padding: 2.4rem 1.25rem 1.6rem; }
  .storefront-hero h1 { max-width: 12em; font-size: clamp(2.25rem, 9vw, 3.2rem); }
  .storefront-hero__copy > p { margin: 1rem 0 1.3rem; font-size: .88rem; }
  .storefront-hero__visual { height: auto; aspect-ratio: 1.12 / 1; }
}
@media (prefers-reduced-motion: reduce) { .storefront-hero__copy > a { transition: none; } }
</style>
