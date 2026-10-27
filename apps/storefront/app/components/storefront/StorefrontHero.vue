<script setup lang="ts">
import type { StorefrontLocale } from '../../utils/storefront-locale'
import { PhArrowLeft, PhArrowRight } from '@phosphor-icons/vue'
import { useHeroCarousel } from '../../composables/useHeroCarousel'

const props = defineProps<{ locale: StorefrontLocale }>()

const slides = [
  {
    id: 'iori-portrait',
    src: '/demo-assets/editorial/model-master.png',
    position: 'bottom-right',
    alt: { 'zh-TW': '伊織在日光下的自然人像', ja: '伊織の自然光ポートレート', en: 'Iori in a soft natural-light portrait' },
    title: { 'zh-TW': '日常的柔軟留白', ja: '日常にやわらかな余白を', en: 'A softer pause in the day' },
    description: { 'zh-TW': '用輕柔布料與自在輪廓，記下每天喜歡的穿搭。', ja: '軽やかな素材と自然なシルエットで、好きな装いを。', en: 'Easy shapes and soft fabrics for the everyday.' },
  },
  {
    id: 'urban-occasion',
    src: '/demo-assets/editorial/banners/hero-01-collection.png',
    position: 'bottom-right',
    alt: { 'zh-TW': '伊織與沙織穿著黑灰蕾絲層次造型，置身日式木質空間', ja: '伊織と沙織のレースを重ねた黒の装い', en: 'Iori and Saori in layered black lace looks in a Japanese timber interior' },
    title: { 'zh-TW': '蕾絲，收進日常裡', ja: 'レースを日常の装いに', en: 'Lace, made for everyday' },
    description: { 'zh-TW': '灰色羅紋、霧面緞光與細緻蕾絲，穿出日系層次。', ja: 'グレーのリブとサテンの艶、繊細なレースを重ねて。', en: 'Rib knit, satin sheen and delicate lace in quiet layers.' },
  },
  {
    id: 'sage-skirt',
    src: '/demo-assets/editorial/banners/hero-02-sage-skirt.png',
    position: 'bottom-left',
    alt: { 'zh-TW': '鼠尾草綠長裙與米白開襟衫的輕柔搭配', ja: 'セージグリーンのロングスカートとアイボリーのカーディガン', en: 'Sage skirt with a soft ivory cardigan' },
    title: { 'zh-TW': '把柔和色調穿進午後', ja: '午後に似合うやさしい色', en: 'Soft tones for the afternoon' },
    description: { 'zh-TW': '鼠尾草綠與米白針織，讓簡單日常多一點從容。', ja: 'セージグリーンとアイボリーのニットで、穏やかな日常に。', en: 'Sage and ivory knit bring an easy calm to everyday dressing.' },
  },
  {
    id: 'floral-dress',
    src: '/demo-assets/editorial/banners/hero-03-floral-dress.png',
    position: 'bottom-right',
    alt: { 'zh-TW': '碎花洋裝搭配淺燕麥色針織外套', ja: '小花柄ワンピースと淡いオートミール色のニット', en: 'Floral dress with a light oatmeal cardigan' },
    title: { 'zh-TW': '花柄與針織的輕盈相遇', ja: '花柄とニットの軽やかな出会い', en: 'A light meeting of florals and knit' },
    description: { 'zh-TW': '細緻花色遇上柔軟針織，日常也有小小浪漫。', ja: '小さな花柄とやわらかなニットで、日々にささやかな華やぎを。', en: 'Small florals and soft knit add a quiet touch of romance.' },
  },
  {
    id: 'denim-skirt',
    src: '/demo-assets/editorial/banners/hero-04-denim-skirt.png',
    position: 'bottom-left',
    alt: { 'zh-TW': '淺藍襯衫與深藍丹寧長裙的自在日常搭配', ja: 'ペールブルーのブラウスと濃紺デニムスカート', en: 'Powder-blue blouse with an indigo denim skirt' },
    title: { 'zh-TW': '藍色系的自在步調', ja: 'ブルーで整える自然なリズム', en: 'An easy rhythm in blue' },
    description: { 'zh-TW': '淺藍襯衫配深色丹寧，俐落又不拘束。', ja: '淡いブルーのブラウスに濃紺デニムを合わせて。', en: 'A powder-blue blouse and dark denim feel relaxed and composed.' },
  },
  {
    id: 'autumn-knit',
    src: '/demo-assets/editorial/banners/hero-05-autumn-knit.png',
    position: 'bottom-left',
    alt: { 'zh-TW': '栗棕針織外套搭配灰色百褶裙的秋日穿搭', ja: '栗色のニットカーディガンとグレーのプリーツスカート', en: 'Chestnut cardigan with a grey pleated skirt' },
    title: { 'zh-TW': '把秋天的暖意穿上身', ja: '秋のぬくもりをまとう', en: 'Wear a little autumn warmth' },
    description: { 'zh-TW': '栗棕針織與灰色百褶，層次溫柔、步伐輕鬆。', ja: '栗色のニットとグレーのプリーツで、やわらかな重なりを。', en: 'Chestnut knit and grey pleats make a gentle layered look.' },
  },
  {
    id: 'kissaten-duo',
    src: '/demo-assets/editorial/banners/hero-06-kissaten-duo.png',
    position: 'bottom-right',
    alt: { 'zh-TW': '伊織與沙織在日式咖啡店互看微笑，穿著黑色與深藍蕾絲洋裝', ja: '喫茶店で微笑み合う伊織と沙織、黒と紺のレーススタイル', en: 'Iori and Saori sharing a smile in a Japanese cafe, wearing black and navy lace looks' },
    title: { 'zh-TW': '喫茶店裡的默契', ja: '喫茶店で見つけたふたりのリズム', en: 'A shared moment at the kissaten' },
    description: { 'zh-TW': '替彼此整理袖口，也替日常添上一點蕾絲。', ja: '袖口を整え合うひとときに、レースの彩りを。', en: 'A little lace and a small moment shared between friends.' },
  },
  {
    id: 'japanese-garden-duo',
    src: '/demo-assets/editorial/banners/hero-07-japanese-park-duo.png',
    position: 'bottom-left',
    alt: { 'zh-TW': '伊織與沙織在日式庭園並肩散步，手挽手自然交談', ja: '日本庭園を腕を組んで歩く伊織と沙織', en: 'Iori and Saori walking arm in arm through a Japanese garden' },
    title: { 'zh-TW': '沿著庭園，並肩慢行', ja: '庭園の小径を、ふたりで', en: 'Together along the garden path' },
    description: { 'zh-TW': '深藍花紋、黑色蕾絲與輕柔步伐，在木緣側旁相映。', ja: '紺の花柄と黒いレースが、木の縁側にやさしく映える。', en: 'Navy florals and black lace move softly beside the timber engawa.' },
  },
] as const

