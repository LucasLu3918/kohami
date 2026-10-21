import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  testMatch: 'issue11-cart.spec.ts',
  fullyParallel: false,
  workers: 1,
  reporter: 'line',
  outputDir: process.env.KOHAMI_ISSUE11_PLAYWRIGHT_OUTPUT || '../../.local/issue11-playwright-artifacts',
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3000',
    ignoreHTTPSErrors: true,
    trace: 'off',
  },
  timeout: 60_000,
  expect: { timeout: 15_000 },
})
