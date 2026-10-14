import { expect, test } from '@playwright/test'

test('shopper can follow mock delivery, return inspection, refund authorization and DEMO receipt notices', async ({ page }) => {
  const order = {
    id: 'demo_order_aftercare',
    version: 'order-v1',
    snapshot: { lines: [{ orderLineId: 'demo_line_01', skuId: 'sku_demo_01', quantity: 1, units: [{ unitId: 'demo_unit_01', paidTwd: 500 }] }] },
    shipmentSummary: { shipments: [] as Array<{ id: string; state: string; version: string; history: unknown[] }> },
    returns: [] as Array<Record<string, any>>,
    receiptSummary: {
      orderId: 'demo_order_aftercare', mode: 'mock', taxEffect: 'none',
      receipts: [{ id: 'DEMO-RECEIPT-01', kind: 'Receipt', state: 'Issued', amountTwd: 500, displayNotice: 'DEMO only; no tax effect' }],
      adjustments: [],
    },
  }
  let shipmentState = 'Pending'
  let returnState = ''
  let shipmentVersion = 0
  let returnVersion = 0
  let refundAmount = 0
  let refundShipping = true
  const calls: string[] = []

  await page.route('**/api/v1/**', async route => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname.replace('/api/v1', '')
    calls.push(`${request.method()} ${path}`)
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })

    if (path === '/auth/session') return json({ state: 'Authenticated', mode: 'mock' })
    if (path === '/auth/csrf') return json({ requestToken: 'synthetic-csrf' })
    if (path === '/products') return json([])
    if (path === '/carts/current') return json({ code: 'RESOURCE_NOT_FOUND' }, 404)
    if (path === '/carts' && request.method() === 'POST') return json({ id: 'demo_cart', version: 'cart-v1', items: [] }, 201)
    if (path === `/orders/${order.id}` && request.method() === 'GET') return json(order)
    if (path === `/orders/${order.id}/returns` && request.method() === 'POST') {
      returnState = 'Requested'
      returnVersion = 1
      order.returns = [{ id: 'demo_return_01', state: returnState, version: 'return-v1', quantity: 1, orderLineId: 'demo_line_01' }]
      return json(order.returns[0], 201)
    }
    if (path === `/admin/orders/${order.id}/shipments` && request.method() === 'POST') {
      shipmentState = 'Pending'
      shipmentVersion = 1
      order.shipmentSummary.shipments = [{ id: 'demo_shipment_01', state: shipmentState, version: 'shipment-v1', history: [{ state: 'Pending' }] }]
      return json(order.shipmentSummary.shipments[0], 201)
    }
    if (path === '/admin/shipments/demo_shipment_01/transitions' && request.method() === 'POST') {
      const body = request.postDataJSON() as { state: string }
      shipmentState = body.state
      shipmentVersion += 1
      const shipment = order.shipmentSummary.shipments[0]
      shipment.state = shipmentState
      shipment.version = `shipment-v${shipmentVersion}`
      shipment.history.push({ state: shipmentState })
      order.version = `order-shipment-v${shipmentVersion}`
      return json(shipment)
    }
    if (path === '/admin/returns/demo_return_01/transitions' && request.method() === 'POST') {
      const body = request.postDataJSON() as { state: string }
      returnState = body.state
      returnVersion += 1
      const ret = order.returns[0]
      ret.state = returnState
      ret.version = `return-v${returnVersion}`
      return json(ret)
    }
    if (path === '/admin/returns/demo_return_01/inspections' && request.method() === 'POST') {
      returnState = 'Inspected'
      returnVersion += 1
      order.returns[0] = {
        ...order.returns[0], state: returnState, version: `return-v${returnVersion}`,
        inspection: { refundCaseId: 'demo_refundcase_01', refundCaseVersion: 'refundcase-v1', refundUnits: [{ unitId: 'demo_unit_01', ordinal: 0, paidTwd: 500 }] },
      }
      return json(order.returns[0])
    }
    if (path === '/auth/account-context') return json({ requestNonce: 'account-nonce' })
    if (path === '/auth/reauthentication-challenges') return json({ challengeId: 'demo-challenge', requestNonce: 'challenge-nonce' }, 201)
    if (path === '/auth/reauthentications') return json({ state: 'Verified' }, 201)
    if (path === '/admin/refund-cases/demo_refundcase_01/operations') {
      const body = request.postDataJSON() as { amountTwd: number; refundShipping: boolean }
      refundAmount = body.amountTwd
      refundShipping = body.refundShipping
      return json({ id: 'demo_refund_01', state: 'Pending', amountTwd: 500, mode: 'mock' }, 201)
    }
    return json({ code: 'UNEXPECTED_TEST_REQUEST', path }, 500)
  })

  await page.goto('/')
  await page.waitForFunction(() => Boolean((document.querySelector('#__nuxt') as (HTMLElement & { __vue_app__?: unknown }) | null)?.__vue_app__))
  await expect(page.getByText('全流程模擬，沒有實際付款、退款、配送或有效發票。')).toBeVisible()
  const aftercare = page.getByRole('region', { name: '訂單配送與售後（模擬）' })
  await aftercare.getByLabel('訂單編號').fill(order.id)
  await aftercare.getByRole('button', { name: '查詢自己的訂單' }).click()
  await expect(aftercare.getByText(/DEMO 憑證僅供展示/)).toBeVisible()
  await expect(aftercare.getByText(/無稅務效果/)).toBeVisible()
  await expect(aftercare.getByText('DEMO-RECEIPT-01')).toBeVisible()

  await aftercare.getByRole('button', { name: '建立模擬配送' }).click()
  await aftercare.getByLabel('配送編號').fill('demo_shipment_01')
  const state = aftercare.getByLabel('下一狀態')
  for (const next of ['Shipped', 'InTransit', 'Delivered']) {
    await state.selectOption(next)
    await aftercare.getByRole('button', { name: '更新配送狀態' }).click()
  }
  await expect(aftercare.getByText(/demo_shipment_01: 已送達/)).toBeVisible()

  await aftercare.getByLabel('訂單明細編號').fill('demo_line_01')
  await aftercare.getByLabel('原因').first().fill('Synthetic acceptance return')
  await aftercare.getByRole('button', { name: '提出退貨申請' }).click()
  await aftercare.getByLabel('退貨編號').fill('demo_return_01')
  await aftercare.getByRole('button', { name: '受理退貨' }).click()
  await aftercare.getByRole('button', { name: '已收件' }).click()
  await aftercare.getByLabel('可再售數量').fill('1')
  await aftercare.getByLabel('原因').last().fill('Inspected synthetic item')
  await aftercare.getByRole('button', { name: '完成驗收' }).click()
  await aftercare.getByLabel('驗證器代碼').fill('123456')
  await aftercare.getByRole('button', { name: '執行模擬退款' }).click()
  await expect(page.getByText(/demo_refund_01/)).toBeVisible()
  expect(returnState).toBe('Inspected')
  expect(shipmentState).toBe('Delivered')
  expect(refundAmount).toBe(500)
  expect(refundShipping).toBe(false)
  expect(calls).toContain('POST /auth/reauthentications')
  expect(calls).toContain('POST /admin/refund-cases/demo_refundcase_01/operations')
})
