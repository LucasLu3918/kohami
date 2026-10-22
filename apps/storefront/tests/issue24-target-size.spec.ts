import { expect, test } from '@playwright/test'
import type { Locator } from '@playwright/test'

const locales = ['zh-TW', 'ja', 'en'] as const

async function expectMinimumTargetSize(locator: Locator, label: string) {
  const box = await locator.boundingBox()
  expect(box, `${label} should have a measurable target`).not.toBeNull()
  expect(box!.width, `${label} target width`).toBeGreaterThanOrEqual(24)
  expect(box!.height, `${label} target height`).toBeGreaterThanOrEqual(24)
}

for (const locale of locales) {
  test(`Issue24 ${locale} product and account navigation targets are at least 24px high`, async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 })
    await page.goto(`/${locale}/products/issue24-vitals-shirt`)
    await expect(page.getByRole('heading', { name: locale === 'ja' ? 'アクセシビリティ確認用シャツ' : locale === 'en' ? 'Accessibility demo shirt' : '無障礙驗收示意襯衫' })).toBeVisible()

    const localeLinks = page.getByRole('navigation', { name: 'Language' }).getByRole('link')
    await expect(localeLinks).toHaveCount(3)
    for (let index = 0; index < await localeLinks.count(); index += 1) {
      await expectMinimumTargetSize(localeLinks.nth(index), `${locale} product locale link ${index + 1}`)
    }
    const productBackLink = page.getByRole('link', { name: /返回商品目錄|商品一覧へ|Back to catalog/ })
    await expectMinimumTargetSize(productBackLink, `${locale} product back link`)

    await page.goto(`/${locale}/account`)
    await expect(page.getByRole('heading', { name: locale === 'ja' ? 'マイアカウント' : locale === 'en' ? 'My account' : '會員中心' })).toBeVisible()
    const accountLocaleLinks = page.getByRole('navigation', { name: 'Language' }).getByRole('link')
    await expect(accountLocaleLinks).toHaveCount(3)
    for (let index = 0; index < await accountLocaleLinks.count(); index += 1) {
      await expectMinimumTargetSize(accountLocaleLinks.nth(index), `${locale} account locale link ${index + 1}`)
    }
    const accountNavigation = page.getByRole('navigation', { name: '主要導覽' })
    for (const link of await accountNavigation.getByRole('link').all()) {
      await expectMinimumTargetSize(link, `${locale} account navigation link`)
    }

    await page.goto(`/${locale}/checkout/issue24-vitals`)
    await expect(page.getByRole('heading', { name: locale === 'ja' ? '決済結果' : locale === 'en' ? 'Checkout result' : '結帳結果' })).toBeVisible()
    await expect(page.getByRole('heading', { name: locale === 'ja' ? 'チェックアウト概要' : locale === 'en' ? 'Checkout summary' : '結帳摘要' })).toBeVisible()
  })
}
