import { createDataSourceFetch } from '../utils/data-source-fetch'

export function useStorefrontApi() {
  const { source, backendAvailable } = useDataSource()
  return createDataSourceFetch({ source: source.value, backendAvailable, backendFetch: useRequestFetch() })
}
