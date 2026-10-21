import { expect, test } from '@playwright/test'

test('Issue22 publishes versioned mock policies in three locales', async ({ page }) => {
  const cases = [
    { path: '/zh-TW/policies', title: '購物政策（展示版）', notice: '全流程模擬' },
    { path: '/ja/policies', title: 'ショッピングポリシー（デモ版）', notice: 'すべて模擬' },
    { path: '/en/policies', title: 'Shopping policies (demo)', notice: 'All flows are simulated' },
  ]

  for (const item of cases) {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(item.path)
    await expect(page.getByRole('heading', { level: 1, name: item.title })).toBeVisible()
    await expect(page.getByRole('note')).toContainText(item.notice)
    await expect(page.getByRole('heading', { level: 2, name: /shopping|購物|購入/i })).toBeVisible()
    await expect(page.getByRole('heading', { level: 2, name: /delivery|配送/i })).toBeVisible()
    await expect(page.getByRole('heading', { level: 2, name: /return|refund|退貨|退款|返品|返金/i })).toBeVisible()
    await expect(page.getByRole('heading', { level: 2, name: /data|資料|データ/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /support|客服|サポート/i })).toHaveAttribute('href', new RegExp(`/${item.path.split('/')[1]}/support$`))
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  }
})

test('Issue22 support entry explains mock-only scope and links to own aftercare cases', async ({ page }) => {
  const cases = [
    { path: '/zh-TW/support', title: '客服與案件入口（展示版）', action: '開啟我的訂單與售後', locale: 'zh-TW' },
    { path: '/ja/support', title: 'サポートと案件（デモ版）', action: '注文とアフターサービスを開く', locale: 'ja' },
    { path: '/en/support', title: 'Customer support (mock)', action: 'Open my order and aftercare', locale: 'en' },
  ]

  for (const item of cases) {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(item.path)
    await expect(page.getByRole('heading', { level: 1, name: item.title })).toBeVisible()
    await expect(page.getByRole('note')).toContainText(/business-day calendars and SLAs are not configured|營業日曆與 SLA 尚未設定|営業日カレンダーと SLA は未設定/)
    const aftercareLink = page.getByRole('link', { name: item.action })
    await expect(aftercareLink).toHaveAttribute('href', `/${item.locale}/#care-heading`)
    await expect(aftercareLink).toBeVisible()
    await aftercareLink.focus()
    await expect(aftercareLink).toBeFocused()
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  }
  await expect(page.getByText('General inquiries and data-rights requests cannot be submitted in this mock.')).toBeVisible()
})

test('Issue22 entry reaches owner-scoped order aftercare and gives safe lookup errors', async ({ page }) => {
  let returnCreated = false
  let returnAuthorized = false
  const order = () => ({
    id: 'issue22_owner_order', version: 'order-v1', orderState: 'Confirmed',
    snapshot: { lines: [{ orderLineId: 'issue22_line', skuId: 'demo-sku', quantity: 1 }] },
    shipmentSummary: { state: 'Delivered', shipments: [] },
    returns: returnCreated ? [{
      id: 'issue22_return_case', state: 'Requested', version: 'return-v1',
      caseSummary: { classification: 'Return', openedAt: '2026-10-17T00:00:00Z', firstResponseAt: null, firstResponseElapsedSeconds: null, escalatedAt: null, escalationElapsedSeconds: null },
      timeline: [{ state: 'Requested', at: '2026-10-17T00:00:00Z' }], inspectionSummary: null,
      lines: [{ orderLineId: 'issue22_line', requestedQuantity: 1, acceptedQuantity: 0, receivedUnitIds: [], restockedUnitIds: [], quarantinedUnitIds: [] }],
    }] : [],
  })

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname.replace('/api/v1', '')
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })

    if (path === '/auth/session') return json({ state: 'Anonymous', mode: 'mock' })
    if (path === '/auth/csrf') return json({ requestToken: 'synthetic-issue22-csrf' })
    if (path === '/products') return json({ items: [], nextCursor: null })
    if (path === '/carts/current') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === '/carts' && request.method() === 'POST') return json({ id: 'issue22_cart', version: 'cart-v1', items: [] }, 201)
    if (path === '/orders/hidden_order') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === '/orders/service_offline') return json({ code: 'SERVICE_UNAVAILABLE' }, 503)
    if (path === '/orders/issue22_owner_order' && request.method() === 'GET') return json(order())
    if (path === '/orders/issue22_owner_order/receipts') return json({ orderId: 'issue22_owner_order', taxEffect: 'none', receipts: [], adjustments: [] })
    if (path === '/orders/issue22_owner_order/returns' && request.method() === 'POST') {
      if (!returnAuthorized) return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
      returnCreated = true
      return json({ id: 'issue22_return_case', state: 'Requested', version: 'return-v1', quantity: 1, orderLineId: 'issue22_line' }, 201)
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/en/support')
  await page.getByRole('link', { name: 'Open my order and aftercare' }).click()
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  const aftercare = page.getByRole('region', { name: 'Order delivery and aftercare (mock)' })
  await expect(aftercare).toBeVisible()

  await aftercare.getByLabel('Order ID').fill('hidden_order')
  await aftercare.getByRole('button', { name: 'Load my order' }).click()
  await expect(aftercare.getByRole('alert')).toHaveText('This order is unavailable for the current identity_access. Check the account or order proof, then try again.')
  await expect(aftercare).not.toContainText('issue22_owner_order')

  await aftercare.getByLabel('Order ID').fill('service_offline')
  await aftercare.getByRole('button', { name: 'Load my order' }).click()
  await expect(aftercare.getByRole('alert')).toHaveText('The local service could not load this order. Try again later.')

  await aftercare.getByLabel('Order ID').fill('issue22_owner_order')
  await aftercare.getByRole('button', { name: 'Load my order' }).click()
  const returnForm = aftercare.locator('form').filter({ hasText: 'Order line ID' })
  await returnForm.getByLabel('Order line ID').fill('issue22_line')
  await returnForm.getByLabel('Reason', { exact: true }).fill('Synthetic Issue 22 return request')
  const deniedResponse = page.waitForResponse(response => response.url().endsWith('/orders/issue22_owner_order/returns') && response.request().method() === 'POST')
  await returnForm.getByRole('button', { name: 'Request a return' }).click()
  expect((await deniedResponse).status()).toBe(404)
  await expect(aftercare.getByRole('alert')).toHaveText('This order is unavailable for the current identity_access. Check the account or order proof, then try again.')
  await expect(returnForm.getByLabel('Reason', { exact: true })).toHaveValue('Synthetic Issue 22 return request')

  returnAuthorized = true
  const requestResponse = page.waitForResponse(response => response.url().endsWith('/orders/issue22_owner_order/returns') && response.request().method() === 'POST', { timeout: 10_000 })
  await returnForm.getByRole('button', { name: 'Request a return' }).click()
  expect((await requestResponse).status()).toBe(201)
  await expect(aftercare.getByRole('region', { name: 'Return case timeline' })).toContainText('Requested')
  await expect(aftercare.getByText('issue22_return_case', { exact: true })).toBeVisible()
})
