import { expect, test } from '@playwright/test'

const routes = [
  '/zh-TW',
  '/zh-TW/cart',
  '/zh-TW/policies',
  '/zh-TW/support',
  '/zh-TW/account',
]

const localizedRoutes = ['/', '/cart', '/policies', '/support', '/account']
const checkoutId = 'checkout-issue24-focus-sweep'
const expiresAt = new Date(Date.now() + 15 * 60_000).toISOString()
const product = {
  slug: 'focus-sweep-demo-shirt',
  skuId: 'sku_demo_02',
  colorId: 'oatmeal',
  garmentSize: 'M',
  priceTwd: 1000,
  availability: 'InStock',
  translations: [
    { locale: 'ja', name: 'フォーカス確認用商品' },
    { locale: 'en', name: 'Focus check demo item' },
  ],
  media: [{ media: { url: '/demo-assets/neutral.svg', alt: { value: 'Synthetic demo image' } } }],
}

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
    await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
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
      const expectedControl = await control.evaluate(element => element.outerHTML.slice(0, 120))
      const activeControl = await page.evaluate(() => (document.activeElement as HTMLElement | null)?.outerHTML.slice(0, 120) || 'no active element')
      await expect(control, `${route}: expected ${expectedControl}; active ${activeControl}`).toBeFocused()
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

test('Issue24 Japanese and English routes keep keyboard focus in order with localized content', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.route('**/api/v1/**', route => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-focus-locales' } })
    if (path.endsWith('/carts/current')) return route.fulfill({ json: { id: 'issue24-focus-sweep-cart', version: 'cart-v1', items: [] } })
    if (path.endsWith('/products')) return route.fulfill({ json: { items: [], nextCursor: null } })
    return route.fulfill({ status: 404, json: { code: 'RESOURCE_NOT_FOUND' } })
  })

  for (const locale of ['ja', 'en'] as const) {
    for (const path of localizedRoutes) {
      await page.goto(`/${locale}${path}`)
      await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
      await expect(page.locator('#main-content')).toBeVisible()
      await sweepKeyboardFocus(page, locale === 'ja' ? 'メインコンテンツへスキップ' : 'Skip to main content', `/${locale}${path}`)
    }
  }
})

