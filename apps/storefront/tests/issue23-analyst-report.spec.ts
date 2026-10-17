import { expect, test } from '@playwright/test'

test('Issue23 Analyst report displays only bounded aggregate rows and UTC mock semantics', async ({ page }) => {
  let requestedUrl = ''
  await page.route('**/api/v1/admin/reports**', async route => {
    requestedUrl = route.request().url()
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        mode: 'mock',
        cutoff: '2026-10-18T00:00:00Z',
        notice: '合成聚合示意，非正式財務報表',
        rows: [{ date: '2026-10-12', sourceKind: 'Primary', orderCount: 2, collectedTwd: 2000, succeededRefundTwd: 100, pendingTwd: 50, availableTwd: 1850, netConfirmedTwd: 1900, discrepancyCount: 0 }],
      }),
    })
  })

  await page.goto('/admin/reports')
  await expect(page.getByRole('heading', { name: '本機營運摘要' })).toBeVisible()
  await expect(page.getByRole('note')).toContainText('日期依 UTC 訂單建立日分組')
  await expect(page.getByRole('note')).toContainText('不含個人資料或訂單明細')
  await expect(page.getByRole('table')).toContainText('2026-10-12')
  await expect(page.getByRole('table')).toContainText('2000')
  expect(requestedUrl).toMatch(/from=\d{4}-\d{2}-\d{2}&to=\d{4}-\d{2}-\d{2}/)
  await expect(page.getByRole('button', { name: '查詢摘要' })).toBeEnabled()
})

test('Issue23 Analyst report shows a safe authorization error', async ({ page }) => {
  await page.route('**/api/v1/admin/reports**', route => route.fulfill({
    status: 403,
    contentType: 'application/problem+json',
    body: JSON.stringify({ code: 'FORBIDDEN', status: 403 }),
  }))
  await page.goto('/admin/reports')
  await expect(page.getByRole('alert')).toContainText('目前帳號沒有此報表的讀取權限')
  await expect(page.getByText(/order_|@example\.test/)).toHaveCount(0)
})
