import { readFileSync } from 'node:fs'
const cert = process.env.M0_HTTPS_CERT
const key = process.env.M0_HTTPS_KEY
export default defineNuxtConfig({
  ssr: true,
  devtools: { enabled: false },
  compatibilityDate: '2026-10-13',
  devServer: { host: 'localhost', port: 3000, https: cert && key ? { cert: readFileSync(cert, 'utf8'), key: readFileSync(key, 'utf8') } : undefined },
  runtimeConfig: { apiOrigin: 'http://127.0.0.1:5080', public: { mode: 'mock' } },
  app: { head: { htmlAttrs: { lang: 'zh-TW' }, meta: [{ name: 'robots', content: 'noindex,nofollow' }] } },
  routeRules: { '/**': { headers: { 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' } } },
  hooks: { 'ready': () => { if (process.argv.includes('dev') && (!cert || !key)) throw new Error('M0_HTTPS_CERT_AND_KEY_REQUIRED') } }
})
