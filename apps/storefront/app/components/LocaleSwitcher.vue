<script setup lang="ts">
import type { StorefrontLocale } from '../utils/storefront-locale'
import { localizedPath, storefrontLocales } from '../utils/storefront-locale'

const props = defineProps<{ locale: StorefrontLocale }>()
const route = useRoute()
const switcher = ref<HTMLDetailsElement | null>(null)
const query = computed(() => {
  const { lang: _legacyLanguage, ...preserved } = route.query
  return preserved
})
const localeNames: Record<StorefrontLocale, string> = { 'zh-TW': '繁體中文', ja: '日本語', en: 'English' }
const copy = computed(() => props.locale === 'ja'
  ? { label: '言語', title: '言語を選択', menu: '表示言語' }
  : props.locale === 'en'
    ? { label: 'Language', title: 'Choose a language', menu: 'Display language' }
    : { label: '語言', title: '選擇語言', menu: '顯示語言' })

function closeSwitcher() {
  if (switcher.value) switcher.value.open = false
}
</script>

<template>
  <details ref="switcher" class="locale-switcher">
    <summary class="locale-switcher__trigger" :aria-label="`${copy.label} · ${localeNames[locale]}`">
      <svg aria-hidden="true" viewBox="0 0 24 24" fill="none">
        <circle cx="12" cy="12" r="9" />
        <path d="M3.5 12h17M12 3c2.3 2.5 3.4 5.5 3.4 9s-1.1 6.5-3.4 9c-2.3-2.5-3.4-5.5-3.4-9S9.7 5.5 12 3Z" />
      </svg>
      <span>{{ localeNames[locale] }}</span>
      <svg class="locale-switcher__chevron" aria-hidden="true" viewBox="0 0 12 12" fill="none"><path d="m2 4 4 4 4-4" /></svg>
    </summary>
    <nav class="locale-switcher__menu" :aria-label="copy.menu">
      <p>{{ copy.title }}</p>
      <NuxtLink
        v-for="language in storefrontLocales"
        :key="language"
        :to="{ path: localizedPath(language, route.path), query }"
        :hreflang="language"
        :aria-current="language === locale ? 'page' : undefined"
        @click="closeSwitcher"
      >
        <span>{{ localeNames[language] }}</span>
        <svg v-if="language === locale" aria-hidden="true" viewBox="0 0 16 16" fill="none"><path d="m3 8 3 3 7-7" /></svg>
      </NuxtLink>
    </nav>
  </details>
</template>

<style scoped>
.locale-switcher { position: relative; z-index: 20; }
.locale-switcher__trigger { display: inline-flex; min-width: 2.75rem; min-height: 2.75rem; align-items: center; justify-content: center; gap: .5rem; padding: .4rem .75rem; border: 1px solid #d8d0d5; border-radius: 999px; background: #fff; color: #514b48; font: inherit; font-size: .78rem; cursor: pointer; list-style: none; white-space: nowrap; }
.locale-switcher__trigger::-webkit-details-marker { display: none; }
.locale-switcher__trigger::marker { content: ''; }
.locale-switcher__trigger > svg:first-child { width: 1rem; height: 1rem; stroke: currentColor; stroke-width: 1.5; }
.locale-switcher__chevron { width: .75rem; height: .75rem; stroke: currentColor; stroke-width: 1.5; transition: transform .16s ease; }
.locale-switcher[open] .locale-switcher__chevron { transform: rotate(180deg); }
.locale-switcher__menu { position: absolute; top: calc(100% + .5rem); right: 0; width: min(15.5rem, calc(100vw - 2rem)); padding: .5rem; border: 1px solid #e8e2de; border-radius: .65rem; background: #fff; box-shadow: 0 .75rem 2rem rgb(39 35 34 / 12%); }
.locale-switcher__menu p { margin: .35rem .6rem .5rem; color: var(--store-muted); font-size: .72rem; }
.locale-switcher__menu a { display: flex; min-height: 2.75rem; align-items: center; justify-content: space-between; gap: .75rem; padding: .5rem .65rem; border-radius: .4rem; color: var(--store-ink-strong); font-size: .85rem; text-decoration: none; }
.locale-switcher__menu a:hover, .locale-switcher__menu a:focus-visible { background: var(--store-paper); }
.locale-switcher__menu a[aria-current='page'] { background: #fbf4f6; color: var(--store-accent-strong); font-weight: 700; }
.locale-switcher__menu a svg { width: 1rem; height: 1rem; flex: 0 0 auto; stroke: currentColor; stroke-width: 2; }
@media (prefers-reduced-motion: reduce) { .locale-switcher__chevron { transition: none; } }
</style>
