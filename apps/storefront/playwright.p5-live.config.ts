import { defineConfig } from '@playwright/test'
import { randomUUID } from 'node:crypto'
import baseConfig from './playwright.config'

const artifactDirectory = process.env.KOHAMI_P5_ARTIFACT_DIR
  || `../../.local/m0/p5-wallet-browser-artifacts-${process.pid}-${randomUUID()}`

export default defineConfig(baseConfig, {
  testMatch: 'issue-p5-live-wallet.spec.ts',
  reporter: 'list',
  outputDir: artifactDirectory,
  use: {
    ...baseConfig.use,
    ignoreHTTPSErrors: true,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
    launchOptions: { args: ['--disable-breakpad', '--disable-crash-reporter'] },
  },
})
