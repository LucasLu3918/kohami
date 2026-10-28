import { test } from 'node:test'
import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

// Each PNG under public/demo-assets stays the provenance source. Its WebP derivative
// is what browsers download through <picture>, so the two must never drift apart.
const assetsRoot = fileURLToPath(new URL('../public/demo-assets', import.meta.url))
const manifestPath = join(assetsRoot, 'WEBP_DERIVATIVES.json')

function listFiles(directory, extension) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) return listFiles(path, extension)
    return entry.name.endsWith(extension) ? [path] : []
  })
}

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex')
}

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
const pngs = listFiles(assetsRoot, '.png').map(path => relative(assetsRoot, path)).sort()

test('every demo PNG has a recorded WebP derivative', () => {
  const recorded = new Set(manifest.derivatives.map(item => item.source))
  const missing = pngs.filter(source => !recorded.has(source))
  assert.deepEqual(missing, [])
})

test('recorded derivatives match their PNG source and WebP output bytes', () => {
  for (const item of manifest.derivatives) {
    const source = join(assetsRoot, item.source)
    const derivative = join(assetsRoot, item.derivative)
    assert.ok(existsSync(source), `${item.source} exists`)
    assert.ok(existsSync(derivative), `${item.derivative} exists`)
    assert.equal(sha256(source), item.sourceSha256, `${item.source} sha256 matches manifest`)
    assert.equal(sha256(derivative), item.sha256, `${item.derivative} sha256 matches manifest`)
    const header = readFileSync(derivative).subarray(0, 12).toString('latin1')
    assert.ok(header.startsWith('RIFF') && header.slice(8) === 'WEBP', `${item.derivative} is a WebP file`)
    assert.ok(statSync(derivative).size < statSync(source).size, `${item.derivative} is smaller than its PNG source`)
  }
})

test('derivative path is the PNG path with a .webp extension', () => {
  for (const item of manifest.derivatives) {
    assert.equal(item.derivative, item.source.replace(/\.png$/, '.webp'))
  }
})
