// Demo PNGs have WebP siblings recorded in public/demo-assets/WEBP_DERIVATIVES.json.
// Returns null for anything else, such as the SVG placeholder.
export function webpSourceFor(url: string): string | null {
  return /\.png$/.test(url) ? url.replace(/\.png$/, '.webp') : null
}