const { activeSlide, isManuallyPaused, moveSlide, prefersReducedMotion, setFocusWithin, setPointerInside, showSlide, togglePause } = useHeroCarousel({
  slideCount: slides.length,
  intervalMs: 3000,
})
const currentSlide = computed(() => slides[activeSlide.value]!)
const content = computed(() => props.locale === 'ja'
  ? { title: '心地よく、いつもの日々を。', body: '肌ざわりも、シルエットも気持ちよく。好きな服で、毎日の小さな時間を。', cta: '新着アイテムを見る', label: 'KOHAMI 日常服のセレクト', controls: 'バナー切り替え', previous: '前のバナー', next: '次のバナー', pause: '自動再生を一時停止', play: '自動再生を開始' }
  : props.locale === 'en'
    ? { title: 'Comfort for the little things.', body: 'Soft fabrics, easy shapes. Wear what you love through the small moments of every day.', cta: 'Explore new arrivals', label: 'KOHAMI everyday wardrobe edit', controls: 'Banner controls', previous: 'Previous banner', next: 'Next banner', pause: 'Pause autoplay', play: 'Start autoplay', reduced: 'Autoplay is off because reduced motion is enabled' }
    : { title: '穿得舒服，\n日子就慢一點。', body: '柔軟的布料、自在的輪廓。把喜歡穿進每天的小日子。', cta: '逛逛本週新品', label: 'KOHAMI 日常衣櫥選品', controls: '主視覺切換', previous: '上一張主視覺', next: '下一張主視覺', pause: '暫停自動播放', play: '開始自動播放', reduced: '已依減少動態設定停用自動播放' })
