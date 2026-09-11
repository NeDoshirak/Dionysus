<script setup>
import { computed, ref } from 'vue'
import { useWaveSurfer } from '@meersagor/wavesurfer-vue'
import { getAuthenticatedFetchOptions } from '@/shared/api/client'
import { BaseButton } from '@/shared/ui'

const props = defineProps({
  projectId: { type: String, required: true },
  recording: { type: Object, required: true },
})

const containerRef = ref(null)
const streamUrl = computed(() => Object.prototype.hasOwnProperty.call(props.recording, 'streamUrl')
  ? props.recording.streamUrl
  : `/api/projects/${encodeURIComponent(props.projectId)}/recordings/${encodeURIComponent(props.recording.id)}/stream`)
const options = computed(() => ({
  ...(streamUrl.value ? { url: streamUrl.value } : {}),
  ...(Array.isArray(props.recording.waveformPeaks) ? { peaks: props.recording.waveformPeaks } : {}),
  ...(Number.isFinite(props.recording.durationSeconds) ? { duration: props.recording.durationSeconds } : {}),
  height: 72,
  waveColor: '#f7b1a7',
  progressColor: '#f1361d',
  cursorColor: '#111827',
  barWidth: 2,
  barGap: 2,
  barRadius: 2,
  fetchParams: getAuthenticatedFetchOptions(),
}))

const { waveSurfer, isReady, isPlaying, currentTime, totalDuration } = useWaveSurfer({
  containerRef,
  options,
})

const progress = computed(() => totalDuration.value ? currentTime.value / totalDuration.value * 100 : 0)

function formatTime(seconds) {
  const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0))
  const minutes = Math.floor(safeSeconds / 60)
  const restSeconds = String(safeSeconds % 60).padStart(2, '0')

  return `${minutes}:${restSeconds}`
}

function togglePlayback() {
  waveSurfer.value?.playPause()
}

function seek(seconds) {
  waveSurfer.value?.setTime(seconds)
}

function seekFromControl(event) {
  if (!totalDuration.value) return
  seek(Number(event.target.value) / 100 * totalDuration.value)
}

defineExpose({ seek })
</script>

<template>
  <section class="specification-player" aria-label="Запись встречи">
    <div class="specification-player__header">
      <div>
        <h2 class="specification-player__title">Запись встречи</h2>
        <p class="specification-player__time">
          {{ formatTime(currentTime) }} / {{ formatTime(totalDuration) }}
        </p>
      </div>
      <BaseButton type="button" :disabled="!isReady" @click="togglePlayback">
        {{ isPlaying ? 'Пауза' : 'Воспроизвести' }}
      </BaseButton>
    </div>

    <div ref="containerRef" class="specification-player__waveform" />

    <label class="specification-player__range">
      <span class="specification-player__range-label">Позиция воспроизведения</span>
      <input
        type="range"
        min="0"
        max="100"
        :value="progress"
        :disabled="!isReady || !totalDuration"
        @input="seekFromControl"
      >
    </label>
  </section>
</template>

<style scoped lang="sass">
.specification-player
  display: grid
  gap: 16px
  padding: 20px
  border: 1px solid var(--color-border)
  border-radius: var(--radius-sm)
  background: var(--color-surface)

  &__header
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px

  &__title
    margin: 0
    color: var(--color-text)
    font-size: 18px
    line-height: 1.35

  &__time
    margin: 4px 0 0
    color: var(--color-muted)
    font-size: 14px
    line-height: 1.4

  &__waveform
    min-height: 72px

  &__range
    display: grid
    gap: 8px

  &__range-label
    color: var(--color-muted)
    font-size: 13px
    line-height: 1.4

  input[type="range"]
    width: 100%
    accent-color: var(--color-accent-strong)

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 4px

@media (max-width: 640px)
  .specification-player
    padding: 16px

    &__header
      align-items: stretch
      flex-direction: column
</style>
