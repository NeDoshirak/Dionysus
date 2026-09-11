<script setup>
import { computed } from 'vue'
import { itemKindLabels } from '@/entities/specification'
import { ClarificationToggle } from '@/features/mark-requirement'
import { BaseButton } from '@/shared/ui'

const props = defineProps({
  item: { type: Object, required: true },
  editable: Boolean,
  clarified: Boolean,
  allowClarification: { type: Boolean, default: true },
})

const emit = defineEmits([
  'source',
  'edit',
  'delete',
  'update:clarified',
])

const kindLabel = computed(() => itemKindLabels[props.item.kind] || props.item.kind || 'Карточка')
const cardClass = computed(() => {
  const kind = String(props.item.kind || 'unknown')
    .replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)

  return `specification-card--${kind}`
})
const metaLabel = computed(() => props.item.kind === 'keyQuestion' ? 'Причина' : 'Приоритет')
const metaValue = computed(() => props.item.priority ?? props.item.reason)
const hasMeta = computed(() => Boolean(metaValue.value))
const isManual = computed(() => {
  if (typeof props.item.isManual === 'boolean') return props.item.isManual
  if (props.item.kind === 'businessContext' || props.item.kind === 'projectContradiction') return false

  return !props.item.sourceStatementIds?.length
})
const sources = computed(() => Array.isArray(props.item.sourceSegments) ? props.item.sourceSegments : [])

function formatTime(seconds) {
  const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0))
  const minutes = Math.floor(safeSeconds / 60)
  const restSeconds = String(safeSeconds % 60).padStart(2, '0')

  return `${minutes}:${restSeconds}`
}

function emitSource(source) {
  emit('source', {
    startSeconds: source.startSeconds,
    endSeconds: source.endSeconds,
    text: source.text,
  })
}
</script>

<template>
  <article
    class="specification-card"
    :class="cardClass"
    :data-kind="item.kind"
    :aria-labelledby="item.title ? `specification-card-title-${item.id}` : undefined"
  >
    <div class="specification-card__content">
      <div class="specification-card__header">
        <span class="specification-card__kind">{{ kindLabel }}</span>
        <span v-if="isManual" class="specification-card__manual">Добавлено вручную</span>
      </div>

      <h3
        v-if="item.title"
        :id="`specification-card-title-${item.id}`"
        class="specification-card__title"
      >
        {{ item.title }}
      </h3>
      <p class="specification-card__description">{{ item.description }}</p>
      <p v-if="hasMeta" class="specification-card__meta">
        <span class="specification-card__meta-label">{{ metaLabel }}:</span> {{ metaValue }}
      </p>

      <div v-if="sources.length" class="specification-card__sources" aria-label="Источники">
        <button
          v-for="(source, index) in sources"
          :key="`${source.startSeconds}-${source.endSeconds}-${index}`"
          class="specification-card__source"
          type="button"
          :data-source-start="source.startSeconds"
          :aria-label="`Перейти к источнику ${formatTime(source.startSeconds)}–${formatTime(source.endSeconds)}`"
          @click="emitSource(source)"
        >
          <span class="specification-card__source-time">
            {{ formatTime(source.startSeconds) }}–{{ formatTime(source.endSeconds) }}
          </span>
          <span v-if="source.text" class="specification-card__source-text">{{ source.text }}</span>
        </button>
      </div>

      <ClarificationToggle
        v-if="allowClarification"
        class="specification-card__clarification"
        :model-value="clarified"
        @update:model-value="emit('update:clarified', $event)"
      />
    </div>

    <div v-if="editable" class="specification-card__actions">
      <BaseButton
        type="button"
        variant="outline"
        :data-action="`edit-item-${item.id}`"
        @click="emit('edit', item)"
      >
        Изменить
      </BaseButton>
      <BaseButton
        type="button"
        variant="text"
        :data-action="`delete-item-${item.id}`"
        @click="emit('delete', item)"
      >
        Удалить
      </BaseButton>
    </div>
  </article>
</template>

<style scoped lang="sass">
.specification-card
  display: grid
  grid-template-columns: minmax(0, 1fr) auto
  gap: 18px
  padding: 18px
  border: 1px solid var(--color-border)
  border-left: 4px solid var(--color-subtle)
  border-radius: var(--radius-sm)
  background: var(--color-surface)

  &--functional-requirement
    border-left-color: var(--color-accent-strong)

  &--role
    border-left-color: #2563eb

  &--user-scenario
    border-left-color: #7c3aed

  &--constraint
    border-left-color: #475569

  &--condition
    border-left-color: #059669

  &--agreement
    border-left-color: #c2410c

  &--key-question,
  &--project-contradiction
    border-left-color: var(--color-danger)

  &__content
    display: grid
    gap: 10px
    min-width: 0

  &__header
    display: flex
    flex-wrap: wrap
    gap: 8px

  &__kind,
  &__manual
    width: fit-content
    padding: 3px 8px
    border-radius: var(--radius-sm)
    background: var(--color-background)
    color: var(--color-muted)
    font-size: 12px
    font-weight: 600
    line-height: 1.4

  &__manual
    color: var(--color-accent-strong)

  &__title
    margin: 0
    color: var(--color-text)
    font-size: 18px
    line-height: 1.35

  &__description,
  &__meta
    margin: 0
    color: var(--color-muted)
    font-size: 14px
    line-height: 1.6

  &__meta-label
    color: var(--color-text)
    font-weight: 600

  &__sources
    display: grid
    gap: 8px

  &__source
    display: grid
    gap: 2px
    justify-items: start
    width: fit-content
    max-width: 100%
    min-height: 36px
    padding: 7px 11px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
    background: var(--color-background)
    color: var(--color-accent-strong)
    font: inherit
    font-size: 13px
    text-align: left
    cursor: pointer

    &:hover
      border-color: var(--color-accent-strong)
      background: var(--color-accent-soft)

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

  &__source-time
    font-weight: 700

  &__source-text
    max-width: 100%
    overflow: hidden
    color: var(--color-text-secondary)
    text-overflow: ellipsis
    white-space: nowrap

  &__actions
    display: flex
    align-items: flex-start
    gap: 10px

@media (max-width: 760px)
  .specification-card
    grid-template-columns: 1fr

    &__actions
      flex-wrap: wrap
</style>
