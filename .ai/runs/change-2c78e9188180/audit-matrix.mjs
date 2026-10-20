import { readFileSync } from 'node:fs'

const input = new URL('../issue24-core-route-matrix-20261020/metrics-merged.json', import.meta.url)
const report = JSON.parse(readFileSync(input, 'utf8'))
const locales = ['zh-TW', 'ja', 'en']
const routes = ['home-search-list', 'product-detail', 'cart-checkout-form', 'checkout-result']
const caches = ['cold', 'warm']
const nearestRank = values => [...values].sort((a, b) => a - b)[Math.ceil(values.length * 0.75) - 1]
const strata = []
const issues = []

for (const locale of locales) {
  for (const route of routes) {
    for (const cache of caches) {
      const samples = report.raw.filter(item => item.locale === locale && item.route === route && item.cache === cache)
      const lcp = samples.map(item => item.lcpMs).filter(Number.isFinite)
      const cls = samples.map(item => item.cls).filter(Number.isFinite)
      if (samples.length !== 30 || lcp.length !== 30 || cls.length !== 30) {
        issues.push(`${locale}/${route}/${cache}: unexpected sample count`)
      }
      if (samples.some(item => item.status !== 200 || item.pageErrors?.length)) {
        issues.push(`${locale}/${route}/${cache}: HTTP or page error`)
      }
      strata.push({ locale, route, cache, sampleCount: samples.length, lcpP75Ms: nearestRank(lcp), clsP75: nearestRank(cls) })
    }
  }
}

const warmups = report.raw.filter(item => item.cache === 'warmup').length
const summary = {
  strata: strata.length,
  scoredNavigations: strata.reduce((total, item) => total + item.sampleCount, 0),
  warmups,
  failures: report.failures.length,
  issues,
  lcpP75MsRange: [Math.min(...strata.map(item => item.lcpP75Ms)), Math.max(...strata.map(item => item.lcpP75Ms))],
  clsP75Range: [Math.min(...strata.map(item => item.clsP75)), Math.max(...strata.map(item => item.clsP75))],
  thresholdExceedances: strata.filter(item => item.lcpP75Ms > 2500 || item.clsP75 > 0.1),
  hardware: report.profile.hardware,
  measurementPeriodUtc: report.profile.measurementPeriodUtc,
  limitation: report.limitation,
}

console.log(JSON.stringify(summary, null, 2))
if (summary.strata !== 24 || summary.scoredNavigations !== 720 || summary.warmups !== 24 || summary.failures || issues.length) {
  process.exitCode = 1
}
