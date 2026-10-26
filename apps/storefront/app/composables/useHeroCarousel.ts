import { onMounted, onUnmounted, ref } from 'vue'

interface UseHeroCarouselOptions {
  slideCount: number
  intervalMs?: number
}

export function useHeroCarousel({ slideCount, intervalMs = 3000 }: UseHeroCarouselOptions) {
  const activeSlide = ref(0)
  const isManuallyPaused = ref(false)
  const isPointerInside = ref(false)
  const isFocusWithin = ref(false)
  const prefersReducedMotion = ref(false)
  let intervalId: ReturnType<typeof setInterval> | undefined
  let motionQuery: MediaQueryList | undefined

  function stopTimer() {
    if (intervalId) {
      clearInterval(intervalId)
      intervalId = undefined
    }
  }

  function syncTimer() {
    stopTimer()
    if (prefersReducedMotion.value || isManuallyPaused.value || isPointerInside.value || isFocusWithin.value || slideCount < 2) return
    intervalId = setInterval(() => {
      activeSlide.value = (activeSlide.value + 1) % slideCount
    }, intervalMs)
  }

  function showSlide(index: number) {
    activeSlide.value = (index + slideCount) % slideCount
    syncTimer()
  }

  function moveSlide(direction: -1 | 1) {
    showSlide(activeSlide.value + direction)
  }

  function togglePause() {
    isManuallyPaused.value = !isManuallyPaused.value
    syncTimer()
  }

  function setPointerInside(value: boolean) {
    isPointerInside.value = value
    syncTimer()
  }

  function setFocusWithin(value: boolean) {
    isFocusWithin.value = value
    syncTimer()
  }

  function updateMotionPreference(event: MediaQueryListEvent) {
    prefersReducedMotion.value = event.matches
    syncTimer()
  }

  onMounted(() => {
    motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
    prefersReducedMotion.value = motionQuery.matches
    motionQuery.addEventListener('change', updateMotionPreference)
    syncTimer()
  })

  onUnmounted(() => {
    stopTimer()
    motionQuery?.removeEventListener('change', updateMotionPreference)
  })

  return {
    activeSlide,
    isManuallyPaused,
    moveSlide,
    prefersReducedMotion,
    setFocusWithin,
    setPointerInside,
    showSlide,
    togglePause,
  }
}
