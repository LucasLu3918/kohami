import { expect, test, type Page } from '@playwright/test'
import { chmod, writeFile } from 'node:fs/promises'

type Evidence = {
  userId: string
  email: string
  skuId: string
  cartId: string
  quoteId: string
  checkoutId: string
  orderId: string
  totalTwd: number
}

const required = (name: string) => {
  const value = process.env[name]
  if (!value) throw new Error(`P5_ACCEPTANCE_ENV_REQUIRED:${name}`)
  return value
}

test('synthetic member signs in, pays from wallet, and receives the confirmed real API order', async ({ page }) => {
  const email = required('KOHAMI_P5_MEMBER_EMAIL')
  const password = required('KOHAMI_P5_MEMBER_PASSWORD')
  const userId = required('KOHAMI_P5_MEMBER_ID')
  const skuId = required('KOHAMI_P5_SKU')
  const evidencePath = required('KOHAMI_P5_EVIDENCE_PATH')

  let cartId = ''
  let quoteId = ''
  let checkoutId = ''
  let orderId = ''
  let totalTwd = 0
  const cdpRouteEvents: Array<{ url: string; navigationType?: string }> = []
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('Page.enable')
  cdp.on('Page.navigatedWithinDocument', event => {
    cdpRouteEvents.push({ url: new URL(event.url).pathname, navigationType: event.navigationType })
  })
  cdp.on('Page.frameNavigated', event => {
    if (event.frame.parentId) return
    cdpRouteEvents.push({ url: new URL(event.frame.url).pathname, navigationType: 'document' })
  })
  let urlAtExpectedReturn = ''
  const memberSessionStates: string[] = []
  const authResponses: Array<{ path: string; status: number; code?: string }> = []
  const pageErrorNames: string[] = []
  const documentNavigations: string[] = []
  await page.addInitScript(() => {
    const routeHistory = [window.location.pathname]
    const routeSamples: Array<{ elapsedMs: number; path: string; heading: string | null }> = []
    Object.defineProperty(window, '__p5RouteHistory', { value: routeHistory })
    Object.defineProperty(window, '__p5RouteSamples', { value: routeSamples })
    const sampleRoute = () => {
      if (routeSamples.length >= 120) return
      routeSamples.push({
        elapsedMs: Math.round(performance.now()),
        path: window.location.pathname,
        heading: document.querySelector('main h1')?.textContent?.trim() ?? null,
      })
    }
    window.setInterval(sampleRoute, 50)
    for (const method of ['pushState', 'replaceState'] as const) {
      const original = window.history[method].bind(window.history)
      window.history[method] = ((...args: Parameters<History[typeof method]>) => {
        const result = original(...args)
        routeHistory.push(window.location.pathname)
        return result
      }) as History[typeof method]
    }
    window.addEventListener('popstate', () => routeHistory.push(window.location.pathname))
  })
  page.on('pageerror', error => pageErrorNames.push(error.name))
  page.on('request', request => {
    if (request.isNavigationRequest()) documentNavigations.push(new URL(request.url()).pathname)
  })
  page.on('response', async response => {
    if (!response.url().includes('/api/v1/')) return
    const path = new URL(response.url()).pathname
    const authOutcome = /^\/api\/v1\/auth\/(?:csrf|sessions|session)$/.test(path)
      ? { path, status: response.status() }
      : null
    if (authOutcome) authResponses.push(authOutcome)
    try {
      const body = await response.json() as Record<string, unknown>
      if (authOutcome && typeof body.code === 'string') authOutcome.code = body.code
      if (response.url().endsWith('/api/v1/auth/session') && response.request().method() === 'GET') {
        memberSessionStates.push(String(body.state || 'unknown'))
      }
      if (response.url().endsWith('/api/v1/carts/current') && response.request().method() === 'GET') {
        cartId = String(body.id || '')
      }
      if (response.url().endsWith('/api/v1/checkout-quotes') && response.request().method() === 'POST') {
        quoteId = String(body.id || '')
        totalTwd = Number((body.snapshot as { totalTwd?: number } | undefined)?.totalTwd || 0)
      }
      if (response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST' && response.status() === 201) {
        checkoutId = String(body.checkoutId || '')
        orderId = String(body.orderId || '')
      }
    } catch {
      // Non-JSON responses do not contribute evidence.
    }
  })

  const initialSession = page.waitForResponse(response => response.url().endsWith('/api/v1/auth/session') && response.request().method() === 'GET')
  await page.goto('/en/account?returnTo=%2Fen%2Fcart')
  await initialSession
  await page.waitForLoadState('networkidle')
  await page.evaluate(() => {
    const root = document.getElementById('__nuxt') as any
    const router = root?.__vue_app__?.config?.globalProperties?.$router
    const events: Array<{ kind: string; to?: string; from?: string; failure?: string; error?: string }> = []
    Object.defineProperty(window, '__p5RouterEvents', { value: events })
    Object.defineProperty(window, '__p5RouterAvailable', { value: Boolean(router) })
    router?.afterEach?.((to: { fullPath: string }, from: { fullPath: string }, failure?: { type?: unknown }) => events.push({ kind: 'afterEach', to: to.fullPath.split('?')[0], from: from.fullPath.split('?')[0], failure: failure ? String(failure.type) : undefined }))
    router?.onError?.((error: Error) => events.push({ kind: 'error', error: error.name }))
    if (router?.replace) {
      const originalReplace = router.replace.bind(router)
      router.replace = async (target: string | { path?: string }) => {
        const from = router.currentRoute.value.fullPath.split('?')[0]
        const to = typeof target === 'string' ? target.split('?')[0] : target.path?.split('?')[0] || ''
        events.push({ kind: 'replace-start', from, to })
        const failure = await originalReplace(target)
        events.push({ kind: 'replace-return', from, to: router.currentRoute.value.fullPath.split('?')[0], failure: failure ? String(failure.type) : undefined })
        return failure
      }
    }
  })
  const emailInput = page.getByLabel('Email', { exact: true })
  const passwordInput = page.getByLabel('Password', { exact: true })
  const signIn = page.getByRole('button', { name: 'Sign in' })
  await emailInput.fill(email)
  await passwordInput.fill(password)
  await expect(signIn).toBeEnabled()
  await signIn.click()
  try {
    await expect.poll(() => new URL(page.url()).pathname).toBe('/en/cart')
    urlAtExpectedReturn = page.url()
    await expect(page.locator('main h1')).toHaveText('Cart details')
  } catch {
    const routeState = await page.evaluate(() => ({
      path: window.location.pathname,
      mainCount: document.querySelectorAll('main').length,
      heading: document.querySelector('main h1')?.textContent?.trim() ?? null,
      routeHistory: (window as Window & { __p5RouteHistory?: string[] }).__p5RouteHistory ?? [],
      routeTransitions: (() => {
        const samples = (window as Window & { __p5RouteSamples?: Array<{ elapsedMs: number; path: string; heading: string | null }> }).__p5RouteSamples ?? []
        return samples.filter((sample, index) => index === 0 || sample.path !== samples[index - 1]?.path || sample.heading !== samples[index - 1]?.heading)
      })(),
      routerEvents: (window as Window & { __p5RouterEvents?: Array<{ kind: string; to?: string; from?: string; failure?: string; error?: string }> }).__p5RouterEvents ?? [],
      routerAvailable: (window as Window & { __p5RouterAvailable?: boolean }).__p5RouterAvailable ?? false,
    }))
    throw new Error(`P5_RETURN_ROUTE_RENDER_FAILED:${JSON.stringify({ ...routeState, mainText: await page.locator('main').innerText(), urlAtExpectedReturn, finalUrl: page.url(), memberSessionStates, authResponses, cdpRouteEvents, documentNavigations, pageErrorNames: pageErrorNames.slice(0, 5) })}`)
  }

  await page.goto('/en')
  const catalog = page.getByRole('region', { name: 'New arrivals' })
  await catalog.getByRole('searchbox', { name: 'Search product name, SKU or description' }).fill(skuId)
  await catalog.getByRole('button', { name: 'Search products', exact: true }).click()
  const product = catalog.getByRole('article').filter({ has: page.locator(`option[value="${skuId}"]`) })
  await expect(product).toHaveCount(1)
  await product.getByRole('combobox').selectOption(skuId)
  const cartUpdate = page.waitForResponse(response => response.url().includes(`/api/v1/carts/current/items/${encodeURIComponent(skuId)}`) && response.request().method() === 'PUT')
  await product.getByRole('button', { name: 'Add to cart' }).click()
  const updatedCart = await cartUpdate
  expect(updatedCart.status()).toBe(200)
  const updatedCartBody = await updatedCart.json() as { items?: Array<{ skuId?: string }> }
  expect(updatedCartBody.items?.some(item => item.skuId === skuId)).toBe(true)

  await page.goto('/en/cart')
  await expect(page.locator('.cart-line').filter({ hasText: skuId })).toHaveCount(1)
  await page.getByRole('button', { name: 'Recalculate fees' }).click()
  await expect(page.getByRole('region', { name: 'Fee summary' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Member wallet payment' })).toBeVisible()
  await expect(page.getByText(/Available member balance: TWD/)).toBeVisible()
  await page.getByLabel('Contact email').fill(email)
  await page.getByRole('checkbox', { name: /I confirm this quote and authorize the synthetic member wallet/ }).check()
  const checkoutResponse = page.waitForResponse(response => response.url().endsWith('/api/v1/checkouts') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Create wallet order and pay' }).click()
  expect((await checkoutResponse).status()).toBe(201)

  await expect(page).toHaveURL(/\/en\/checkout\/[^/]+$/)
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  await expect(page.getByText('Confirmed', { exact: true })).toBeVisible()
  expect([cartId, quoteId, checkoutId, orderId].every(Boolean)).toBe(true)
  expect(totalTwd).toBeGreaterThan(0)

  const evidence: Evidence = { userId, email, skuId, cartId, quoteId, checkoutId, orderId, totalTwd }
  await writeFile(evidencePath, `${JSON.stringify(evidence)}\n`, { mode: 0o600 })
  await chmod(evidencePath, 0o600)
})
