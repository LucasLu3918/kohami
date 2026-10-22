// Existing integration suites exercise the API, rather than the new default demo source.
export function backendModeStorageState(origin: string) {
  const url = new URL(origin)
  return { cookies: [{ name: 'kohami.data-source', value: 'backend', domain: url.hostname, path: '/', expires: -1, httpOnly: false, secure: url.protocol === 'https:', sameSite: 'Lax' as const }], origins: [] }
}
