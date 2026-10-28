#!/usr/bin/env node
// Encodes every demo PNG into a sibling WebP and records the derivation in
// public/demo-assets/WEBP_DERIVATIVES.json. The PNG stays the provenance source,
// so the sha256 values in the existing ASSET_MANIFEST files remain valid.
import { execFile } from 'node:child_process'
import { createHash } from 'node:crypto'
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'

const run = promisify(execFile)
const assetsRoot = fileURLToPath(new URL('../public/demo-assets', import.meta.url))
const manifestPath = join(assetsRoot, 'WEBP_DERIVATIVES.json')
const encoder = { tool: 'cwebp', quality: 82, method: 6 }

function listPngs(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) return listPngs(path)
    return entry.name.endsWith('.png') ? [path] : []
  })
}

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex')
}

const derivatives = []
for (const sourcePath of listPngs(assetsRoot).sort()) {
  const derivativePath = sourcePath.replace(/\.png$/, '.webp')
  await run('cwebp', ['-quiet', '-q', String(encoder.quality), '-m', String(encoder.method), sourcePath, '-o', derivativePath])
  derivatives.push({
    source: relative(assetsRoot, sourcePath),
    derivative: relative(assetsRoot, derivativePath),
    sourceSha256: sha256(sourcePath),
    sha256: sha256(derivativePath),
    sourceBytes: statSync(sourcePath).size,
    bytes: statSync(derivativePath).size,
  })
}

const manifest = {
  schemaVersion: 1,
  usage: 'WebP derivatives of the PNG sources, served through <picture> with the PNG as the img fallback. PNG remains the provenance source.',
  encoder: { ...encoder, command: `cwebp -quiet -q ${encoder.quality} -m ${encoder.method}` },
  derivatives,
}
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`)

const before = derivatives.reduce((total, item) => total + item.sourceBytes, 0)
const after = derivatives.reduce((total, item) => total + item.bytes, 0)
console.log(`encoded ${derivatives.length} files: ${(before / 1048576).toFixed(1)} MB PNG -> ${(after / 1048576).toFixed(1)} MB WebP`)
