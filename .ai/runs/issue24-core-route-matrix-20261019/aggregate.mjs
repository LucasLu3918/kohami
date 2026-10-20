import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
const directory = path.dirname(fileURLToPath(import.meta.url))
const inputs = ['metrics-batch1.json', 'metrics-batch2.json'].map(name => path.join(directory, name))
const outputPath = path.join(directory, 'metrics-merged.json')
const runs = inputs.map(input => JSON.parse(fs.readFileSync(input, 'utf8')))
const raw = runs.flatMap(run => run.raw)
function percentile(values, p) {
  const sorted = [...values].sort((a, b) => a - b)
  return sorted[Math.ceil(sorted.length * p) - 1]
}
const locales = ['zh-TW', 'ja', 'en']
const routes = ['home-search-list', 'product-detail', 'cart-checkout-form', 'checkout-result']
const groups = {}
for (const locale of locales) for (const route of routes) for (const cache of ['cold', 'warm']) {
  const samples = raw.filter(item => item.locale === locale && item.route === route && item.cache === cache)
  const lcp = samples.map(item => item.lcpMs).filter(Number.isFinite)
  const cls = samples.map(item => item.cls).filter(Number.isFinite)
  groups[`${locale}/${route}/${cache}`] = {
    samples: samples.length, lcpObserved: lcp.length, clsObserved: cls.length,
    lcpP50Ms: +percentile(lcp, .5).toFixed(1), lcpP75Ms: +percentile(lcp, .75).toFixed(1), lcpMaxMs: +Math.max(...lcp).toFixed(1),
    clsP50: +percentile(cls, .5).toFixed(4), clsP75: +percentile(cls, .75).toFixed(4), clsMax: +Math.max(...cls).toFixed(4),
    http200: samples.filter(item => item.status === 200).length,
    pageErrors: samples.reduce((sum, item) => sum + item.pageErrors.length, 0),
    cacheHits: samples.reduce((sum, item) => sum + item.cacheHits, 0),
    assetResponses: samples.reduce((sum, item) => sum + item.cacheableAssetResponses, 0),
  }
}
const measured = raw.filter(item => item.cache === 'cold' || item.cache === 'warm')
const warmups = raw.filter(item => item.cache === 'warmup')
const failures = runs.flatMap(run => run.failures)
const invalidGroups = Object.entries(groups).filter(([, group]) =>
  group.samples !== 30 || group.lcpObserved !== 30 || group.clsObserved !== 30 || group.http200 !== 30 || group.pageErrors !== 0 || group.lcpP75Ms > 2500 || group.clsP75 > .1)
const missingWarmCache = Object.entries(groups).filter(([key, group]) => key.endsWith('/warm') && group.cacheHits === 0)
const output = {
  profile: {
    ...runs[0].profile,
    samplesPerLocaleRouteCache: 30,
    percentile: 'Nearest rank: sorted ascending, index ceil(n * p) - 1.',
    measurementPeriodUtc: `${runs[0].profile.startedAt} to ${runs[1].profile.finishedAt}`,
    measuredNavigations: measured.length,
    warmupNavigations: warmups.length,
    allNavigations: raw.length,
    batches: inputs.map(input => path.basename(input)),
  },
  groups, failures, raw,
  limitation: 'Desktop localhost synthetic lab only. Not field Core Web Vitals, formal INP, real API/SQL/payment/LAN, mobile, or SLA evidence.',
}
fs.writeFileSync(outputPath, JSON.stringify(output, null, 2) + '\n')
console.log(JSON.stringify({
  groups: Object.keys(groups).length, measured: measured.length, warmups: warmups.length, all: raw.length,
  failures: failures.length, invalidGroups: invalidGroups.map(([key]) => key), missingWarmCache: missingWarmCache.map(([key]) => key),
  lcpP75RangeMs: [Math.min(...Object.values(groups).map(group => group.lcpP75Ms)), Math.max(...Object.values(groups).map(group => group.lcpP75Ms))],
  clsP75Range: [Math.min(...Object.values(groups).map(group => group.clsP75)), Math.max(...Object.values(groups).map(group => group.clsP75))],
  outputPath,
}, null, 2))
if (measured.length !== 720 || warmups.length !== 24 || Object.keys(groups).length !== 24 || failures.length || invalidGroups.length || missingWarmCache.length) process.exitCode = 1
