<script setup>
defineProps({
  modelValue: Boolean,
})
const emit = defineEmits(['update:modelValue'])
</script>

<template>
  <label class="clarification-toggle">
    <input
      class="clarification-toggle__control"
      type="checkbox"
      :checked="modelValue"
      @change="emit('update:modelValue', $event.target.checked)"
    >
    <span class="clarification-toggle__content">
      <span class="clarification-toggle__label">Требует уточнения</span>
      <span class="clarification-toggle__hint">Локально: не сохраняется после обновления страницы.</span>
    </span>
  </label>
</template>

<style scoped lang="sass">
.clarification-toggle
  display: flex
  align-items: flex-start
  gap: 12px
  color: var(--color-text)
  cursor: pointer

  &__control
    width: 18px
    height: 18px
    margin: 3px 0 0
    accent-color: var(--color-accent-strong)

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

  &__content
    display: grid
    gap: 2px

  &__label
    font-weight: 600
    line-height: 24px

  &__hint
    color: var(--color-muted)
    font-size: 14px
    line-height: 20px
</style>
