export function useDataSource() {
  const config = useRuntimeConfig()
  const preference = useCookie<string>('kohami.data-source', { default: () => 'mock', sameSite: 'lax', path: '/', maxAge: 60 * 60 * 24 * 30 })
  const backendAvailable = config.public.backendAvailable === true
  // Keep the active source stable until the full reload has cleared all page state.
  const source = useState<'mock' | 'backend'>('storefront-data-source', () => backendAvailable && preference.value === 'backend' ? 'backend' : 'mock')
  return { source: readonly(source), preference, backendAvailable, readOnly: computed(() => source.value === 'mock') }
}
