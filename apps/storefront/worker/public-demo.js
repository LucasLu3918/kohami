const API_ROOT = '/api/v1'

function unavailableProblem() {
  const body = {
    type: 'about:blank',
    title: '服務暫時無法使用',
    status: 503,
    code: 'SERVICE_UNAVAILABLE',
  }
  return new Response(JSON.stringify(body), {
    status: 503,
    headers: {
      'cache-control': 'no-store',
      'content-type': 'application/problem+json; charset=utf-8',
    },
  })
}

export default {
  async fetch(request, env) {
    const { pathname } = new URL(request.url)
    if (pathname === API_ROOT || pathname.startsWith(`${API_ROOT}/`)) return unavailableProblem()
    return env.ASSETS.fetch(request)
  },
}