test('Issue24 populated cart and checkout retry error retain complete keyboard focus order in Japanese and English', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 })
  let failCheckoutRefresh = false
  let failQuantityUpdate = false
  let failQuoteRequest = false
  let cartQuantity = 2
  let cartVersion = 'cart-focus-v1'
  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path.endsWith('/auth/session')) return route.fulfill({ json: { state: 'Anonymous', mode: 'mock' } })
    if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { requestToken: 'synthetic-issue24-focus-dynamic' } })
    if (path.endsWith('/carts/current')) {
      return route.fulfill({ json: {
        id: 'issue24-focus-sweep-cart',
        version: cartVersion,
        items: [{ skuId: product.skuId, quantity: cartQuantity, unitPriceTwd: product.priceTwd, availability: 'InStock' }],
      } })
    }
    if (path === `/api/v1/carts/current/items/${product.skuId}` && request.method() === 'PUT') {
      if (failQuantityUpdate) {
        failQuantityUpdate = false
        return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      }
      cartQuantity = Number(request.postDataJSON().quantity)
      cartVersion = 'cart-focus-v2'
      return route.fulfill({ json: {
        id: 'issue24-focus-sweep-cart',
        version: cartVersion,
        items: [{ skuId: product.skuId, quantity: cartQuantity, unitPriceTwd: product.priceTwd, availability: 'InStock' }],
      } })
    }
    if (path === '/api/v1/checkout-quotes' && request.method() === 'POST') {
      if (failQuoteRequest) {
        failQuoteRequest = false
        return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      }
      return route.fulfill({ json: {
        id: `quote-issue24-focus-${cartQuantity}`,
        mode: 'mock',
        expiresAt,
        snapshot: { currency: 'TWD', merchandiseTwd: cartQuantity * product.priceTwd, discountTwd: 0, shippingTwd: 60, totalTwd: cartQuantity * product.priceTwd + 60 },
      } })
    }
    if (path.endsWith('/products')) return route.fulfill({ json: { items: [product], nextCursor: null } })
    if (path === `/api/v1/checkouts/${checkoutId}/status`) {
      if (failCheckoutRefresh) return route.fulfill({ status: 503, json: { code: 'SERVICE_UNAVAILABLE' } })
      return route.fulfill({ json: {
        checkoutId,
        mode: 'mock',
        version: 'checkout-focus-v1',
        orderState: 'AwaitingPayment',
        paymentState: 'Created',
        compensationRequired: false,
        expiresAt,
        snapshot: {
          merchandiseTwd: 2000,
          discountTwd: 0,
          shippingTwd: 60,
          totalTwd: 2060,
          lines: [{ skuId: product.skuId, quantity: 2, unitPriceTwd: product.priceTwd }],
        },
      } })
    }
    return route.fulfill({ status: 404, json: { code: 'UNEXPECTED_TEST_REQUEST', path } })
  })

  for (const locale of ['ja', 'en'] as const) {
    failCheckoutRefresh = false
    failQuantityUpdate = true
    failQuoteRequest = true
    cartQuantity = 2
    cartVersion = 'cart-focus-v1'
    const skipLabel = locale === 'ja' ? 'メインコンテンツへスキップ' : 'Skip to main content'
    await page.goto(`/${locale}/cart`)
    await expect(page.getByRole('heading', { name: product.translations.find(item => item.locale === locale)!.name })).toBeVisible()
    await expect(page.getByRole('spinbutton')).toBeVisible()
    await sweepKeyboardFocus(page, skipLabel, `/${locale}/cart populated state`)

    const quantity = page.getByRole('spinbutton')
    await quantity.focus()
    await quantity.fill('3')
    const failedQuantity = page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/v1/carts/current/items/${product.skuId}` && response.request().method() === 'PUT' && response.status() === 503,
    )
    await quantity.press('Tab')
    await failedQuantity
    await expect(page.getByRole('alert')).toContainText(locale === 'ja' ? 'ローカルサービスに接続できません' : 'The local service is unavailable')
    await expect(quantity).toHaveValue('2')
    await expect(quantity, `/${locale}/cart: focus returns to quantity after its failed update`).toBeFocused()

    await quantity.focus()
    await quantity.fill('3')
    const successfulQuantity = page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/v1/carts/current/items/${product.skuId}` && response.request().method() === 'PUT' && response.status() === 200,
    )
    await quantity.press('Tab')
    await successfulQuantity
    await expect(quantity).toHaveValue('3')
    await expect(quantity, `/${locale}/cart: focus returns to quantity after its successful retry`).toBeFocused()

    const quoteButton = page.getByRole('button', { name: locale === 'ja' ? '費用を再計算' : 'Recalculate fees' })
    await quoteButton.focus()
    const failedQuote = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/v1/checkout-quotes' && response.request().method() === 'POST' && response.status() === 503,
    )
    await page.keyboard.press('Enter')
    await failedQuote
    await expect(page.getByRole('alert')).toContainText(locale === 'ja' ? 'ローカルサービスに接続できません' : 'The local service is unavailable')
    await expect(quoteButton, `/${locale}/cart: quote failure retains focus on the retry control`).toBeFocused()

    const successfulQuote = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/v1/checkout-quotes' && response.request().method() === 'POST' && response.status() === 200,
    )
    await page.keyboard.press('Enter')
    await successfulQuote
    await expect(page.getByRole('region', { name: locale === 'ja' ? '費用明細' : 'Fee summary' })).toBeVisible()
    await expect(page.getByText(`TWD ${3 * product.priceTwd + 60}`)).toBeVisible()
    await expect(quoteButton, `/${locale}/cart: successful quote retry leaves focus on its trigger`).toBeFocused()

    await page.goto(`/${locale}`)
    await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
    const checkoutHeading = locale === 'ja' ? '模擬チェックアウト' : 'Mock checkout'
    const checkoutSection = page.locator('section').filter({ has: page.getByRole('heading', { name: checkoutHeading }) })
    await checkoutSection.getByLabel(`${checkoutHeading} ID`).fill(checkoutId)
    await checkoutSection.getByRole('button', { name: locale === 'ja' ? '模擬支払状態' : 'Mock payment status' }).click()
    await checkoutSection.getByRole('link', { name: locale === 'ja' ? 'チェックアウト概要を表示' : 'View checkout summary' }).click()
    await expect(page).toHaveURL(new RegExp(`/${locale}/checkout/${checkoutId}$`))
    await expect(page.getByRole('heading', { name: /checkout result|チェックアウト結果|結帳結果/i })).toBeVisible()
    await expect(page.getByText(checkoutId)).toBeVisible()
    await expect(page.getByText('TWD 2060')).toBeVisible()
    failCheckoutRefresh = true
    const refresh = page.getByRole('button', { name: locale === 'ja' ? '状態を再確認' : 'Refresh status' })
    await refresh.focus()
    const failedRefresh = page.waitForResponse(response =>
      new URL(response.url()).pathname === `/api/v1/checkouts/${checkoutId}/status` && response.status() === 503,
    )
    await page.keyboard.press('Enter')
    await failedRefresh
    await expect(page.getByText(locale === 'ja' ? '前回取得したサーバー結果を表示しています' : 'Showing the last server result')).toBeVisible()
    await expect(refresh).toBeFocused()
    await sweepKeyboardFocus(page, skipLabel, `/${locale}/checkout/${checkoutId} after retry failure`, true)
  }
})

