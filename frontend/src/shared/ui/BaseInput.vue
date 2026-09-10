<script setup>
import { useId } from 'vue'

defineOptions({ inheritAttrs: false })
defineProps({
  modelValue: { type: [String, Number], default: '' },
  label: { type: String, required: true },
  type: { type: String, default: 'text' },
  error: { type: String, default: '' },
  id: { type: String, default: '' },
})
defineEmits(['update:modelValue'])
const generatedId = useId()
</script>

<template>
  <div class="base-input">
    <label class="base-input__label" :for="id || generatedId">{{ label }}</label>
    <input
      v-bind="$attrs"
      :id="id || generatedId"
      class="base-input__control"
      :class="{ 'base-input__control--invalid': error }"
      :type="type"
      :value="modelValue"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="[$attrs['aria-describedby'], error && `${id || generatedId}-error`].filter(Boolean).join(' ') || undefined"
      @input="$emit('update:modelValue', $event.target.value)"
    >
    <p v-if="error" :id="`${id || generatedId}-error`" class="base-input__error" role="alert">{{ error }}</p>
  </div>
</template>

<style scoped lang="sass">
.base-input
  display: grid
  gap: 8px
  min-width: 0

  &__label
    font-size: 14px
    font-weight: 600
    line-height: 20px

  &__control
    width: 100%
    min-height: 49px
    height: 49px
    padding: 12px 14px
    border: 1px solid var(--color-border)
    border-radius: 8px
    background: var(--color-surface)
    color: var(--color-text)
    line-height: 22px

    &:focus-visible
      outline: 3px solid var(--color-focus)
      outline-offset: 2px

    &:disabled
      background: var(--color-background)
      cursor: not-allowed

    &--invalid
      border-color: var(--color-danger)

  &__error
    margin: 0
    color: var(--color-danger)
    font-size: 14px
    line-height: 20px
</style>
