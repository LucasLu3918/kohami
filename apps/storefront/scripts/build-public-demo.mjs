import { spawnSync } from 'node:child_process'
import { writeFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const env = { ...process.env, KOHAMI_PUBLIC_DEMO: 'true', NUXT_PUBLIC_BACKEND_AVAILABLE: 'false', NUXT_PUBLIC_LOCAL_DEMO: 'false', NUXT_PUBLIC_SITE_URL: '' }
delete env.NUXT_API_ORIGIN
delete env.NUXT_SITE_ORIGIN
const result = spawnSync(process.execPath, ['node_modules/nuxt/bin/nuxt.mjs', 'generate', '--dotenv', '/dev/null'], { cwd: root, env, stdio: 'inherit' })
if (result.error) throw result.error
if (result.status !== 0) process.exit(result.status || 1)
if (existsSync(new URL('../.output/public/_worker.js', import.meta.url))) throw new Error('PUBLIC_DEMO_MUST_BE_STATIC')
writeFileSync(new URL('../.output/public/_headers', import.meta.url), '/*\n  Cache-Control: no-store\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: no-referrer\n  X-Robots-Tag: noindex, nofollow\n')
