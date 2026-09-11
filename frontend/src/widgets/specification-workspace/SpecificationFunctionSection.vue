<script setup>
import SpecificationCard from './SpecificationCard.vue'
import { BaseButton } from '@/shared/ui'

const props = defineProps({
  functionItem: { type: Object, required: true },
  items: { type: Array, default: () => [] },
  editable: Boolean,
  clarificationIds: {
    type: [Set, Array, Object],
    default: () => new Set(),
  },
})

const emit = defineEmits([
  'add-item',
  'edit-function',
  'delete-function',
  'edit-item',
  'delete-item',
  'source',
  'toggle-clarification',
])

function isClarified(itemId) {
  if (Array.isArray(props.clarificationIds)) return props.clarificationIds.includes(itemId)
  if (typeof props.clarificationIds?.has === 'function') return props.clarificationIds.has(itemId)

  return false
}
</script>

<template>
  <details
    class="specification-function-section"
    :open="functionItem.open !== false"
  >
    <summary
      class="specification-function-section__summary"
      :aria-label="`Функция: ${functionItem.title}`"
    >
      <span class="specification-function-section__summary-content">
        <span class="specification-function-section__title">{{ functionItem.title }}</span>
        <span class="specification-function-section__count">
          {{ items.length }} {{ items.length === 1 ? 'карточка' : 'карточек' }}
        </span>
      </span>
    </summary>

    <div class="specification-function-section__body">
      <p v-if="functionItem.description" class="specification-function-section__description">
        {{ functionItem.description }}
      </p>

      <div v-if="editable" class="specification-function-section__actions">
        <BaseButton
          type="button"
          :data-action="`add-item-${functionItem.id}`"
          @click="emit('add-item', functionItem)"
        >
          Добавить карточку
        </BaseButton>
        <BaseButton
          type="button"
          variant="outline"
          :data-action="`edit-function-${functionItem.id}`"
          @click="emit('edit-function', functionItem)"
        >
          Изменить функцию
        </BaseButton>
        <BaseButton
          type="button"
          variant="text"
          :data-action="`delete-function-${functionItem.id}`"
          @click="emit('delete-function', functionItem)"
        >
          Удалить функцию
        </BaseButton>
      </div>

      <div v-if="items.length" class="specification-function-section__cards">
        <SpecificationCard
          v-for="item in items"
          :key="item.id"
          :item="item"
          :editable="editable"
          :clarified="isClarified(item.id)"
          @source="emit('source', $event)"
          @edit="emit('edit-item', functionItem, $event)"
          @delete="emit('delete-item', functionItem, $event)"
          @update:clarified="emit('toggle-clarification', item.id, $event)"
        />
      </div>
      <p v-else class="specification-function-section__empty">
        В этой функции пока нет карточек.
      </p>
    </div>
  </details>
</template>

<style scoped lang="sass">
.specification-function-section
  border: 1px solid var(--color-border)
  border-radius: var(--radius-sm)
  background: var(--color-surface)

  &__summary
    padding: 18px 20px
    color: var(--color-text)
    cursor: pointer

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 3px

  &__summary-content
    display: flex
    align-items: center
    justify-content: space-between
    gap: 16px

  &__title
    font-size: 20px
    font-weight: 700
    line-height: 1.3

  &__count
    color: var(--color-muted)
    font-size: 13px
    line-height: 1.4

  &__body
    display: grid
    gap: 18px
    padding: 0 20px 20px

  &__description,
  &__empty
    margin: 0
    color: var(--color-muted)
    font-size: 14px
    line-height: 1.6

  &__actions
    display: flex
    flex-wrap: wrap
    gap: 10px

  &__cards
    display: grid
    gap: 14px

@media (max-width: 640px)
  .specification-function-section
    &__summary-content
      align-items: flex-start
      flex-direction: column

    &__actions
      flex-direction: column

      :deep(.base-button)
        width: 100%
</style>
