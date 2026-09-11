<script setup>
import { computed } from 'vue'

const props = defineProps({
  segments: { type: Array, default: () => [] },
  activeStartSeconds: { type: Number, default: null },
  query: { type: String, default: '' },
})

const normalizedQuery = computed(() => props.query.trim().toLocaleLowerCase('ru'))
const visibleSegments = computed(() => {
  if (!normalizedQuery.value) return [...props.segments]

  return props.segments.filter((segment) => (
    String(segment.text || '').toLocaleLowerCase('ru').includes(normalizedQuery.value)
  ))
})
const activeIndex = computed(() => {
  if (props.activeStartSeconds === null || !visibleSegments.value.length) return -1

  return visibleSegments.value.findIndex((segment) => (
    Number(segment.startSeconds) === Number(props.activeStartSeconds)
  ))
})

function formatTime(seconds) {
  const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0))
  const minutes = Math.floor(safeSeconds / 60)
  const restSeconds = String(safeSeconds % 60).padStart(2, '0')

  return `${minutes}:${restSeconds}`
}
</script>

<template>
  <section class="specification-transcript" aria-label="Транскрипция">
    <p v-if="!segments.length" class="specification-transcript__empty">
      Транскрипция для записи пока недоступна.
    </p>
    <p v-else-if="!visibleSegments.length" class="specification-transcript__empty">
      В загруженной транскрипции нет совпадений.
    </p>
    <ol v-else class="specification-transcript__list">
      <li
        v-for="(segment, index) in visibleSegments"
        :key="`${segment.startSeconds}-${segment.endSeconds}-${index}`"
        class="specification-transcript__segment"
        :class="{ 'specification-transcript__segment--active': index === activeIndex }"
      >
        <span class="specification-transcript__time">
          {{ formatTime(segment.startSeconds) }}-{{ formatTime(segment.endSeconds) }}
        </span>
        <span class="specification-transcript__text">{{ segment.text }}</span>
      </li>
    </ol>
  </section>
</template>

<style scoped lang="sass">
.specification-transcript
  min-height: 0

  &__empty
    margin: 0
    padding: 18px
    border: 1px dashed var(--color-border)
    border-radius: var(--radius-sm)
    color: var(--color-muted)
    font-size: 14px
    line-height: 1.6

  &__list
    display: grid
    gap: 10px
    max-height: 420px
    margin: 0
    padding: 0
    list-style: none
    overflow: auto

  &__segment
    display: grid
    gap: 6px
    padding: 12px
    border-left: 3px solid transparent
    border-radius: var(--radius-sm)
    background: var(--color-background)

    &--active
      border-left-color: var(--color-accent-strong)
      background: var(--color-accent-soft)

  &__time
    color: var(--color-muted)
    font-size: 12px
    font-weight: 600
    line-height: 1.4

  &__text
    color: var(--color-text)
    font-size: 14px
    line-height: 1.55
</style>
