import { expect, test } from '@playwright/test'
import fixture from './fixtures/issue24-api-fixture.json' with { type: 'json' }

test('localized home pages keep catalog hydration layout shift within the Issue24 budget', async ({ page }) => {
  const product = fixture.product
  const hydrationWarnings: string[] = []
  page.on('console', message => {
    if (/hydration.*mismatch|hydration completed but contains mismatches/i.test(message.text())) hydrationWarnings.push(message.text())
  })

  await page.addInitScript(() => {
    window.__issue24LayoutShifts = 0
    new PerformanceObserver(list => {
      for (const entry of list.getEntries()) {
        if (!entry.hadRecentInput) window.__issue24LayoutShifts += entry.value
      }
    }).observe({ type: 'layout-shift', buffered: true })
  })

  for (const locale of ['zh-TW', 'ja', 'en']) {
    const serverRendered = await page.request.get(`/${locale}`)
    expect(serverRendered.ok()).toBe(true)
    expect(await serverRendered.text()).toContain(product.translations.find(item => item.locale === locale)!.name)
    await page.goto(`/${locale}`, { waitUntil: 'domcontentloaded' })
    const initialCartTop = await page.locator('#cart-heading').evaluate(element => element.parentElement!.getBoundingClientRect().top)
    await expect(page.locator('#catalog-heading')).toBeVisible()
    await expect(page.getByRole('heading', { name: product.translations.find(item => item.locale === locale)!.name })).toBeVisible()
    await page.waitForTimeout(500)
    const finalCartTop = await page.locator('#cart-heading').evaluate(element => element.parentElement!.getBoundingClientRect().top)
    const cls = await page.evaluate(() => window.__issue24LayoutShifts)
    expect(Math.abs(finalCartTop - initialCartTop), `${locale} cart section moved during catalog hydration`).toBeLessThanOrEqual(100)
    expect(cls, `${locale} home CLS`).toBeLessThanOrEqual(0.1)
  }
  expect(hydrationWarnings).toEqual([])
})

declare global {
  interface Window { __issue24LayoutShifts: number }
}
