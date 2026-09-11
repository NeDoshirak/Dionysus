<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { getSession } from '@/entities/session'
import { BaseButton } from '@/shared/ui'
import SpecificationWaveform from './SpecificationWaveform.vue'

const props = defineProps({
  projectId: { type: String, required: true },
  recording: { type: Object, required: true },
})

const waveformRef = ref(null)

const accessToken = computed(() => getSession()?.accessToken || '')

const streamUrl = computed(() => {
  const explicit = props.recording?.streamUrl
  if (typeof explicit === 'string' && explicit.length > 0) return explicit
  if (!props.projectId || !props.recording?.id) return null
  return `/api/projects/${encodeURIComponent(props.projectId)}/recordings/${encodeURIComponent(props.recording.id)}/stream`
})

const fetchParams = computed(() => ({
  credentials: 'include',
  headers: accessToken.value ? { Authorization: `Bearer ${accessToken.value}` } : {},
}))

const canRender = computed(() => Boolean(streamUrl.value) && Boolean(accessToken.value))

// ключ: пересоздаём wavesurfer при смене url или токена
const waveformKey = computed(() => `${streamUrl.value}::${accessToken.value.slice(0, 12)}`)

const isReady = ref(false)
const isPlaying = ref(false)
const currentTime = ref(0)
const totalDuration = ref(0)

const progress = computed(() => totalDuration.value ? currentTime.value / totalDuration.value * 100 : 0)

function formatTime(seconds) {
  const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0))
  const minutes = Math.floor(safeSeconds / 60)
  const restSeconds = String(safeSeconds % 60).padStart(2, '0')
  return `${minutes}:${restSeconds}`
}

function onWaveformReady(ws) {
  console.log('[player] waveform ready, duration', ws.getDuration())
  isReady.value = true
  totalDuration.value = ws.getDuration() || 0
}

function onWaveformError(err) {
  console.error('[player] waveform error', err)
  isReady.value = false
}

function onTimeUpdate(time) {
  currentTime.value = Number(time) || 0
}

function onPlay() { isPlaying.value = true }
function onPause() { isPlaying.value = false }
function onFinish() { isPlaying.value = false }

function togglePlayback() {
  if (!isReady.value) return
  waveformRef.value?.playPause()
}

function seek(seconds) {
  if (!isReady.value) return
  waveformRef.value?.seek(seconds)
}

function seekFromControl(event) {
  if (!totalDuration.value || !isReady.value) return
  seek(Number(event.target.value) / 100 * totalDuration.value)
}

watch(() => props.recording?.id, () => {
  isReady.value = false
  isPlaying.value = false
  currentTime.value = 0
  totalDuration.value = 0
})

onBeforeUnmount(() => {
  isReady.value = false
})

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

    <SpecificationWaveform
      v-if="canRender"
      :key="waveformKey"
      ref="waveformRef"
      :url="streamUrl"
      :fetch-params="fetchParams"
      @ready="onWaveformReady"
      @error="onWaveformError"
      @timeupdate="onTimeUpdate"
      @play="onPlay"
      @pause="onPause"
      @finish="onFinish"
    />
    <div v-else class="specification-player__waveform specification-player__waveform--placeholder">
      Загружаем запись...
    </div>

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

    &--placeholder
      display: grid
      place-items: center
      color: var(--color-muted)
      font-size: 14px

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