async function sweepKeyboardFocus(page: import('@playwright/test').Page, skipLabel: string, context: string, startAtSkipLink = false) {
  const main = page.locator('#main-content')
  const skipLink = page.getByRole('link', { name: skipLabel })
  if (startAtSkipLink) {
    await skipLink.focus()
    await expect(skipLink, `${context}: skip navigation is focusable`).toBeFocused()
  } else {
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
    await page.keyboard.press('Tab')
    await expect(skipLink, `${context}: first Tab focuses skip navigation`).toBeFocused()
  }
  await page.keyboard.press('Enter')
  await expect(main, `${context}: skip navigation moves focus to main`).toBeFocused()

  const candidates = main.locator('a[href], button:not(:disabled), input:not([type="hidden"]):not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')
  const tabbable: typeof candidates[] = []
  for (let index = 0; index < await candidates.count(); index += 1) {
    const candidate = candidates.nth(index)
    if (!await candidate.isVisible()) continue
    const hiddenFromAccessibility = await candidate.evaluate(element => Boolean(element.closest('[aria-hidden="true"], [inert]')))
    if (!hiddenFromAccessibility) tabbable.push(candidate)
  }

  for (const control of tabbable) {
    await page.keyboard.press('Tab')
    const expectedControl = await control.evaluate(element => element.outerHTML.slice(0, 120))
    const activeControl = await page.evaluate(() => (document.activeElement as HTMLElement | null)?.outerHTML.slice(0, 120) || 'no active element')
    await expect(control, `${context}: expected ${expectedControl}; active ${activeControl}`).toBeFocused()
    const focusIndicatorVisible = await control.evaluate(element => {
      if (!element.matches(':focus-visible')) return false
      const style = getComputedStyle(element)
      const outlineVisible = style.outlineStyle !== 'none' && Number.parseFloat(style.outlineWidth) > 0
      return outlineVisible || style.boxShadow !== 'none'
    })
    expect(focusIndicatorVisible, `${context}: focused control has a visible indicator`).toBe(true)
  }
}
