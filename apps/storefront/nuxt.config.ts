import { demoProductSlugs } from './app/utils/demo-api'

const cert = process.env.Foundation_HTTPS_CERT
const key = process.env.Foundation_HTTPS_KEY
const publicDemo = process.env.KOHAMI_PUBLIC_DEMO === 'true'
export default defineNuxtConfig({
  ssr: true,
  buildDir: process.env.NUXT_BUILD_DIR || '.nuxt',
  devtools: { enabled: false },
  compatibilityDate: '2026-10-13',
  devServer: { host: 'localhost', port: Number(process.env.NUXT_PORT || 3000), https: cert && key ? { cert, key } : undefined },
  runtimeConfig: { apiOrigin: process.env.NUXT_API_ORIGIN || 'http://127.0.0.1:5080', siteOrigin: process.env.NUXT_SITE_ORIGIN || 'https://localhost:3000', public: { mode: 'mock', siteUrl: process.env.NUXT_PUBLIC_SITE_URL || '', localDemo: process.env.NUXT_PUBLIC_LOCAL_DEMO === 'true', backendAvailable: !publicDemo } },
  nitro: publicDemo ? { prerender: { routes: ['zh-TW', 'ja', 'en'].flatMap(locale => [`/${locale}`, ...demoProductSlugs.map(slug => `/${locale}/products/${slug}`)]), crawlLinks: true } } : {},
  app: { head: { htmlAttrs: { lang: 'zh-TW' }, meta: [{ name: 'robots', content: 'noindex,nofollow' }], link: [{ rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }] } },
  routeRules: { '/**': { headers: { 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' } } },
  hooks: {
    'pages:extend': (pages) => {
      if (publicDemo) {
        const publicPaths = new Set(['/', '/products/:slug()', '/products/:slug', '/policies', '/support', '/account'])
        for (let index = pages.length - 1; index >= 0; index--) {
          if (!publicPaths.has(pages[index]!.path)) pages.splice(index, 1)
        }
      }
      const localizedPages = pages
        .filter(page => page.file && !page.path.startsWith('/admin'))
        .flatMap(page => ['zh-TW', 'ja', 'en'].map(locale => ({
          ...page,
          name: `${page.name}-${locale}`,
          path: `/${locale}${page.path === '/' ? '' : page.path}`,
          meta: { ...page.meta, locale },
        })))
      pages.push(...localizedPages)
    },
    'ready': () => { if (process.argv.includes('dev') && (!cert || !key)) throw new Error('Foundation_HTTPS_CERT_AND_KEY_REQUIRED') }
  }
})
