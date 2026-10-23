<script setup lang="ts">
import { useStorefrontLocale } from './composables/useStorefrontLocale'
import StorefrontFooter from './components/storefront/StorefrontFooter.vue'
import StorefrontHeader from './components/storefront/StorefrontHeader.vue'
import { localizedPath } from './utils/storefront-locale'

const { locale } = useStorefrontLocale()
const route = useRoute()
const showStoreShell = computed(() => !route.path.startsWith('/admin'))
const hideHeaderSearch = computed(() => route.path === localizedPath(locale.value, '/'))
const skipLinkLabel = computed(() => locale.value === 'en'
  ? 'Skip to main content'
  : locale.value === 'ja'
    ? 'メインコンテンツへスキップ'
    : '略過導覽，前往主要內容')
</script>

<template>
  <a class="skip-link" href="#main-content">{{ skipLinkLabel }}</a>
  <StorefrontHeader v-if="showStoreShell" :locale="locale" :hide-search="hideHeaderSearch" />
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <StorefrontFooter v-if="showStoreShell" :locale="locale" />
</template>

<style>
:root {
  font-family: var(--store-font-body);
  color: #33282e;
  background: #fff;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  --store-ink: #33282e;
  --store-ink-strong: #261f23;
  --store-muted: #746970;
  --store-muted-strong: #5c5157;
  --store-accent: #b77d8d;
  --store-accent-strong: #73334a;
  --store-accent-hover: #5b283c;
  --store-paper: #fbf7f8;
  --store-linen: #f3e8eb;
  --store-surface: #fff;
  --store-border: #dfd3d8;
  --store-border-subtle: #efe7ea;
  --store-border-strong: #88777e;
  --store-focus: #235b9d;
  --store-danger: #a11919;
  --store-danger-ink: #711010;
  --store-danger-surface: #fff3f3;
  --store-success: #277547;
  --store-success-surface: #f0faf4;
  --store-scrollbar-thumb: #bdaab1;
  --store-scrollbar-track: #fbf7f8;
  --store-scrollbar-hover: #8c6b77;
  --store-scrollbar-active: #73334a;
  --store-font-body: 'Noto Sans TC', 'Hiragino Kaku Gothic ProN', 'Yu Gothic', Meiryo, system-ui, sans-serif;
  --store-font-display: Georgia, 'Times New Roman', serif;
  --store-font-mono: ui-monospace, SFMono-Regular, Menlo, monospace;
  --store-radius-sm: .25rem;
  --store-radius: .5rem;
  --store-radius-lg: .75rem;
  scrollbar-color: var(--store-scrollbar-thumb) var(--store-scrollbar-track);
  scrollbar-width: auto;
}

body { margin: 0; min-width: 320px; font-family: var(--store-font-body); line-height: 1.6; }

::-webkit-scrollbar { width: .75rem; height: .75rem; }
::-webkit-scrollbar-track { background: var(--store-scrollbar-track); }
::-webkit-scrollbar-thumb { border: 2px solid var(--store-scrollbar-track); border-radius: 999px; background: var(--store-scrollbar-thumb); }
::-webkit-scrollbar-thumb:hover { background: var(--store-scrollbar-hover); }
::-webkit-scrollbar-thumb:active { background: var(--store-scrollbar-active); }

@media (forced-colors: active) {
  :root { scrollbar-color: auto; }
  ::-webkit-scrollbar-thumb { border-color: Canvas; background: CanvasText; }
  ::-webkit-scrollbar-track { background: Canvas; }
}

button:disabled {
  background: #625c58;
  color: #fff;
  cursor: not-allowed;
  opacity: 1;
}

.skip-link {
  position: fixed;
  z-index: 1000;
  inset-block-start: 0.5rem;
  inset-inline-start: 0.5rem;
  transform: translateY(-200%);
  padding: 0.65rem 1rem;
  border-radius: 0.25rem;
  background: var(--store-ink-strong);
  color: #fff;
  font: inherit;
}

.skip-link:focus {
  transform: translateY(0);
  outline: 3px solid var(--store-focus);
  outline-offset: 3px;
}
</style>
