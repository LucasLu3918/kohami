<script setup lang="ts">
import type { StorefrontLocale } from '../utils/storefront-locale'
import { localizedPath, storefrontLocales } from '../utils/storefront-locale'

defineProps<{ locale: StorefrontLocale }>()
const route = useRoute()
const query = computed(() => {
  const { lang: _legacyLanguage, ...preserved } = route.query
  return preserved
})
</script>

<template>
  <nav class="locale-switcher" aria-label="Language">
    <NuxtLink
      v-for="language in storefrontLocales"
      :key="language"
      :to="{ path: localizedPath(language, route.path), query }"
      :aria-current="language === locale ? 'page' : undefined"
    >{{ language }}</NuxtLink>
  </nav>
</template>

<style scoped>
.locale-switcher { display: flex; gap: .75rem; flex-wrap: wrap; }
.locale-switcher a { display: inline-flex; min-inline-size: 24px; min-block-size: 24px; align-items: center; justify-content: center; }
.locale-switcher a[aria-current="page"] { font-weight: 700; text-decoration-thickness: .15em; }
</style>
