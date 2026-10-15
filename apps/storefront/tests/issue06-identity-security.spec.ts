import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

test('Issue06 Identity logout rejects replay and clears SSR persona on back navigation', async ({ page }) => {
  test.setTimeout(90_000)
  const artifactPath = process.env.KOHAMI_ISSUE06_SESSION_ARTIFACT
  if (!artifactPath) throw new Error('ISSUE06_IDENTITY_SESSION_ARTIFACT_MISSING')
  const artifact = JSON.parse(readFileSync(artifactPath, 'utf8')) as { sessionCookie: string; browserCookie: string }
  await page.context().addCookies([
    { name: 'kohami.session', value: artifact.sessionCookie, url: 'https://localhost:3000' },
    { name: 'kohami.browser', value: artifact.browserCookie, url: 'https://localhost:3000' },
  ])

  const authenticatedPage = await page.goto('/?identity=authenticated')
  expect(authenticatedPage?.headers()['cache-control']).toContain('no-store')
  const authenticated = await page.evaluate(async () => fetch('/api/v1/auth/session').then(response => response.json())) as { state: string; userId?: string | null }
  expect(authenticated.state).toBe('AdminAuthenticated')
  expect(authenticated.userId).toBeTruthy()
  const priorSessionCookie = (await page.context().cookies('https://localhost:3000')).find(cookie => cookie.name === 'kohami.session')
  expect(priorSessionCookie).toBeTruthy()

  const logoutResponse = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/auth/session' && response.request().method() === 'DELETE')
  await page.getByRole('button', { name: '登出' }).click()
  expect((await logoutResponse).status()).toBe(204)
  await expect.poll(async () => page.evaluate(async () => fetch('/api/v1/auth/session').then(response => response.json().then(body => body.state)))).toBe('Anonymous')

  const replay = await page.context().request.get('http://127.0.0.1:5080/api/v1/auth/session', {
    headers: { Cookie: `kohami.session=${priorSessionCookie!.value}`, 'X-Forwarded-Proto': 'https' },
  })
  expect(replay.status()).toBe(200)
  expect((await replay.json()).state).toBe('Anonymous')

  const anonymousPage = await page.goto('/?identity=anonymous')
  expect(anonymousPage?.headers()['cache-control']).toContain('no-store')
  expect(await page.content()).not.toContain(authenticated.userId!)
  await page.goBack()
  await expect.poll(async () => page.locator('#identity-heading + pre').textContent()).toContain('Anonymous')
  expect(await page.content()).not.toContain(authenticated.userId!)
})
