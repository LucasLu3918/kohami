import { defineConfig, devices } from '@playwright/test'

const publicBuild = process.env.DATA_SOURCE_PUBLIC_TEST === 'true'
export default defineConfig({
  testDir: './tests', testMatch: 'data-source.spec.ts', workers: 1, fullyParallel: false,
  reporter: 'list', outputDir: process.env.DATA_SOURCE_TEST_OUTPUT || '../../.local/data-source-tests',
  use: { baseURL: 'http://127.0.0.1:3197', channel: 'chrome', trace: 'off' },
  projects: [{ name: 'desktop', use: devices['Desktop Chrome'] }, { name: 'mobile', use: { ...devices['iPhone 13'], defaultBrowserType: 'chromium' } }],
  webServer: publicBuild
    ? { command: 'npx wrangler dev --config wrangler.public-demo.json --ip 127.0.0.1 --port 3197', url: 'http://127.0.0.1:3197/zh-TW/', reuseExistingServer: false }
    : [
        { command: 'node tests/fixtures/data-source-backend.mjs', url: 'http://127.0.0.1:5197/__counts', reuseExistingServer: false },
        { command: 'node .output/server/index.mjs', url: 'http://127.0.0.1:3197/zh-TW', reuseExistingServer: false, env: { NITRO_HOST: '127.0.0.1', NITRO_PORT: '3197', NUXT_SITE_ORIGIN: 'http://127.0.0.1:3197', NUXT_API_ORIGIN: 'http://127.0.0.1:5197' } },
      ],
})
