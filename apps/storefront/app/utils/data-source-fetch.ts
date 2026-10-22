import { ofetch } from 'ofetch'
import { demoProblem, mockApiResponse } from './demo-api.ts'

type ApiOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH' | 'HEAD' | 'OPTIONS'
  body?: Record<string, unknown>
  headers?: Record<string, string>
  credentials?: RequestCredentials
}
type ApiFetch = <T = unknown>(request: string, options?: ApiOptions) => Promise<T>

export function createDataSourceFetch(options: {
  source: 'mock' | 'backend'
  backendAvailable: boolean
  backendFetch: ApiFetch
}): ApiFetch {
  if (options.source === 'backend' && options.backendAvailable) return options.backendFetch
  return ofetch.create({ retry: 0 }, {
    fetch: async (request, init) => options.source === 'backend'
      ? demoProblem(503, 'BACKEND_NOT_CONFIGURED')
      : mockApiResponse(String(request), init?.method || 'GET', init?.body, init?.headers, !options.backendAvailable),
  })
}
