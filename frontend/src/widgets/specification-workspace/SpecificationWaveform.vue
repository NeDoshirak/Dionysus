<script setup>
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import WaveSurfer from 'wavesurfer.js'

const props = defineProps({
  url: { type: String, required: true },
  fetchParams: { type: Object, default: () => ({}) },
  height: { type: Number, default: 72 },
  waveColor: { type: String, default: '#f7b1a7' },
  progressColor: { type: String, default: '#f1361d' },
  cursorColor: { type: String, default: '#111827' },
  barWidth: { type: Number, default: 2 },
  barGap: { type: Number, default: 2 },
  barRadius: { type: Number, default: 2 },
})

const emit = defineEmits(['ready', 'error', 'timeupdate', 'play', 'pause', 'finish'])

const containerRef = ref(null)
const ws = shallowRef(null)

onMounted(() => {
  if (!containerRef.value) {
    console.warn('[waveform] container is not mounted')
    return
  }

  console.log('[waveform] create', {
    url: props.url,
    headers: props.fetchParams?.headers,
  })

  ws.value = WaveSurfer.create({
    container: containerRef.value,
    url: props.url,
    height: props.height,
    waveColor: props.waveColor,
    progressColor: props.progressColor,
    cursorColor: props.cursorColor,
    barWidth: props.barWidth,
    barGap: props.barGap,
    barRadius: props.barRadius,
    fetchParams: props.fetchParams,
  })

  ws.value.on('ready', () => {
    console.log('[waveform] ready', ws.value.getDuration())
    emit('ready', ws.value)
  })
  ws.value.on('error', (err) => {
    console.error('[waveform] error', err)
    emit('error', err)
  })
  ws.value.on('timeupdate', (time) => emit('timeupdate', time))
  ws.value.on('play', () => emit('play'))
  ws.value.on('pause', () => emit('pause'))
  ws.value.on('finish', () => emit('finish'))
})

onBeforeUnmount(() => {
  const instance = ws.value
  if (!instance) return
  instance.unAll?.()
  instance.destroy?.()
  ws.value = null
})

defineExpose({
  seek(seconds) {
    if (!ws.value) return
    ws.value.setTime(seconds)
  },
  playPause() {
    if (!ws.value) return
    ws.value.playPause()
  },
})
</script>

<template>
  <div ref="containerRef" class="specification-waveform" />
</template>

<style scoped lang="sass">
.specification-waveform
  width: 100%
  min-height: 72px
</style>