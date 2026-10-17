import { expect, test } from '@playwright/test'

test('Issue24 skip link is the first keyboard stop and focuses localized main content', async ({ page }) => {
  const cases = [
    { path: '/zh-TW/policies', label: '略過導覽，前往主要內容' },
    { path: '/ja/policies', label: 'メインコンテンツへスキップ' },
    { path: '/en/policies', label: 'Skip to main content' },
  ]

  for (const item of cases) {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(item.path)
    await page.keyboard.press('Tab')
    const skipLink = page.getByRole('link', { name: item.label })
    await expect(skipLink).toBeFocused()
    await expect(skipLink).toBeInViewport()
    await page.keyboard.press('Enter')
    await expect(page.locator('#main-content')).toBeFocused()
    await expect(page).toHaveURL(/#main-content$/)
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  }
})
