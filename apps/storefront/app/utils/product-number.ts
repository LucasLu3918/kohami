const CROCKFORD_BASE32 = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'
const FNV_OFFSET_BASIS = 0xcbf29ce484222325n
const FNV_PRIME = 0x100000001b3n

/** Stable, non-semantic customer-facing code derived from the immutable catalog SKU key. */
export function formatProductNumber(skuId: string): string {
  let hash = FNV_OFFSET_BASIS
  for (const byte of new TextEncoder().encode(skuId)) {
    hash = BigInt.asUintN(64, (hash ^ BigInt(byte)) * FNV_PRIME)
  }

  let encoded = ''
  do {
    encoded = CROCKFORD_BASE32[Number(hash & 31n)] + encoded
    hash >>= 5n
  } while (hash > 0n)

  return `KH-${encoded.padStart(13, '0')}`
}