</script>

<template>
  <section class="home-intro" :aria-label="content.label">
    <div class="storefront-hero">
      <div class="storefront-hero__copy">
        <h1>{{ content.title }}</h1>
        <p>{{ content.body }}</p>
        <NuxtLink :to="`${localizedPath(locale, '/')}#catalog-heading`">{{ content.cta }} <span aria-hidden="true">→</span></NuxtLink>
      </div>
      <figure class="storefront-hero__visual" @mouseenter="setPointerInside(true)" @mouseleave="setPointerInside(false)" @focusin="setFocusWithin(true)" @focusout="setFocusWithin(false)">
        <Transition name="hero-image" mode="out-in">
          <img :key="currentSlide.src" :src="currentSlide.src" :alt="currentSlide.alt[locale]" width="1024" height="1024" loading="eager" decoding="async" fetchpriority="high" aria-live="off" />
        </Transition>
        <Transition name="hero-caption" mode="out-in">
          <figcaption :key="currentSlide.id" class="storefront-hero__caption" :class="`is-${currentSlide.position}`" aria-live="off">
            <span class="storefront-hero__eyebrow">KOHAMI · {{ props.locale === 'ja' ? 'スタイルノート' : props.locale === 'en' ? 'STYLE NOTE' : '日式穿搭紀錄' }}</span>
            <h2>{{ currentSlide.title[locale] }}</h2>
            <p>{{ currentSlide.description[locale] }}</p>
          </figcaption>
        </Transition>
        <div class="storefront-hero__controls" role="group" :aria-label="content.controls">
          <button type="button" :aria-label="content.previous" @click="moveSlide(-1)">
            <PhArrowLeft :size="18" weight="regular" aria-hidden="true" />
          </button>
          <button v-for="(slide, index) in slides" :key="slide.id" type="button" class="storefront-hero__dot" :class="{ 'is-active': activeSlide === index }" :aria-label="locale === 'ja' ? `バナー ${index + 1}` : locale === 'en' ? `Banner ${index + 1}` : `第 ${index + 1} 張主視覺`" :aria-current="activeSlide === index ? 'true' : undefined" @click="showSlide(index)" />
          <button type="button" :aria-label="content.next" @click="moveSlide(1)">
            <PhArrowRight :size="18" weight="regular" aria-hidden="true" />
          </button>
          <button type="button" class="storefront-hero__play" :aria-label="prefersReducedMotion ? content.reduced : isManuallyPaused ? content.play : content.pause" :aria-pressed="isManuallyPaused" :disabled="prefersReducedMotion" @click="togglePause">
            <span aria-hidden="true">{{ isManuallyPaused || prefersReducedMotion ? '▶' : 'Ⅱ' }}</span>
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
.storefront-hero__copy > a { display: inline-flex; min-height: 3rem; align-items: center; gap: 1.5rem; padding: 0 1.25rem; background: var(--store-accent-strong); color: #fff; font-size: .82rem; text-decoration: none; transition: background-color .16s ease; }
.storefront-hero__copy > a:hover, .storefront-hero__copy > a:focus-visible { background: var(--store-accent-hover); }
.storefront-hero__copy > a:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 3px; }
.storefront-hero__visual { position: relative; min-width: 0; min-height: 0; height: 100%; margin: 0; overflow: hidden; background: var(--store-linen); }
.storefront-hero__visual > img { display: block; width: 100%; height: 100%; object-fit: contain; object-position: center; }
.hero-image-enter-active, .hero-image-leave-active, .hero-caption-enter-active, .hero-caption-leave-active { transition: opacity .22s ease; }
.hero-image-enter-from, .hero-image-leave-to, .hero-caption-enter-from, .hero-caption-leave-to { opacity: 0; }
.storefront-hero__caption { position: absolute; z-index: 1; width: min(19rem, calc(100% - 2rem)); padding: 1rem 1.15rem 1.05rem; border: 1px solid rgb(255 255 255 / 72%); background: rgb(250 248 244 / 91%); color: var(--store-ink-strong); box-shadow: 0 .45rem 1.5rem rgb(35 29 25 / 12%); backdrop-filter: blur(12px); }
.storefront-hero__caption.is-bottom-left { inset-block-end: 4.75rem; inset-inline-start: 1.25rem; }
.storefront-hero__caption.is-bottom-right { inset-block-end: 4.75rem; inset-inline-end: 1.25rem; }
.storefront-hero__eyebrow { color: var(--store-accent-strong); font-size: .62rem; letter-spacing: .12em; }
.storefront-hero__caption h2 { margin: .45rem 0 .3rem; font-family: var(--store-font-display); font-size: clamp(1rem, 1.5vw, 1.25rem); font-weight: 500; line-height: 1.45; }
.storefront-hero__caption p { margin: 0; color: var(--store-muted-strong); font-size: .76rem; line-height: 1.65; }
.storefront-hero__controls { position: absolute; z-index: 2; inset-inline-end: clamp(1rem, 3vw, 2.5rem); inset-block-end: 1rem; display: flex; align-items: center; gap: .55rem; }
.storefront-hero__controls button { display: grid; width: 2.35rem; height: 2.35rem; place-items: center; border: 1px solid rgb(255 255 255 / 68%); border-radius: 50%; background: rgb(255 255 255 / 92%); color: var(--store-ink-strong); font: inherit; cursor: pointer; transition: background-color .16s ease, color .16s ease; }
.storefront-hero__controls button:hover, .storefront-hero__controls button:focus-visible { background: var(--store-accent-strong); color: #fff; }
.storefront-hero__controls button:focus-visible { outline: 3px solid var(--store-focus); outline-offset: 2px; }
.storefront-hero__controls .storefront-hero__dot { position: relative; width: 1.5rem; height: 1.5rem; padding: 0; border: 0; background: transparent; }
.storefront-hero__controls .storefront-hero__dot:hover { background: transparent; color: inherit; }
.storefront-hero__controls .storefront-hero__dot::before { content: ''; position: absolute; top: 50%; left: 50%; width: .55rem; height: .55rem; border-radius: 50%; background: rgb(255 255 255 / 68%); transform: translate(-50%, -50%); }
.storefront-hero__controls .storefront-hero__dot:hover::before, .storefront-hero__controls .storefront-hero__dot:focus-visible::before { background: var(--store-accent-strong); }
.storefront-hero__controls .storefront-hero__dot.is-active::before { background: #fff; box-shadow: 0 0 0 1px rgb(65 50 47 / 44%); }
.storefront-hero__controls .storefront-hero__play { margin-inline-start: .2rem; font-size: .8rem; }
@media (prefers-reduced-motion: reduce) { .storefront-hero__controls button, .hero-image-enter-active, .hero-image-leave-active, .hero-caption-enter-active, .hero-caption-leave-active, .storefront-hero__copy > a { transition: none; } }
@media (max-width: 760px) {
  .storefront-hero { grid-template-columns: minmax(0, 1fr); height: auto; min-height: 0; }
  .storefront-hero__copy { padding: 2.4rem 1.25rem 1.6rem; }
  .storefront-hero h1 { max-width: 12em; font-size: clamp(2.25rem, 9vw, 3.2rem); }
  .storefront-hero__copy > p { margin: 1rem 0 1.3rem; font-size: .88rem; }
  .storefront-hero__visual { height: auto; aspect-ratio: 1.12 / 1; }
  .storefront-hero__caption { width: min(17rem, calc(100% - 1.5rem)); padding: .75rem .85rem; }
  .storefront-hero__caption.is-bottom-left { inset-block-end: 4rem; inset-inline-start: .75rem; }
  .storefront-hero__caption.is-bottom-right { inset-block-end: 4rem; inset-inline-end: .75rem; }
  .storefront-hero__caption p { font-size: .69rem; }
  .storefront-hero__controls { inset-inline-end: .75rem; inset-block-end: .7rem; gap: .25rem; }
  .storefront-hero__controls button { width: 2.15rem; height: 2.15rem; }
}
</style>
