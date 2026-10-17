<script setup lang="ts">
import { useStorefrontLocale } from './composables/useStorefrontLocale'

const { locale } = useStorefrontLocale()
const skipLinkLabel = computed(() => locale.value === 'en'
  ? 'Skip to main content'
  : locale.value === 'ja'
    ? 'メインコンテンツへスキップ'
    : '略過導覽，前往主要內容')
</script>

<template>
  <a class="skip-link" href="#main-content">{{ skipLinkLabel }}</a>
  <NuxtPage />
</template>

<style>
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
