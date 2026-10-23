<script setup lang="ts">
import { useStorefrontLocale } from './composables/useStorefrontLocale'
import StorefrontFooter from './components/storefront/StorefrontFooter.vue'
import StorefrontHeader from './components/storefront/StorefrontHeader.vue'

const { locale } = useStorefrontLocale()
const route = useRoute()
const showStoreShell = computed(() => !route.path.startsWith('/admin'))
const skipLinkLabel = computed(() => locale.value === 'en'
  ? 'Skip to main content'
  : locale.value === 'ja'
    ? 'メインコンテンツへスキップ'
    : '略過導覽，前往主要內容')
</script>

<template>
  <a class="skip-link" href="#main-content">{{ skipLinkLabel }}</a>
  <StorefrontHeader v-if="showStoreShell" :locale="locale" />
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <StorefrontFooter v-if="showStoreShell" :locale="locale" />
</template>

<style>
:root {
  font-family: var(--store-font-body);
  color: #332d2b;
  background: #fff;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  --store-ink: #332d2b;
  --store-ink-strong: #272322;
  --store-muted: #756c67;
  --store-muted-strong: #625c58;
  --store-accent: #975e65;
  --store-accent-strong: #6d2942;
  --store-accent-hover: #4e1d30;
  --store-paper: #f8f6f3;
  --store-linen: #f3ede6;
  --store-surface: #fff;
  --store-border: #ded9d5;
  --store-border-subtle: #eee9e4;
  --store-border-strong: #8a817b;
  --store-focus: #235b9d;
  --store-danger: #a11919;
  --store-danger-ink: #711010;
  --store-danger-surface: #fff3f3;
  --store-success: #277547;
  --store-success-surface: #f0faf4;
  --store-scrollbar-thumb: #b8aaa4;
  --store-scrollbar-track: #f8f6f3;
  --store-scrollbar-hover: #957d78;
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
