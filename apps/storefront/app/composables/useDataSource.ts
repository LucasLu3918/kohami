export function useDataSource() {
  const preference = useCookie<string>('kohami.data-source', { default: () => 'mock', sameSite: 'lax', path: '/', maxAge: 60 * 60 * 24 * 30 })
  // Keep the active source stable until the full reload has cleared all page state.
  const source = useState<'mock' | 'backend'>('storefront-data-source', () => preference.value === 'backend' ? 'backend' : 'mock')
  // Static prerender payloads were generated in mock mode, so reapply the visitor's
  // saved choice on the client before page requests are issued.
  if (import.meta.client) source.value = preference.value === 'backend' ? 'backend' : 'mock'
  return { source: readonly(source), preference }
}
