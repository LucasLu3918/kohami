import { defineEventHandler, setHeader } from 'h3'
export default defineEventHandler((event) => { setHeader(event, 'cache-control', 'no-store'); setHeader(event, 'x-content-type-options', 'nosniff'); setHeader(event, 'referrer-policy', 'no-referrer') })
