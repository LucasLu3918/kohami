import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'

test.use({ trace: 'off', screenshot: 'off', video: 'off' })

const autoplayIntervalMs = 6000

async function activeSlideIndex(page: Page) {
  return page.locator('.storefront-hero__dot').evaluateAll(dots => dots.findIndex(dot => dot.classList.contains('is-active')))
}

async function openHero(page: Page) {
  await page.goto('/zh-TW')
  // Wait for hydration so the carousel's pointer and focus listeners are attached before interacting.
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(300)
}

test('hero banner autoplay advances to the next slide every 6 seconds', async ({ page }) => {
  await page.goto('/zh-TW')
  await expect(page.locator('.storefront-hero__dot').first()).toBeVisible()
  await page.mouse.move(0, 0)

  await page.evaluate(() => {
    const activeIndex = () => [...document.querySelectorAll('.storefront-hero__dot')].findIndex(dot => dot.classList.contains('is-active'))
    const transitions: number[] = []
    const observedAt = performance.now()
    let last = activeIndex()
    const watch = () => {
      const current = activeIndex()
      if (current !== last) {
        transitions.push(performance.now())
        last = current
      }
      requestAnimationFrame(watch)
    }
    ;(window as unknown as { __heroTransitions: { observedAt: number; at: number[] } }).__heroTransitions = { observedAt, at: transitions }
    requestAnimationFrame(watch)
  })

  await expect.poll(() => page.evaluate(() => (window as unknown as { __heroTransitions: { at: number[] } }).__heroTransitions.at.length), { timeout: 3 * autoplayIntervalMs + 4000 }).toBeGreaterThanOrEqual(3)

  const { observedAt, at } = await page.evaluate(() => (window as unknown as { __heroTransitions: { observedAt: number; at: number[] } }).__heroTransitions)
  expect(at[0]! - observedAt).toBeLessThan(autoplayIntervalMs + 600)
  for (let index = 1; index < at.length; index += 1) {
    const gap = at[index]! - at[index - 1]!
    expect(gap, `gap before transition ${index + 1}`).toBeGreaterThan(autoplayIntervalMs - 300)
    expect(gap, `gap before transition ${index + 1}`).toBeLessThan(autoplayIntervalMs + 300)
  }
})

test('hovering the hero banner pauses autoplay', async ({ page }) => {
  await openHero(page)
  const visual = page.locator('.storefront-hero__visual')
  await expect(visual).toBeVisible()
  await visual.hover()
  await page.waitForTimeout(autoplayIntervalMs + 800)
  expect(await activeSlideIndex(page)).toBe(0)
})

test('the progress line runs for the same 6 seconds as the autoplay interval', async ({ page }) => {
  await openHero(page)
  const progress = page.locator('.storefront-hero__progress')
  const running = await progress.evaluate(element => {
    const [animation] = element.getAnimations()
    return { duration: animation?.effect?.getComputedTiming().duration, playState: animation?.playState }
  })
  expect(running.duration).toBe(autoplayIntervalMs)
  expect(running.playState).toBe('running')
})

test('hovering a banner dot keeps it a round indicator without a filled square', async ({ page }) => {
  await openHero(page)
  const dot = page.locator('.storefront-hero__dot').nth(3)
  await dot.hover()
  await page.waitForTimeout(400)
  const styles = await dot.evaluate(element => ({
    background: getComputedStyle(element).backgroundColor,
    indicatorRadius: getComputedStyle(element, '::before').borderRadius,
  }))
  expect(styles.background).toBe('rgba(0, 0, 0, 0)')
  expect(styles.indicatorRadius).toBe('999px')
})

test('a mouse click on a banner control does not stop autoplay', async ({ page }) => {
  await openHero(page)
  await expect(page.locator('.storefront-hero__dot').first()).toBeVisible()
  await page.locator('button[aria-label="下一張主視覺"]').click()
  await page.mouse.move(0, 0)
  const afterClick = await activeSlideIndex(page)
  await expect.poll(() => activeSlideIndex(page), { timeout: autoplayIntervalMs + 1500 }).not.toBe(afterClick)
})
