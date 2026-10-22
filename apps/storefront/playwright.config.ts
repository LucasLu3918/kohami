import { defineConfig, devices } from '@playwright/test'
import { backendModeStorageState } from './tests/backend-mode'

export default defineConfig({
  testDir: './tests',
  testMatch: 'issue*.spec.ts',
  testIgnore: ['issue24-performance-layout-shift.spec.ts', 'issue24-localized-keyboard.spec.ts'],
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../../.local/foundation/issue01-playwright-report', open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3000',
    storageState: backendModeStorageState(process.env.KOHAMI_STOREFRONT_ORIGIN || 'https://localhost:3000'),
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
  },
  outputDir: '../../.local/foundation/issue01-playwright-artifacts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
})
