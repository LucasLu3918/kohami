import { createDataSourceFetch } from '../utils/data-source-fetch'

export function useStorefrontApi() {
  const { source } = useDataSource()
  return createDataSourceFetch({ source: source.value, backendFetch: useRequestFetch() })
}
