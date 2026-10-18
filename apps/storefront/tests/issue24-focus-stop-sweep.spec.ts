import { expect, test } from '@playwright/test'

const routes = [
  '/zh-TW',
  '/zh-TW/cart',
  '/zh-TW/policies',
  '/zh-TW/support',
  '/zh-TW/account',
]

test('Issue24 desktop core routes keep every visible control in keyboard order with a visible focus indicator', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-focus-sweep' } })
    if (path.endsWith('/carts/current')) return route.fulfill({ json: { id: 'issue24-focus-sweep-cart', version: 'cart-v1', items: [] } })
    if (path.endsWith('/products')) return route.fulfill({ json: { items: [], nextCursor: null } })
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  for (const route of routes) {
    await page.goto(route)
    const main = page.locator('#main-content')
    await expect(main).toBeVisible()

    await page.keyboard.press('Tab')
    const skipLink = page.getByRole('link', { name: '略過導覽，前往主要內容' })
    await expect(skipLink).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(main).toBeFocused()

    const allCandidates = main.locator('a[href], button:not(:disabled), input:not([type="hidden"]):not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')
    const tabbable: typeof allCandidates[] = []
    for (let index = 0; index < await allCandidates.count(); index += 1) {
      const candidate = allCandidates.nth(index)
      if (!await candidate.isVisible()) continue
      const hiddenFromAccessibility = await candidate.evaluate(element => Boolean(element.closest('[aria-hidden="true"], [inert]')))
      if (!hiddenFromAccessibility) tabbable.push(candidate)
    }

    for (const control of tabbable) {
      await page.keyboard.press('Tab')
      await expect(control, `${route}: ${await control.evaluate(element => element.outerHTML.slice(0, 120))}`).toBeFocused()
      const focusIndicatorVisible = await control.evaluate(element => {
        if (!element.matches(':focus-visible')) return false
        const style = getComputedStyle(element)
        const outlineVisible = style.outlineStyle !== 'none' && Number.parseFloat(style.outlineWidth) > 0
        return outlineVisible || style.boxShadow !== 'none'
      })
      expect(focusIndicatorVisible, `${route}: focused control has a visible indicator`).toBe(true)
    }
  }
})
