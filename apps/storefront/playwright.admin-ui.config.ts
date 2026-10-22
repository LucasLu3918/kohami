import { defineConfig, devices } from '@playwright/test'
import { backendModeStorageState } from './tests/backend-mode'

export default defineConfig({
  testDir: './tests',
  testMatch: 'issue-demo-p4-admin.spec.ts',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    channel: 'chrome',
    baseURL: process.env.KOHAMI_STOREFRONT_ORIGIN || 'http://127.0.0.1:3008',
    storageState: backendModeStorageState(process.env.KOHAMI_STOREFRONT_ORIGIN || 'http://127.0.0.1:3008'),
    ignoreHTTPSErrors: false,
    trace: 'retain-on-failure',
  },
  outputDir: '../../.local/kohami-demo-admin-frontend/playwright-ui-artifacts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
})
