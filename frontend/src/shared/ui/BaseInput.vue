<script setup>
import { computed, useAttrs, useId } from 'vue'

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
const attrs = useAttrs()
const controlAttrs = computed(() => {
  const nativeAttrs = { ...attrs }
  delete nativeAttrs.class
  delete nativeAttrs.style
  return nativeAttrs
})
</script>

<template>
  <div class="base-input" :class="attrs.class" :style="attrs.style">
    <label class="base-input__label" :for="id || generatedId">{{ label }}</label>
    <div class="base-input__field">
      <input
        v-bind="controlAttrs"
        :id="id || generatedId"
        class="base-input__control"
        :class="{ 'base-input__control--invalid': error, 'base-input__control--with-trailing': $slots.trailing }"
        :type="type"
        :value="modelValue"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="[attrs['aria-describedby'], error && `${id || generatedId}-error`].filter(Boolean).join(' ') || undefined"
        @input="$emit('update:modelValue', $event.target.value)"
      >
      <span v-if="$slots.trailing" class="base-input__trailing"><slot name="trailing" /></span>
    </div>
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

  &__field
    position: relative

  &__control
    width: 100%
    min-height: 49px
    height: 49px
    padding: 12px 14px
    border: 1px solid var(--color-border)
    border-radius: var(--radius-sm)
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

    &--with-trailing
      padding-right: 84px

  &__trailing
    position: absolute
    top: 50%
    right: 10px
    display: inline-flex
    transform: translateY(-50%)

  &__error
    margin: 0
    color: var(--color-danger)
    font-size: 14px
    line-height: 20px
</style>
