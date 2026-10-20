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
  <NuxtPage />
  <StorefrontFooter v-if="showStoreShell" :locale="locale" />
</template>

<style>
:root {
  font-family: Arial, 'Noto Sans TC', 'Hiragino Kaku Gothic ProN', Meiryo, sans-serif;
  color: #332d2b;
  background: #fff;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  --store-ink: #332d2b;
  --store-muted: #756c67;
  --store-border: #eee9e4;
  --store-accent: #975e65;
  --store-paper: #f8f6f3;
}

body { margin: 0; min-width: 320px; }

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
  background: #25252c;
  color: #fff;
  font: inherit;
}

.skip-link:focus {
  transform: translateY(0);
  outline: 3px solid #235b9d;
  outline-offset: 3px;
}
</style>
