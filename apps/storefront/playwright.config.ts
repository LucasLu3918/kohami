import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  testMatch: 'issue*.spec.ts',
  testIgnore: ['issue24-performance-layout-shift.spec.ts', 'issue24-localized-keyboard.spec.ts'],
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../../.local/m0/issue01-playwright-report', open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3000',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
  },
  outputDir: '../../.local/m0/issue01-playwright-artifacts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
})
