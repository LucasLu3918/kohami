import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  testMatch: 'issue-demo-p4-admin-http.spec.ts',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3004',
    ignoreHTTPSErrors: false,
    trace: 'retain-on-failure',
  },
  outputDir: '../../.local/kohami-demo-admin-frontend/playwright-artifacts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
})
