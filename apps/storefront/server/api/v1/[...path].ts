import { defineEventHandler, getRequestURL, getRequestHeaders, getMethod, readRawBody, setResponseStatus, setHeader, appendResponseHeader, send } from 'h3'
export default defineEventHandler(async (event) => {
  setHeader(event, 'cache-control', 'no-store')
  const problem = (status: number, code: string) => { setResponseStatus(event, status); setHeader(event, 'content-type', 'application/problem+json'); return { type: 'about:blank', title: '無法完成模擬操作', status, code, traceId: crypto.randomUUID() } }
  try {
  const url = getRequestURL(event)
  const host = getRequestHeaders(event).host
  if (host !== 'localhost:3000') return problem(403, 'CSRF_INVALID')
  const path = url.pathname
  if (!path.startsWith('/api/v1/') || path.includes('..') || path.includes('%2f')) return problem(404, 'RESOURCE_NOT_FOUND')
  const method = getMethod(event)
  const incoming = getRequestHeaders(event)
  const headers: Record<string, string> = { 'x-forwarded-proto': 'https' }
  // Per-request allowlist; no shared cookies/cache, no browser-supplied upstream target.
  for (const name of ['cookie', 'origin', 'content-type', 'x-csrf-token', 'idempotency-key', 'if-match', 'x-change-reason']) if (incoming[name]) headers[name] = incoming[name]!
  const body = ['GET', 'HEAD'].includes(method) ? undefined : await readRawBody(event)
  if (body && Buffer.byteLength(body) > 65536) return problem(413, 'PAYLOAD_TOO_LARGE')
  const response = await fetch(`http://127.0.0.1:5080${path}${url.search}`, { method, headers, body, redirect: 'manual', signal: AbortSignal.timeout(20000) })
  setResponseStatus(event, response.status)
  for (const name of ['content-type', 'etag', 'location']) { const value = response.headers.get(name); if (value) setHeader(event, name, value) }
  setHeader(event, 'cache-control', 'no-store')
  for (const cookie of response.headers.getSetCookie()) appendResponseHeader(event, 'set-cookie', cookie)
  return send(event, await response.text())
  } catch { return problem(503, 'SERVICE_UNAVAILABLE') }
})
