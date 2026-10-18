import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from '@playwright/test'
import baseConfig from './playwright.config'

const storefrontRoot = fileURLToPath(new URL('.', import.meta.url))
const repositoryRoot = resolve(storefrontRoot, '../..')

export default defineConfig({
  ...baseConfig,
  testMatch: ['issue24-performance-layout-shift.spec.ts', 'issue24-localized-keyboard.spec.ts'],
  testIgnore: [],
  webServer: [
    {
      command: 'node tests/fixtures/issue24-api-server.mjs',
      cwd: storefrontRoot,
      url: 'http://127.0.0.1:5080/healthz',
      reuseExistingServer: false,
      timeout: 10_000,
    },
    {
      command: 'npm run dev',
      cwd: storefrontRoot,
      url: 'https://localhost:3000/zh-TW',
      reuseExistingServer: true,
      timeout: 120_000,
      ignoreHTTPSErrors: true,
      env: {
        M0_HTTPS_CERT: process.env.M0_HTTPS_CERT || resolve(repositoryRoot, '.local/m0/browser/public.pem'),
        M0_HTTPS_KEY: process.env.M0_HTTPS_KEY || resolve(repositoryRoot, '.local/m0/browser/private.key'),
      },
    },
  ],
})
